import { describe, expect, it } from 'vitest';

import { readEnv } from '@/shared/config/env';

describe('readEnv', () => {
    it('uses gateway on localhost:8080 by default', () => {
        expect(readEnv({}).apiBaseUrl).toBe('http://localhost:8080');
    });

    it('joins base url without double slash', () => {
        expect(readEnv({ VITE_API_BASE_URL: 'http://localhost:8080/' }).apiBaseUrl).toBe(
            'http://localhost:8080',
        );
    });
});
