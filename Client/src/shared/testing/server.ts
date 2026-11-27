import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';

/**
 * MSW-сервер для тестов: каждый тест подставляет свои ответы через `server.use(...)`.
 * Общие GET-ответы поддерживают навигацию после входа; сценарии ошибок задаются в тестах.
 */
export const server = setupServer(
    http.get('http://localhost:8080/bff/health', () =>
        HttpResponse.json({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
    ),
    http.get('http://localhost:8080/bff/me', () =>
        HttpResponse.json({ userId: 'test-user', email: 'test@example.com', role: 'User' }),
    ),
    http.get('http://localhost:8080/bff/wallets', () =>
        HttpResponse.json({ wallets: [], totals: [] }),
    ),
    http.get('http://localhost:8080/api/identity/users/me', () =>
        HttpResponse.json({
            userId: 'test-user',
            email: 'test@example.com',
            status: 'Active',
            createdAt: '2026-11-01T09:00:00Z',
        }),
    ),
);
