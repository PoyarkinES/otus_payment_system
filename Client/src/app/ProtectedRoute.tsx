import { Navigate, useLocation } from 'react-router';
import type { ReactNode } from 'react';

import { useAuth } from '@/shared/auth/AuthContext';

/**
 * Защищённый маршрут — если пользователь не авторизован, перенаправляет на /login.
 * Запоминает запрошенный URL, чтобы после входа вернуть пользователя на нужную страницу.
 */
export function ProtectedRoute({ children }: { children: ReactNode }) {
    const { isAuthenticated } = useAuth();
    const location = useLocation();

    if (!isAuthenticated) {
        return <Navigate to="/login" state={{ from: location }} replace />;
    }

    return <>{children}</>;
}
