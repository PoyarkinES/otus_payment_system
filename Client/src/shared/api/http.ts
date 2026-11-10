import { env } from '@/shared/config/env';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';

/** Заголовок, по которому один запрос прослеживается от браузера до логов всех сервисов. */
export const CORRELATION_HEADER = 'X-Correlation-Id';

/**
 * Единая точка всех HTTP-запросов. Её вызывает сгенерированный orval клиент (`generated/`).
 *
 * - добавляет адрес Gateway из env к пути запроса;
 * - ставит каждому запросу новый `X-Correlation-Id`;
 * - `application/problem+json` и ответы с ошибкой без тела превращает в `ApiProblem`,
 *   недоступность сервера — в `ApiNetworkError`;
 * - остальные ответы (в т.ч. 503 с JSON-телом у `/bff/health`) возвращает как
 *   `{ data, status, headers }` — эту форму ждёт сгенерированный клиент.
 */
export async function apiFetch<T>(url: string, options: RequestInit = {}): Promise<T> {
    const correlationId = crypto.randomUUID();
    const headers = new Headers(options.headers);
    headers.set(CORRELATION_HEADER, correlationId);

    let response: Response;
    try {
        response = await fetch(`${env.apiBaseUrl}${url}`, { ...options, headers });
    } catch (error) {
        // Отмену запроса (TanStack Query отменяет устаревшие) не выдаём за недоступность сервера.
        if (error instanceof Error && error.name === 'AbortError') throw error;
        throw new ApiNetworkError(correlationId, { cause: error });
    }

    const traceId = response.headers.get(CORRELATION_HEADER) ?? correlationId;
    const contentType = response.headers.get('Content-Type') ?? '';
    const body = parseJson(await response.text());

    if (contentType.includes('application/problem+json') && isObject(body)) {
        throw problemFromBody(body, response, traceId);
    }

    const data = contentType.includes('json') ? body : undefined;
    if (!response.ok && data === undefined) {
        throw new ApiProblem({
            status: response.status,
            title: response.statusText || `HTTP ${response.status}`,
            traceId,
        });
    }

    return { data, status: response.status, headers: response.headers } as T;
}

function problemFromBody(body: Record<string, unknown>, response: Response, traceId: string) {
    return new ApiProblem({
        status: typeof body.status === 'number' ? body.status : response.status,
        title: asString(body.title) ?? (response.statusText || `HTTP ${response.status}`),
        detail: asString(body.detail),
        type: asString(body.type),
        traceId: asString(body.traceId) ?? traceId,
        errors: isObject(body.errors) ? (body.errors as Record<string, string[]>) : undefined,
    });
}

function parseJson(text: string): unknown {
    if (!text) return undefined;
    try {
        return JSON.parse(text);
    } catch {
        return undefined;
    }
}

function isObject(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asString(value: unknown): string | undefined {
    return typeof value === 'string' && value !== '' ? value : undefined;
}
