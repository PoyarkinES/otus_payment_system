import { http, HttpResponse } from 'msw';

import {
    getBffHealthMockHandler,
    getBffMeMockHandler,
    getBffWalletsMockHandler,
} from '@/shared/api/generated/endpoints/bff/bff.msw';
import type { BffWalletResult } from '@/shared/api/generated/model';

const demoId = '00000000-0000-4000-8000-000000000001';
const demoEmail = 'demo@otus.local';
let currentEmail = demoEmail;
const accounts = new Map([[demoEmail, demoId]]);
const wallets = new Map<string, BffWalletResult>([
    [
        demoEmail,
        {
            id: '00000000-0000-4000-8000-000000000002',
            currency: 'RUB',
            available: 84250,
            held: 3500,
            total: 87750,
            status: 'Active',
            recentOperations: [],
        },
    ],
]);

function accountId(email: string) {
    const id = accounts.get(email) ?? crypto.randomUUID();
    accounts.set(email, id);
    return id;
}

function profile(email: string) {
    return { userId: accountId(email), email, status: 'Active', createdAt: '2026-11-01T09:00:00Z' };
}

function tokens() {
    return {
        accessToken: 'mock-access-token',
        refreshToken: 'mock-refresh-token',
        expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        refreshExpiresAt: new Date(Date.now() + 30 * 86400_000).toISOString(),
    };
}

export const handlers = [
    getBffHealthMockHandler({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
    getBffMeMockHandler(() => ({
        userId: accountId(currentEmail),
        email: currentEmail,
        role: currentEmail === 'admin@otus.local' ? 'Admin' : 'User',
    })),
    getBffWalletsMockHandler(() => {
        const wallet = wallets.get(currentEmail);
        if (!wallet) return { wallets: [], totals: [] };
        const { currency, available, held, total } = wallet;
        return { wallets: [wallet], totals: [{ currency, available, held, total }] };
    }),
    http.post('*/api/wallets', async ({ request }) => {
        const email = currentEmail;
        const { currency } = (await request.json()) as { currency: string };
        if (currency !== 'RUB') return new HttpResponse(null, { status: 400 });
        if (wallets.has(email)) {
            return HttpResponse.json(
                { status: 409, detail: 'Кошелёк с такой валютой уже существует.' },
                { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
            );
        }
        const wallet = {
            id: crypto.randomUUID(),
            currency,
            available: 0,
            held: 0,
            total: 0,
            status: 'Active',
            recentOperations: [],
        };
        wallets.set(email, wallet);
        return HttpResponse.json(wallet, { status: 201 });
    }),
    http.post('*/api/identity/auth/register', async ({ request }) => {
        const { email } = (await request.json()) as { email: string };
        return HttpResponse.json({ userId: accountId(email), email }, { status: 201 });
    }),
    http.post('*/api/identity/auth/login', async ({ request }) => {
        const { email } = (await request.json()) as { email: string };
        currentEmail = email;
        return HttpResponse.json(tokens());
    }),
    http.post('*/api/identity/auth/refresh', () => HttpResponse.json(tokens())),
    http.post('*/api/identity/auth/logout', () => new HttpResponse(null, { status: 204 })),
    http.get('*/api/identity/auth/me', () =>
        HttpResponse.json({ userId: accountId(currentEmail), email: currentEmail }),
    ),
    http.get('*/api/identity/users/me', () => HttpResponse.json(profile(currentEmail))),
    http.get('*/api/identity/users/:id', ({ params }) => {
        if (currentEmail !== 'admin@otus.local') return new HttpResponse(null, { status: 403 });
        const account = Array.from(accounts).find(([, id]) => id === params.id);
        return account
            ? HttpResponse.json(profile(account[0]))
            : new HttpResponse(null, { status: 404 });
    }),
];
