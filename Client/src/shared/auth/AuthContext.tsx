import {
    createContext,
    useContext,
    useCallback,
    useSyncExternalStore,
    type ReactNode,
} from 'react';
import type { AuthTokens } from '@/shared/api/generated/identity/model';
import {
    getSnapshot,
    subscribe,
    setTokensStorage,
    clearTokens as clearStorageTokens,
} from '@/shared/api/auth/storage';

/**
 * Состояние авторизации: токены и данные пользователя.
 * Хранится только в памяти (memory) — никогда в localStorage, чтобы пароль и токены
 * не попадали в хранилище браузера.
 *
 * AuthContext использует useSyncExternalStore для подписки на storage —
 * это гарантирует, что после refresh в http.ts контекст автоматически получит новые токены.
 */
interface AuthState {
    /** JWT access-токен для авторизации запросов. */
    accessToken: string | null;
    /** Refresh-токен для обновления сессии. */
    refreshToken: string | null;
    /** Когда истекает access-токен (ISO-строка из сервера). */
    expiresAt: string | null;
    /** Когда истекает refresh-токен (ISO-строка из сервера). */
    refreshExpiresAt: string | null;
}

interface AuthContextValue extends AuthState {
    /** Сохранить пару токенов после login/refresh. */
    setTokens: (tokens: AuthTokens) => void;
    /** Очистить всё — при logout или неудачном refresh. */
    clearTokens: () => void;
    /** Есть ли активная сессия (есть ли refresh-токен). */
    isAuthenticated: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Провайдер Auth контекста — оборачивает приложение в `providers.tsx`.
 * Использует useSyncExternalStore для синхронизации с единым storage.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
    const storage = useSyncExternalStore(subscribe, getSnapshot);

    const setTokens = useCallback((tokens: AuthTokens) => {
        setTokensStorage(
            tokens.accessToken,
            tokens.refreshToken,
            tokens.expiresAt,
            tokens.refreshExpiresAt,
        );
    }, []);

    const clearTokens = useCallback(() => {
        clearStorageTokens();
    }, []);

    // isAuthenticated основан на наличии refresh-токена, а не на сроке access-токена.
    // Access-токен может быть просрочен, но refresh его обновит.
    // Если refresh-токен тоже истёк — сессия завершена.
    const isAuthenticated = storage.refreshToken !== null;

    return (
        <AuthContext.Provider
            value={{
                accessToken: storage.accessToken,
                refreshToken: storage.refreshToken,
                expiresAt: storage.expiresAt,
                refreshExpiresAt: storage.refreshExpiresAt,
                setTokens,
                clearTokens,
                isAuthenticated,
            }}
        >
            {children}
        </AuthContext.Provider>
    );
}

/**
 * Хук для доступа к Auth контексту. Бросает ошибку, если вызван вне AuthProvider.
 *
 * @see {@link https://react.dev/reference/react/useContext#what-to-do-when-the-context-value-is-null}
 */
// eslint-disable-next-line react-refresh/only-export-components -- хук нужен страницам; провайдер в providers.tsx
export function useAuth(): AuthContextValue {
    const ctx = useContext(AuthContext);
    if (ctx === null) {
        throw new Error('useAuth must be used within AuthProvider');
    }
    return ctx;
}
