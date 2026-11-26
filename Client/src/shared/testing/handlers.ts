import { http, HttpResponse } from 'msw';
import {
    getBffHealthMockHandler,
    getBffMeMockHandler,
} from '@/shared/api/generated/endpoints/bff/bff.msw';

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

    // Явные пути для Identity: Gateway добавляет префикс /api/identity,
    // которого нет в путях openapi/identity.v1.json (там /auth/*).
    http.post('*/api/identity/auth/register', () =>
        HttpResponse.json({ userId: 'mock-user-id', email: 'mock@example.com' }, { status: 201 }),
    ),
    http.post('*/api/identity/auth/login', () => HttpResponse.json(mockTokens, { status: 200 })),
    http.post('*/api/identity/auth/refresh', () => HttpResponse.json(mockTokens, { status: 200 })),
    http.post('*/api/identity/auth/logout', () => new HttpResponse(null, { status: 204 })),
    http.get('*/api/identity/auth/me', () =>
        HttpResponse.json({ userId: 'mock-user-id', email: 'mock@example.com' }, { status: 200 }),
    ),
];
