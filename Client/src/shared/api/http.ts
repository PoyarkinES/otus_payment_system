import { env } from '@/shared/config/env';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';

/**
 * Тип ошибки для сгенерированных хуков: orval подставляет его вместо типов ошибок из спеки,
 * потому что apiFetch бросает только эти два класса (ответы 4xx/5xx с телом из спеки — это `ApiProblem`).
 */
export type ErrorType<_SpecError> = ApiProblem | ApiNetworkError;

export type ApiFetchOptions = RequestInit & {
    /**
     * Статусы вне 2xx, которые для этого вызова — нормальный ответ с данными, а не ошибка.
     * Пример: `/bff/health` отвечает 503 тем же телом, что и 200. Всё остальное вне 2xx
     * бросается как `ApiProblem`, поэтому ошибки всегда приходят в `error`, а не в `data`.
     */
    acceptStatus?: number[];
};

/** Заголовок, по которому один запрос прослеживается от браузера до логов всех сервисов. */
export const CORRELATION_HEADER = 'X-Correlation-Id';

/**
 * Единая точка всех HTTP-запросов. Её вызывает сгенерированный orval клиент (`generated/`).
 *
 * - добавляет адрес Gateway из env к пути запроса;
 * - ставит каждому запросу новый `X-Correlation-Id`;
 * - `application/problem+json` и ответы с ошибкой без тела превращает в `ApiProblem`,
 *   недоступность сервера — в `ApiNetworkError`;
 * - любой другой ответ вне 2xx — тоже `ApiProblem`, кроме статусов из `acceptStatus`;
 * - успешные ответы возвращает как `{ data, status, headers }` — эту форму ждёт
 *   сгенерированный клиент.
 */
export async function apiFetch<T>(url: string, options: ApiFetchOptions = {}): Promise<T> {
    const { acceptStatus = [], ...init } = options;
    const correlationId = newCorrelationId();
    const headers = new Headers(init.headers);
    headers.set(CORRELATION_HEADER, correlationId);

    let response: Response;
    try {
        response = await fetch(`${env.apiBaseUrl}${url}`, { ...init, headers });
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

    const accepted = response.ok || acceptStatus.includes(response.status);
    if (!accepted) {
        throw new ApiProblem({
            status: response.status,
            title: response.statusText || `HTTP ${response.status}`,
            traceId,
        });
    }

    const data = contentType.includes('json') ? body : undefined;
    return { data, status: response.status, headers: response.headers } as T;
}

/**
 * `crypto.randomUUID` есть только в secure context (https или localhost). Фронт, открытый
 * по http с другого адреса (стенд в Docker), иначе падал бы на каждом запросе.
 */
function newCorrelationId(): string {
    if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();

    const bytes = crypto.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 0x0f) | 0x40; // версия 4
    bytes[8] = (bytes[8] & 0x3f) | 0x80; // вариант RFC 4122
    const hex = [...bytes].map((b) => b.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
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
