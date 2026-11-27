/**
 * Единый модуль управления сессией.
 *
 * Отвечает за:
 * - проверку freshness access-токена;
 * - проактивный refresh за 30 секунд до истечения;
 * - обновление тела запросов, зависящих от токенов (например, logout);
 * - гарантию, что защищённые запросы идут с валидным access-токеном.
 *
 * Используется в AppLayout (logout) и других местах, где тело запроса
 * зависит от актуальных токенов.
 */

import { getAccessToken, getRefreshToken, getExpiresAt } from '@/shared/api/auth/storage';
import { refreshSession } from '@/shared/api/http';

/**
 * Запас в миллисекундах для проактивного refresh.
 * Если до истечения access-токена осталось меньше этого времени,
 * считаем токен не свежим и обновляем сессию.
 */
const FRESHNESS_MARGIN_MS = 30_000;

/**
 * Проверяет, является ли текущий access-токен свежим.
 * Токен считается несвежим, если:
 * - его нет;
 * - нет expiresAt;
 * - до истечения осталось меньше FRESHNESS_MARGIN_MS.
 */
export function isAccessTokenFresh(): boolean {
    const at = getAccessToken();
    const expiresAt = getExpiresAt();

    if (!at || !expiresAt) return false;

    try {
        const expiresMs = new Date(expiresAt).getTime();
        return Date.now() < expiresMs - FRESHNESS_MARGIN_MS;
    } catch {
        // Если не удалось распарсить expiresAt — считаем токен несвежим.
        return false;
    }
}

/**
 * Обновляет сессию через refresh, если access-токен не свежий.
 * Возвращает true, если сессия обновлена или уже была свежей.
 * Возвращает false, если refresh не удался.
 */
export async function ensureFreshSession(): Promise<boolean> {
    if (isAccessTokenFresh()) return true;

    try {
        return await refreshSession();
    } catch {
        return false;
    }
}

/**
 * Получает актуальный refresh-токен из storage.
 * Используется в logout, где тело запроса зависит от refresh-токена.
 */
export function getCurrentRefreshToken(): string | null {
    return getRefreshToken();
}
