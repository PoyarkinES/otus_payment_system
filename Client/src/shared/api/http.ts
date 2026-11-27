import { env } from '@/shared/config/env';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';
import {
    getSnapshot,
    rotateTokens,
    type AuthTokensStorage,
    clearTokens,
} from '@/shared/api/auth/storage';
import type { AuthTokens } from '@/shared/api/generated/identity/model';

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

/** Путь refresh эндпоинта — не требует авторизации. */
export const REFRESH_URL = '/api/identity/auth/refresh';

const publicAuthUrls = new Set([
    REFRESH_URL,
    '/api/identity/auth/login',
    '/api/identity/auth/register',
]);

const pendingRefreshes = new WeakMap<AuthTokensStorage, Promise<boolean>>();

/**
 * Единая точка всех HTTP-запросов. Её вызывает сгенерированный orval клиент (`generated/`).
 *
 * - добавляет адрес Gateway из env к пути запроса;
 * - ставит каждому запросу новый `X-Correlation-Id`;
 * - прикрепляет `Authorization: Bearer` из текущего access-токена;
 * - при 401 автоматически пробует refresh (single-flight); если refresh удался — повторяет запрос;
 * - `application/problem+json` и ответы с ошибкой без тела превращает в `ApiProblem`,
 *   недоступность сервера — в `ApiNetworkError`;
 * - любой другой ответ вне 2xx — тоже `ApiProblem`, кроме статусов из `acceptStatus`;
 * - успешные ответы возвращает как `{ data, status, headers }` — эту форму ждёт
 *   сгенерированный клиент.
 */
export function apiFetch<T>(url: string, options: ApiFetchOptions = {}): Promise<T> {
    return sendRequest(url, options, true);
}

async function sendRequest<T>(
    url: string,
    options: ApiFetchOptions,
    allowRefresh: boolean,
): Promise<T> {
    const { acceptStatus = [], ...init } = options;
    const correlationId = newCorrelationId();
    const headers = new Headers(init.headers);
    headers.set(CORRELATION_HEADER, correlationId);

    // Прикрепляем access-токен, если он есть.
    const session = getSnapshot();
    const authenticated = session.accessToken !== null && !publicAuthUrls.has(url);
    if (authenticated) {
        headers.set('Authorization', `Bearer ${session.accessToken}`);
    }

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
    const bodyText = await response.text();
    const body = parseJson(bodyText);

    const current = getSnapshot();
    // Logout использует refresh-токен в теле, его нельзя повторить после ротации.
    if (
        response.status === 401 &&
        authenticated &&
        current.sessionId === session.sessionId &&
        url !== '/api/identity/auth/logout'
    ) {
        if (allowRefresh) {
            // Другой запрос мог уже обновить токены этой же сессии.
            const refreshed = current !== session || (await refreshSession());
            if (refreshed && getSnapshot().sessionId === session.sessionId) {
                return sendRequest(url, options, false);
            }
        } else if (current === session) {
            clearTokens();
        }
    }

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

/** Все запросы с одной парой токенов ожидают одну ротацию. */
export function refreshSession(): Promise<boolean> {
    const session = getSnapshot();
    if (!session.refreshToken) return Promise.resolve(false);
    const pending = pendingRefreshes.get(session);
    if (pending) return pending;

    const rotation = requestRotation(session).finally(() => pendingRefreshes.delete(session));
    pendingRefreshes.set(session, rotation);
    return rotation;
}

async function requestRotation(session: AuthTokensStorage): Promise<boolean> {
    try {
        const { data } = await apiFetch<{ data: AuthTokens }>(REFRESH_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ refreshToken: session.refreshToken }),
        });
        if (getSnapshot() !== session) return false;
        rotateTokens(data);
        return true;
    } catch (error) {
        if (
            getSnapshot() === session &&
            error instanceof ApiProblem &&
            [400, 401, 403].includes(error.status)
        ) {
            clearTokens();
        }
        throw error;
    }
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
