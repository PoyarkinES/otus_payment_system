import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it } from 'vitest';

import { apiFetch, CORRELATION_HEADER, REFRESH_URL } from '@/shared/api/http';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';
import { clearTokens, getAccessToken, setTokens } from '@/shared/api/auth/storage';
import { server } from '@/shared/testing/server';

afterEach(() => {
    clearTokens();
});

const URL = 'http://localhost:8080/bff/health';
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

type Result<T> = { data: T; status: number; headers: Headers };

async function catchError(promise: Promise<unknown>): Promise<unknown> {
    try {
        await promise;
    } catch (error) {
        return error;
    }
    throw new Error('ожидалось исключение');
}

function problemJson(body: object, status: number, headers: Record<string, string> = {}) {
    return HttpResponse.json(body, {
        status,
        headers: { 'Content-Type': 'application/problem+json', ...headers },
    });
}

describe('apiFetch', () => {
    it('sends X-Correlation-Id header', async () => {
        let sent: string | null = null;
        server.use(
            http.get(URL, ({ request }) => {
                sent = request.headers.get(CORRELATION_HEADER);
                return HttpResponse.json({ status: 'ok' });
            }),
        );

        await apiFetch('/bff/health');

        expect(sent).toMatch(UUID);
    });

    it('returns data and status for 200', async () => {
        server.use(http.get(URL, () => HttpResponse.json({ status: 'ok' })));

        const result = await apiFetch<Result<{ status: string }>>('/bff/health');

        expect(result.status).toBe(200);
        expect(result.data).toEqual({ status: 'ok' });
    });

    it('returns body for 503 with json body', async () => {
        const body = { status: 'degraded', services: { gateway: 'ok', identity: 'unavailable' } };
        server.use(http.get(URL, () => HttpResponse.json(body, { status: 503 })));

        const result = await apiFetch<Result<typeof body>>('/bff/health', { acceptStatus: [503] });

        expect(result.status).toBe(503);
        expect(result.data.status).toBe('degraded');
    });

    it('throws ApiProblem for non-ok json response that is not accepted', async () => {
        server.use(http.get(URL, () => HttpResponse.json({ message: 'oops' }, { status: 500 })));

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        expect((error as ApiProblem).status).toBe(500);
    });

    it('does not pass acceptStatus to fetch as a header or option', async () => {
        let headerNames: string[] = [];
        server.use(
            http.get(URL, ({ request }) => {
                headerNames = [...request.headers.keys()];
                return HttpResponse.json({ status: 'ok' });
            }),
        );

        await apiFetch('/bff/health', { acceptStatus: [503] });

        expect(headerNames).not.toContain('acceptstatus');
    });

    it('sends correlation id when crypto.randomUUID is unavailable', async () => {
        // Вне secure context (http не на localhost) randomUUID отсутствует.
        const original = crypto.randomUUID;
        Object.defineProperty(crypto, 'randomUUID', { value: undefined, configurable: true });
        let sent: string | null = null;
        server.use(
            http.get(URL, ({ request }) => {
                sent = request.headers.get(CORRELATION_HEADER);
                return HttpResponse.json({ status: 'ok' });
            }),
        );

        try {
            await apiFetch('/bff/health');
        } finally {
            Object.defineProperty(crypto, 'randomUUID', { value: original, configurable: true });
        }

        expect(sent).toMatch(UUID);
    });

    it('throws ApiProblem from problem+json', async () => {
        server.use(
            http.get(URL, () =>
                problemJson(
                    {
                        title: 'Internal Server Error',
                        status: 500,
                        detail: 'Ошибка',
                        traceId: 'abc',
                    },
                    500,
                ),
            ),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        expect(error).toMatchObject({ status: 500, detail: 'Ошибка', traceId: 'abc' });
    });

    it('takes trace id from response header when problem body has none', async () => {
        server.use(
            http.get(URL, () =>
                problemJson({ title: 'Conflict', status: 409 }, 409, {
                    [CORRELATION_HEADER]: 'hdr',
                }),
            ),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toMatchObject({ status: 409, traceId: 'hdr' });
    });

    it('maps validation errors', async () => {
        server.use(
            http.get(URL, () =>
                problemJson(
                    { title: 'Bad Request', status: 400, errors: { email: ['Обязательное поле'] } },
                    400,
                ),
            ),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect((error as ApiProblem).errors.email[0]).toBe('Обязательное поле');
    });

    it('builds problem from status when body is not problem+json', async () => {
        server.use(
            http.get(
                URL,
                () =>
                    new HttpResponse(null, {
                        status: 502,
                        statusText: 'Bad Gateway',
                        headers: { [CORRELATION_HEADER]: 'xyz' },
                    }),
            ),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        expect(error).toMatchObject({ status: 502, traceId: 'xyz' });
    });

    it('builds problem from status when body is html', async () => {
        server.use(
            http.get(URL, () => HttpResponse.html('<h1>Gateway Timeout</h1>', { status: 504 })),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        expect((error as ApiProblem).status).toBe(504);
        expect((error as ApiProblem).traceId).toMatch(UUID);
    });

    it('throws ApiNetworkError when fetch fails', async () => {
        server.use(http.get(URL, () => HttpResponse.error()));

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiNetworkError);
        expect((error as ApiNetworkError).correlationId).toMatch(UUID);
    });

    it('rethrows abort as is', async () => {
        server.use(http.get(URL, () => HttpResponse.json({ status: 'ok' })));
        const controller = new AbortController();
        controller.abort();

        const error = await catchError(apiFetch('/bff/health', { signal: controller.signal }));

        expect(error).not.toBeInstanceOf(ApiNetworkError);
        expect((error as Error).name).toBe('AbortError');
    });

    // --- Authorization tests ---

    it('sends Authorization header when access token is set', async () => {
        setTokens('test-at', 'test-rt');
        let authHeader: string | null = null;
        server.use(
            http.get(URL, ({ request }) => {
                authHeader = request.headers.get('Authorization');
                return HttpResponse.json({ status: 'ok' });
            }),
        );

        await apiFetch('/bff/health');

        expect(authHeader).toBe('Bearer test-at');
    });

    it('does not send Authorization header when no token', async () => {
        let authHeader: string | null = null;
        server.use(
            http.get(URL, ({ request }) => {
                authHeader = request.headers.get('Authorization');
                return HttpResponse.json({ status: 'ok' });
            }),
        );

        await apiFetch('/bff/health');

        expect(authHeader).toBeNull();
    });

    // --- 401 → refresh tests ---

    it('handles 401 problem+json by calling refresh and retrying', async () => {
        setTokens('old-at', 'old-rt');
        let callCount = 0;
        let authHeader = '';

        server.use(
            http.get(URL, ({ request }) => {
                callCount++;
                authHeader = request.headers.get('Authorization') ?? '';
                if (callCount === 1) {
                    return problemJson(
                        { title: 'Unauthorized', status: 401, detail: 'Токен истёк' },
                        401,
                    );
                }
                return HttpResponse.json({ status: 'ok' });
            }),
        );

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

        const result = await apiFetch<Result<{ status: string }>>('/bff/health');

        expect(callCount).toBe(2);
        expect(authHeader).toBe('Bearer new-at');
        expect(result.data).toEqual({ status: 'ok' });
        expect(getAccessToken()).toBe('new-at');
    });

    it('does not refresh on 401 when no access token set', async () => {
        let refreshCalled = false;
        server.use(http.get(URL, () => problemJson({ title: 'Unauthorized', status: 401 }, 401)));
        server.use(
            http.post('http://localhost:8080' + REFRESH_URL, () => {
                refreshCalled = true;
                return HttpResponse.json({ accessToken: 'new', refreshToken: 'new' });
            }),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        expect(refreshCalled).toBe(false);
    });

    it('clears tokens on invalid refresh (401)', async () => {
        setTokens('old-at', 'old-rt');

        server.use(http.get(URL, () => problemJson({ title: 'Unauthorized', status: 401 }, 401)));
        server.use(
            http.post('http://localhost:8080' + REFRESH_URL, () =>
                problemJson({ title: 'Invalid', status: 401 }, 401),
            ),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        expect(getAccessToken()).toBeNull();
    });

    it('does not clear tokens on refresh network error', async () => {
        setTokens('old-at', 'old-rt');

        server.use(http.get(URL, () => problemJson({ title: 'Unauthorized', status: 401 }, 401)));
        server.use(http.post('http://localhost:8080' + REFRESH_URL, () => HttpResponse.error()));

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        // Токены не очищены при сетевой ошибке refresh
        expect(getAccessToken()).toBe('old-at');
    });

    it('does not clear tokens on refresh 429 (rate limit)', async () => {
        setTokens('old-at', 'old-rt');

        server.use(http.get(URL, () => problemJson({ title: 'Unauthorized', status: 401 }, 401)));
        server.use(
            http.post('http://localhost:8080' + REFRESH_URL, () =>
                problemJson({ title: 'Too Many Requests', status: 429 }, 429),
            ),
        );

        const error = await catchError(apiFetch('/bff/health'));

        expect(error).toBeInstanceOf(ApiProblem);
        // Токены не очищены при 429
        expect(getAccessToken()).toBe('old-at');
    });

    it('skips refresh on refresh endpoint itself', async () => {
        setTokens('old-at', 'old-rt');
        let refreshCallCount = 0;

        server.use(
            http.post('http://localhost:8080' + REFRESH_URL, () => {
                refreshCallCount++;
                return problemJson({ title: 'Unauthorized', status: 401 }, 401);
            }),
        );

        // Прямой вызов POST refresh — не должен триггерить рекурсивный refresh
        const error = await catchError(
            apiFetch(REFRESH_URL, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ refreshToken: 'old-rt' }),
            }),
        );

        expect(error).toBeInstanceOf(ApiProblem);
        // refresh вызван только 1 раз (прямой вызов), не рекурсивно
        expect(refreshCallCount).toBe(1);
    });
});
