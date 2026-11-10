import { QueryClient } from '@tanstack/react-query';

import { isApiProblem } from '@/shared/api/problem';

/** Ошибки клиента (4xx) повтор не исправит; сбой сервера или сети повторяем один раз. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
    if (isApiProblem(error) && error.status < 500) return false;
    return failureCount < 1;
}

export function createQueryClient(): QueryClient {
    return new QueryClient({
        defaultOptions: {
            queries: {
                retry: shouldRetry,
                refetchOnWindowFocus: false,
            },
        },
    });
}
