import { http, HttpResponse } from 'msw';

import {
    getBffHealthMockHandler,
    getBffMeMockHandler,
    getBffWalletsMockHandler,
} from '@/shared/api/generated/endpoints/bff/bff.msw';
import type { BffWalletResult } from '@/shared/api/generated/model';

const demoEmail = 'demo@otus.local';
let currentEmail = demoEmail;
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
            recentOperations: [
                {
                    id: '00000000-0000-4000-8000-000000000003',
                    type: 'TopUp',
                    currency: 'RUB',
                    amount: 12000,
                    createdAt: '2026-11-27T07:30:00Z',
                },
            ],
        },
    ],
]);

const futureAccess = new Date(Date.now() + 15 * 60 * 1000).toISOString();
const futureRefresh = new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString();

const mockTokens = {
    accessToken: 'mock-access-token',
    refreshToken: 'mock-refresh-token',
    expiresAt: futureAccess,
    refreshExpiresAt: futureRefresh,
};

export const handlers = [
    getBffHealthMockHandler({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
    getBffMeMockHandler({ userId: 'mock-user-id', email: 'mock@example.com', role: 'User' }),

    getBffWalletsMockHandler(() => {
        const wallet = wallets.get(currentEmail);
        return {
            wallets: wallet ? [wallet] : [],
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
        };
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
