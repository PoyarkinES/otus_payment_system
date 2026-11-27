import { http, HttpResponse } from 'msw';

import {
    getBffHealthMockHandler,
    getBffMeMockHandler,
} from '@/shared/api/generated/endpoints/bff/bff.msw';
import type { BffWalletResult, BffWalletsResult } from '@/shared/api/generated/model';
import { demoOperations } from './demoOperations';

const demoId = '00000000-0000-4000-8000-000000000001';
const demoEmail = 'demo@otus.local';
const demoWalletId = '00000000-0000-4000-8000-000000000002';
const accessToken = 'mock-access-token';
let currentEmail = demoEmail;
const accounts = new Map([[demoEmail, demoId]]);
const wallets = new Map<string, BffWalletResult>([
    [
        demoEmail,
        {
            id: demoWalletId,
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
        accessToken,
        refreshToken: 'mock-refresh-token',
        expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
        refreshExpiresAt: new Date(Date.now() + 30 * 86400_000).toISOString(),
    };
}

function integerParameter(params: URLSearchParams, key: string, fallback: number) {
    const values = params.getAll(key);
    if (values.length === 0) return fallback;
    if (values.length !== 1 || !/^[+-]?\d+$/.test(values[0].trim())) return NaN;
    return Number(values[0]);
}

function walletProblem(status: number, title: string, detail: string) {
    return HttpResponse.json(
        { status, title, detail, type: `https://httpstatuses.io/${status}` },
        { status, headers: { 'Content-Type': 'application/problem+json' } },
    );
}

function operationsFor(wallet: BffWalletResult) {
    return wallet.id === demoWalletId ? demoOperations : (wallet.recentOperations ?? []);
}

export const handlers = [
    getBffHealthMockHandler({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
    getBffMeMockHandler(() => ({
        userId: accountId(currentEmail),
        email: currentEmail,
        role: currentEmail === 'admin@otus.local' ? 'Admin' : 'User',
    })),
    http.get('*/bff/wallets', ({ request }) => {
        const limit = integerParameter(new URL(request.url).searchParams, 'recentOperations', 5);
        if (!(limit >= 0 && limit <= 20)) {
            return walletProblem(
                400,
                'Недопустимое количество операций.',
                'Параметр recentOperations должен быть от 0 до 20.',
            );
        }
        const wallet = wallets.get(currentEmail);
        return HttpResponse.json<BffWalletsResult>({
            wallets: wallet
                ? [{ ...wallet, recentOperations: operationsFor(wallet).slice(0, limit) }]
                : [],
            totals: wallet
                ? [
                      {
                          currency: wallet.currency,
                          available: wallet.available,
                          held: wallet.held,
                          total: wallet.total,
                      },
                  ]
                : [],
        });
    }),
    http.get('*/api/wallets/:id/operations', ({ params, request }) => {
        if (request.headers.get('Authorization') !== `Bearer ${accessToken}`) {
            return new HttpResponse(null, { status: 401 });
        }
        const query = new URL(request.url).searchParams;
        const page = integerParameter(query, 'page', 1);
        const pageSize = integerParameter(query, 'pageSize', 20);
        if (!(page >= 1 && page <= 2147483647)) {
            return walletProblem(400, 'Bad Request', 'Номер страницы должен быть не менее 1.');
        }
        if (!(pageSize >= 1 && pageSize <= 100)) {
            return walletProblem(400, 'Bad Request', 'Размер страницы должен быть от 1 до 100.');
        }
        const wallet = [...wallets.values()].find(
            (item) => item.id === String(params.id).toLowerCase(),
        );
        if (!wallet) return walletProblem(404, 'Not Found', 'Кошелёк не найден.');
        if (wallet !== wallets.get(currentEmail)) {
            return walletProblem(403, 'Forbidden', 'Доступ к чужой истории запрещён.');
        }
        const history = operationsFor(wallet);
        const start = (page - 1) * pageSize;
        return HttpResponse.json({
            items: history.slice(start, start + pageSize),
            page,
            pageSize,
            totalCount: history.length,
            totalPages: Math.ceil(history.length / pageSize),
        });
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
