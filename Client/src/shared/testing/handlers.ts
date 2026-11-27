import { http, HttpResponse } from 'msw';

import {
    getBffHealthMockHandler,
    getBffMeMockHandler,
} from '@/shared/api/generated/endpoints/bff/bff.msw';
import type { BffWalletResult, BffWalletsResult } from '@/shared/api/generated/model';
import { demoOperations } from './demoOperations';

const demoEmail = 'demo@otus.local';
const demoWalletId = '00000000-0000-4000-8000-000000000002';
const accessToken = 'mock-access-token';
let currentEmail = demoEmail;
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
        },
    ],
]);

const futureAccess = new Date(Date.now() + 15 * 60 * 1000).toISOString();
const futureRefresh = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString();

const mockTokens = {
    accessToken,
    refreshToken: 'mock-refresh-token',
    expiresAt: futureAccess,
    refreshExpiresAt: futureRefresh,
};

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
    getBffMeMockHandler({ userId: 'mock-user-id', email: 'mock@example.com', role: 'User' }),

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
    http.post('*/api/wallets', () => {
        if (wallets.has(currentEmail)) {
            return HttpResponse.json(
                { status: 409, detail: 'Кошелёк с такой валютой уже существует.' },
                { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
            );
        }
        const wallet = {
            id: crypto.randomUUID(),
            currency: 'RUB',
            available: 0,
            held: 0,
            total: 0,
            status: 'Active',
            recentOperations: [],
        };
        wallets.set(currentEmail, wallet);
        return HttpResponse.json(wallet, { status: 201 });
    }),

    // Явные пути для Identity: Gateway добавляет префикс /api/identity,
    // которого нет в путях openapi/identity.v1.json (там /auth/*).
    http.post('*/api/identity/auth/register', () =>
        HttpResponse.json({ userId: 'mock-user-id', email: 'mock@example.com' }, { status: 201 }),
    ),
    http.post('*/api/identity/auth/login', async ({ request }) => {
        const { email } = (await request.json()) as { email: string };
        currentEmail = email;
        return HttpResponse.json(mockTokens, { status: 200 });
    }),
    http.post('*/api/identity/auth/refresh', () => HttpResponse.json(mockTokens, { status: 200 })),
    http.post('*/api/identity/auth/logout', () => new HttpResponse(null, { status: 204 })),
    http.get('*/api/identity/auth/me', () =>
        HttpResponse.json({ userId: 'mock-user-id', email: 'mock@example.com' }, { status: 200 }),
    ),
];
