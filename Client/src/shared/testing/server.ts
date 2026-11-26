import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';

/**
 * MSW-сервер для тестов: каждый тест подставляет свои ответы через `server.use(...)`.
 * Глобальные моки для bff/health и bff/me — чтобы StatusPage не падал при навигации
 * после login/register в тестах, которые не связаны с этими эндпоинтами.
 */
export const server = setupServer(
    http.get('http://localhost:8080/bff/health', () =>
        HttpResponse.json({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
    ),
    http.get('http://localhost:8080/bff/me', () =>
        HttpResponse.json({ userId: 'test-user', email: 'test@example.com', role: 'User' }),
    ),
);
