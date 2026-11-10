import { defineConfig } from 'orval';

/**
 * Клиент API генерируется из OpenAPI-спеки Gateway — той же, что бэк проверяет на drift в CI.
 * После изменения openapi/gateway.v1.json: `npm run api:generate` и закоммитить результат.
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
});
