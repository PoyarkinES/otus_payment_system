import { http, HttpResponse } from 'msw';

import {
    getBffHealthMockHandler,
    getBffMeMockHandler,
} from '@/shared/api/generated/endpoints/bff/bff.msw';

const demoId = '00000000-0000-4000-8000-000000000001';
const demoEmail = 'demo@otus.local';
let currentEmail = demoEmail;
const accounts = new Map([[demoEmail, demoId]]);
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
