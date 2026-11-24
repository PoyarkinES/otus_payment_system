import { env } from '@/shared/config/env';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';
import {
    getAccessToken,
    getRefreshToken,
    setTokensStorage,
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

/**
 * Single-flight refresh: один общий Promise на все параллельные 401.
 * При первом 401 создаётся promise, все последующие 401 ждут его завершения.
 * После завершения — сброс в null.
 */
let refreshPromise: Promise<boolean> | null = null;

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
export async function apiFetch<T>(url: string, options: ApiFetchOptions = {}): Promise<T> {
    const { acceptStatus = [], ...init } = options;
    const correlationId = newCorrelationId();
    const headers = new Headers(init.headers);
    headers.set(CORRELATION_HEADER, correlationId);

    // Прикрепляем access-токен, если он есть.
    const at = getAccessToken();
    if (at !== null) {
        headers.set('Authorization', `Bearer ${at}`);
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

    // 401 обрабатывается ДО throw по problem+json — иначе refresh недостижим,
    // так как все 401 в системе приходят как ProblemDetails.
    if (response.status === 401 && at !== null && url !== REFRESH_URL) {
        const refreshed = await tryRefresh();
        if (refreshed) {
            // Повторяем запрос с новым токеном.
            return retryRequest(url, init, acceptStatus);
        }
        // Refresh не удался — токены очищены, клиент должен перейти на login.
        // Продолжаем ниже для бросания ошибки.
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

/** Повторяет запрос с новым токеном после успешного refresh. */
async function retryRequest<T>(url: string, init: RequestInit, acceptStatus: number[]): Promise<T> {
    const retryHeaders = new Headers(init.headers);
    retryHeaders.set(CORRELATION_HEADER, newCorrelationId());
    const newAt = getAccessToken();
    if (newAt !== null) {
        retryHeaders.set('Authorization', `Bearer ${newAt}`);
    }

    const retryResponse = await fetch(`${env.apiBaseUrl}${url}`, {
        ...init,
        headers: retryHeaders,
    });
    const retryContentType = retryResponse.headers.get('Content-Type') ?? '';
    const retryBodyText = await retryResponse.text();
    const retryBody = parseJson(retryBodyText);
    const retryTraceId = retryResponse.headers.get(CORRELATION_HEADER) ?? '';

    if (retryContentType.includes('application/problem+json') && isObject(retryBody)) {
        throw problemFromBody(retryBody, retryResponse, retryTraceId);
    }

    if (retryResponse.ok || acceptStatus.includes(retryResponse.status)) {
        const retryData = retryContentType.includes('json') ? retryBody : undefined;
        return {
            data: retryData,
            status: retryResponse.status,
            headers: retryResponse.headers,
        } as T;
    }

    throw new ApiProblem({
        status: retryResponse.status,
        title: retryResponse.statusText || `HTTP ${retryResponse.status}`,
        traceId: retryTraceId,
    });
}

/**
 * Пробует обновить токены через /auth/refresh.
 * Использует single-flight pattern: только один refresh за раз.
 *
 * Возвращает true, если refresh удался и токены обновлены.
 * При неудаче очищает токены только при невалидном refresh (400/401),
 * а не при сетевых ошибках, 429 или 5xx — чтобы не выкидывать пользователя
 * из-за моргнувшей сети или rate limit.
 */
async function tryRefresh(): Promise<boolean> {
    // Single-flight: если refresh уже идёт, ждём его результат.
    if (refreshPromise !== null) {
        return refreshPromise;
    }

    const rt = getRefreshToken();
    if (rt === null) return false;

    // Создаём общий promise для всех параллельных 401.
    // Вызываем refresh напрямую через apiFetch, чтобы не зависеть от сгенерированных endpoints
    // и избежать цикла импортов (http.ts ↔ generated/identity/auth.ts).
    refreshPromise = (async () => {
        try {
            const response = await apiFetch<{
                data: AuthTokens;
                status: number;
                headers: Headers;
            }>(REFRESH_URL, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ refreshToken: rt }),
            });

            const tokens = response.data;
            setTokensStorage(
                tokens.accessToken,
                tokens.refreshToken,
                tokens.expiresAt,
                tokens.refreshExpiresAt,
            );
            return true;
        } catch (error) {
            // Очищаем токены только при невалидном refresh (400/401).
            // При сети, 429, 5xx — не чистим, даём приложению продолжить.
            if (error instanceof ApiProblem && (error.status === 400 || error.status === 401)) {
                clearTokens();
            }
            // При ApiNetworkError или других статусах — не чистим токены.
            return false;
        } finally {
            refreshPromise = null;
        }
    })();

    const result = await refreshPromise;
    refreshPromise = null;
    return result;
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
