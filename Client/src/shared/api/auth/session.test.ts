import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import {
    ensureFreshSession,
    isAccessTokenFresh,
    getCurrentRefreshToken,
} from '@/shared/api/auth/session';
import {
    clearTokens,
    setTokens,
    setTokensStorage,
    getAccessToken,
} from '@/shared/api/auth/storage';
import { server } from '@/shared/testing/server';
import { REFRESH_URL } from '@/shared/api/http';

afterEach(() => {
    clearTokens();
    vi.restoreAllMocks();
});

describe('session', () => {
    describe('isAccessTokenFresh', () => {
        it('returns false when no tokens', () => {
            expect(isAccessTokenFresh()).toBe(false);
        });

        it('returns false when no expiresAt', () => {
            setTokens('at', 'rt');
            expect(isAccessTokenFresh()).toBe(false);
        });

        it('returns true when access token is fresh', () => {
            const future = new Date(Date.now() + 3600000).toISOString();
            setTokensStorage('at', 'rt', future, '');
            expect(isAccessTokenFresh()).toBe(true);
        });

        it('returns false when access token expires soon (within 30s margin)', () => {
            const soon = new Date(Date.now() + 10000).toISOString(); // 10 seconds
            setTokensStorage('at', 'rt', soon, '');
            expect(isAccessTokenFresh()).toBe(false);
        });

        it('returns false when access token is expired', () => {
            const past = new Date(Date.now() - 1000).toISOString();
            setTokensStorage('at', 'rt', past, '');
            expect(isAccessTokenFresh()).toBe(false);
        });
    });

    describe('ensureFreshSession', () => {
        it('returns true when access token is already fresh', async () => {
            const future = new Date(Date.now() + 3600000).toISOString();
            setTokensStorage('at', 'rt', future, '');

            const result = await ensureFreshSession();

            expect(result).toBe(true);
        });

        it('refreshes session when access token is not fresh', async () => {
            const soon = new Date(Date.now() + 10000).toISOString();
            setTokensStorage('old-at', 'old-rt', soon, '');

            server.use(
                http.post('http://localhost:8080' + REFRESH_URL, () =>
                    HttpResponse.json({
                        accessToken: 'new-at',
                        refreshToken: 'new-rt',
                        expiresAt: new Date(Date.now() + 3600000).toISOString(),
                        refreshExpiresAt: new Date(Date.now() + 86400000).toISOString(),
                    }),
                ),
            );

            const result = await ensureFreshSession();

            expect(result).toBe(true);
            expect(getAccessToken()).toBe('new-at');
        });

        it('returns false and clears tokens on invalid refresh (401)', async () => {
            const soon = new Date(Date.now() + 10000).toISOString();
            setTokensStorage('old-at', 'old-rt', soon, '');

            server.use(
                http.post(
                    'http://localhost:8080' + REFRESH_URL,
                    () =>
                        new HttpResponse(JSON.stringify({ title: 'Invalid', status: 401 }), {
                            status: 401,
                            headers: { 'Content-Type': 'application/problem+json' },
                        }),
                ),
            );

            const result = await ensureFreshSession();

            expect(result).toBe(false);
            expect(getAccessToken()).toBeNull();
        });

        it('returns false but keeps tokens on network error', async () => {
            const soon = new Date(Date.now() + 10000).toISOString();
            setTokensStorage('old-at', 'old-rt', soon, '');

            server.use(
                http.post('http://localhost:8080' + REFRESH_URL, () => HttpResponse.error()),
            );

            const result = await ensureFreshSession();

            expect(result).toBe(false);
            // Токены не очищены при сетевой ошибке
            expect(getAccessToken()).toBe('old-at');
        });

        it('returns false but keeps tokens on 429 (rate limit)', async () => {
            const soon = new Date(Date.now() + 10000).toISOString();
            setTokensStorage('old-at', 'old-rt', soon, '');

            server.use(
                http.post(
                    'http://localhost:8080' + REFRESH_URL,
                    () =>
                        new HttpResponse(
                            JSON.stringify({ title: 'Too Many Requests', status: 429 }),
                            {
                                status: 429,
                                headers: { 'Content-Type': 'application/problem+json' },
                            },
                        ),
                ),
            );

            const result = await ensureFreshSession();

            expect(result).toBe(false);
            // Токены не очищены при 429
            expect(getAccessToken()).toBe('old-at');
        });

        it('returns false when no refresh token', async () => {
            clearTokens();
            const result = await ensureFreshSession();
            expect(result).toBe(false);
        });
    });

    describe('getCurrentRefreshToken', () => {
        it('returns null when no tokens', () => {
            expect(getCurrentRefreshToken()).toBeNull();
        });

        it('returns current refresh token', () => {
            setTokens('at', 'test-rt');
            expect(getCurrentRefreshToken()).toBe('test-rt');
        });
    });
});
