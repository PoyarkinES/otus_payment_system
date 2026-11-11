import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { apiFetch, CORRELATION_HEADER } from '@/shared/api/http';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';
import { server } from '@/shared/testing/server';

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
});
