import { beforeEach, expect, it } from 'vitest';

import type { BffWalletsResult, BffWalletOperationResult } from '@/shared/api/generated/model';
import { handlers } from './handlers';
import { server } from './server';

const api = 'http://localhost:8080';
const demoEmail = 'demo@otus.local';
const walletId = '00000000-0000-4000-8000-000000000002';
const operations = `${api}/api/wallets/${walletId}/operations`;
let headers: { Authorization: string };

async function login(email: string) {
    const response = await fetch(`${api}/api/identity/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password: 'demo-password' }),
    });
    const { accessToken } = await response.json();
    return { Authorization: `Bearer ${accessToken}` };
}

beforeEach(async () => {
    server.use(...handlers);
    headers = await login(demoEmail);
});

it('uses the same five newest entries for the wallet preview and paginated ledger', async () => {
    const response = await fetch(`${api}/bff/wallets`, { headers });
    const { wallets } = (await response.json()) as BffWalletsResult;
    const recent = wallets?.[0].recentOperations;
    expect(recent).toHaveLength(5);

    const page = await (await fetch(operations, { headers })).json();
    expect(page).toMatchObject({ page: 1, pageSize: 20 });
    expect(page.items).toHaveLength(20);
    expect(page.items.slice(0, 5)).toEqual(recent);
    const next = await (await fetch(`${operations}?page=2&pageSize=5`, { headers })).json();
    expect(next.items).toEqual(page.items.slice(5, 10));
    expect(next.totalPages).toBe(Math.ceil(page.totalCount / 5));
    const beyond = await (await fetch(`${operations}?page=1000`, { headers })).json();
    expect(beyond).toMatchObject({ items: [], totalCount: page.totalCount });
});

it('reconciles the demo ledger from the opening deposit through the current total', async () => {
    const response = await fetch(`${operations}?pageSize=100`, { headers });
    const {
        items,
        totalCount,
    }: { items: Required<BffWalletOperationResult>[]; totalCount: number } = await response.json();
    expect(items.length).toBeGreaterThanOrEqual(60);
    expect(items).toHaveLength(totalCount);
    expect(new Set(items.map((item) => item.id)).size).toBe(totalCount);
    expect(items[0].balanceAfter).toBe(87750);
    expect(items.at(-1)?.balanceBefore).toBe(0);
    for (const [index, item] of items.entries()) {
        const credit = ['TopUp', 'TransferIn', 'Refund'].includes(item.type ?? '');
        expect(item.balanceAfter - item.balanceBefore).toBeCloseTo(
            credit ? item.amount : -item.amount,
        );
        if (index > 0) {
            expect(item.balanceAfter).toBe(items[index - 1].balanceBefore);
            expect(Date.parse(item.createdAt)).toBeLessThanOrEqual(
                Date.parse(items[index - 1].createdAt),
            );
        }
    }
});

it.each([0, 1, 20])('honours a preview limit of %i', async (limit) => {
    const response = await fetch(`${api}/bff/wallets?recentOperations=${limit}`, { headers });
    const { wallets } = (await response.json()) as BffWalletsResult;
    expect(wallets?.[0].recentOperations).toHaveLength(limit);
});

it.each(['page=0', 'pageSize=101', 'pageSize=0', 'page=1.2', 'pageSize=abc'])(
    'rejects invalid pagination %s',
    async (query) => {
        expect((await fetch(`${operations}?${query}`, { headers })).status).toBe(400);
    },
);

it('rejects an invalid preview limit', async () => {
    expect((await fetch(`${api}/bff/wallets?recentOperations=21`, { headers })).status).toBe(400);
});

it('distinguishes a new empty wallet from a missing or foreign wallet', async () => {
    const otherHeaders = await login('new-wallet@otus.local');
    expect((await fetch(operations, { headers: otherHeaders })).status).toBe(403);
    const created = await fetch(`${api}/api/wallets`, { method: 'POST', headers: otherHeaders });
    const { id } = await created.json();
    const own = await fetch(`${api}/api/wallets/${id}/operations`, { headers: otherHeaders });
    expect(await own.json()).toMatchObject({ items: [], totalPages: 0, totalCount: 0 });
    const missing = await fetch(`${api}/api/wallets/${crypto.randomUUID()}/operations`, {
        headers: otherHeaders,
    });
    expect(missing.status).toBe(404);
    expect((await fetch(operations)).status).toBe(401);
});
