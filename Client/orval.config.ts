import { defineConfig } from 'orval';

/**
 * Клиент API генерируется из OpenAPI-спек — той же, что бэк проверяет на drift в CI.
 * После изменения openapi/*.v1.json: `npm run api:generate` и закоммитить результат.
 *
 * Два target'а:
 * - gateway: BFF-агрегаты /bff/* и health Gateway.
 * - identity: Auth-эндпоинты /api/identity/auth/* (login, register, refresh, logout, me).
 *
 * У каждого target'а свои папки schemas — clean: true не затирает модели другого target'а.
 */
export default defineConfig({
    gateway: {
        input: {
            target: '../openapi/gateway.v1.json',
        },
        output: {
            client: 'react-query',
            httpClient: 'fetch',
            mode: 'tags-split',
            target: 'src/shared/api/generated/endpoints',
            schemas: 'src/shared/api/generated/model',
            mock: true,
            clean: true,
            formatter: 'prettier',
            override: {
                // Все запросы идут через apiFetch: адрес Gateway, correlation ID, ProblemDetails.
                mutator: {
                    path: 'src/shared/api/http.ts',
                    name: 'apiFetch',
                },
            },
        },
    },
    identity: {
        input: {
            target: '../openapi/identity.v1.json',
        },
        output: {
            client: 'react-query',
            httpClient: 'fetch',
            mode: 'tags-split',
            target: 'src/shared/api/generated/identity/endpoints',
            schemas: 'src/shared/api/generated/identity/model',
            baseUrl: '/api/identity',
            mock: true,
            clean: true,
            formatter: 'prettier',
            override: {
                mutator: {
                    path: 'src/shared/api/http.ts',
                    name: 'apiFetch',
                },
            },
        },
    },
});
