import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';

import { server } from '@/shared/testing/server';

// Запрос без подготовленного ответа — ошибка теста, а не поход в настоящую сеть.
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }));
afterEach(() => {
    // Без globals: true Testing Library сама не убирает DOM прошлого теста.
    cleanup();
    server.resetHandlers();
});
afterAll(() => server.close());
