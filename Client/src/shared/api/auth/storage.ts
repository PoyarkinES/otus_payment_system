/**
 * Единый источник правды для токенов авторизации.
 *
 * Используется как из apiFetch (без React), так и из AuthContext (через useSyncExternalStore).
 * Это гарантирует, что после refresh в http.ts контекст автоматически получит новые токены.
 */

export interface AuthTokensStorage {
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: string | null;
    refreshExpiresAt: string | null;
}

let tokens: AuthTokensStorage = {
    accessToken: null,
    refreshToken: null,
    expiresAt: null,
    refreshExpiresAt: null,
};

type Subscriber = () => void;
const subscribers = new Set<Subscriber>();

/** Получает текущее состояние токенов (для useSyncExternalStore). */
export function getSnapshot(): AuthTokensStorage {
    return tokens;
}

/** Подписка на изменения токенов (для useSyncExternalStore). */
export function subscribe(subscriber: Subscriber): () => void {
    subscribers.add(subscriber);
    return () => {
        subscribers.delete(subscriber);
    };
}

/** Сохраняет токены после login или refresh. */
export function setTokensStorage(
    at: string,
    rt: string,
    expiresAt: string,
    refreshExpiresAt: string,
): void {
    tokens = {
        accessToken: at,
        refreshToken: rt,
        expiresAt,
        refreshExpiresAt,
    };
    subscribers.forEach((s) => s());
}

/** Очищает токены при logout или неудачном refresh. */
export function clearTokens(): void {
    tokens = {
        accessToken: null,
        refreshToken: null,
        expiresAt: null,
        refreshExpiresAt: null,
    };
    subscribers.forEach((s) => s());
}

/** Геттеры для apiFetch (обратная совместимость). */
export function getAccessToken(): string | null {
    return tokens.accessToken;
}

export function getRefreshToken(): string | null {
    return tokens.refreshToken;
}

export function getExpiresAt(): string | null {
    return tokens.expiresAt;
}

/** Обратная совместимость — для тестов (render.tsx). Ставит токены без expiresAt. */
export function setTokens(at: string, rt: string): void {
    setTokensStorage(at, rt, '', '');
}
