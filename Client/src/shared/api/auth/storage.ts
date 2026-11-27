/**
 * Единый источник правды для токенов авторизации.
 *
 * Используется как из apiFetch (без React), так и из AuthContext (через useSyncExternalStore).
 * Это гарантирует, что после refresh в http.ts контекст автоматически получит новые токены.
 */

import type { AuthTokens } from '@/shared/api/generated/identity/model';

export interface AuthTokensStorage {
    /** Меняется при входе и завершении сессии, сохраняется при ротации токенов. */
    sessionId: number;
    accessToken: string | null;
    refreshToken: string | null;
    expiresAt: string | null;
    refreshExpiresAt: string | null;
}

const emptyTokens = {
    accessToken: null,
    refreshToken: null,
    expiresAt: null,
    refreshExpiresAt: null,
};

let tokens: AuthTokensStorage = { ...emptyTokens, sessionId: 0 };

type Subscriber = (sessionChanged: boolean) => void;
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

function publish(next: AuthTokensStorage): void {
    const sessionChanged = tokens.sessionId !== next.sessionId;
    tokens = next;
    subscribers.forEach((subscriber) => subscriber(sessionChanged));
}

/** Начинает новую сессию после входа. */
export function setTokensStorage(
    at: string,
    rt: string,
    expiresAt: string,
    refreshExpiresAt: string,
): void {
    publish({
        sessionId: tokens.sessionId + 1,
        accessToken: at,
        refreshToken: rt,
        expiresAt,
        refreshExpiresAt,
    });
}

/** Сохраняет результат ротации в рамках текущей сессии. */
export function rotateTokens(next: AuthTokens): void {
    publish({ ...next, sessionId: tokens.sessionId });
}

/** Завершает сессию при выходе или отклонении токенов. */
export function clearTokens(): void {
    publish({ ...emptyTokens, sessionId: tokens.sessionId + 1 });
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
