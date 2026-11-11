import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { expect, it } from 'vitest';

import { useBffHealth } from '@/shared/api/generated/endpoints/bff/bff';
import { ApiProblem } from '@/shared/api/problem';
import { server } from '@/shared/testing/server';

function wrapper({ children }: { children: ReactNode }) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

it('generated hook goes through apiFetch to the gateway', async () => {
    server.use(
        http.get('http://localhost:8080/bff/health', () =>
            HttpResponse.json({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
        ),
    );

    const { result } = renderHook(() => useBffHealth(), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.status).toBe(200);
    expect(result.current.data?.data.status).toBe('ok');
});

it('generated hook exposes ApiProblem as error', async () => {
    server.use(
        http.get('http://localhost:8080/bff/health', () =>
            HttpResponse.json(
                { title: 'Too Many Requests', status: 429 },
                { status: 429, headers: { 'Content-Type': 'application/problem+json' } },
            ),
        ),
    );

    const { result } = renderHook(() => useBffHealth(), { wrapper });

    await waitFor(() => expect(result.current.isError).toBe(true));
    const error: ApiProblem | Error | null = result.current.error;
    expect(error).toBeInstanceOf(ApiProblem);
});
