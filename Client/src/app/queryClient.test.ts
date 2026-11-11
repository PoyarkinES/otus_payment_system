import { describe, expect, it } from 'vitest';

import { shouldRetry } from '@/app/queryClient';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';

describe('shouldRetry', () => {
    it('does not retry client errors', () => {
        expect(shouldRetry(0, new ApiProblem({ status: 404, title: 'Not Found' }))).toBe(false);
        expect(shouldRetry(0, new ApiProblem({ status: 429, title: 'Too Many' }))).toBe(false);
    });

    it('retries server and network errors once', () => {
        const serverError = new ApiProblem({ status: 502, title: 'Bad Gateway' });
        const networkError = new ApiNetworkError('id');

        expect(shouldRetry(0, serverError)).toBe(true);
        expect(shouldRetry(1, serverError)).toBe(false);
        expect(shouldRetry(0, networkError)).toBe(true);
        expect(shouldRetry(1, networkError)).toBe(false);
    });
});
