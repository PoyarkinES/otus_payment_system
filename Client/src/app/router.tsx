import { Navigate, type RouteObject } from 'react-router';

import { ProtectedRoute } from '@/app/ProtectedRoute';
import { RouteError } from '@/app/RouteError';
import { LoginPage } from '@/pages/login/LoginPage';
import { NotFoundPage } from '@/pages/not-found/NotFoundPage';
import { RegisterPage } from '@/pages/register/RegisterPage';
import { AmountVisibilityProvider } from '@/pages/wallets/AmountVisibility';
import { WalletsPage } from '@/pages/wallets/WalletsPage';
import { AppLayout } from '@/shared/ui/AppLayout';

/** Все маршруты приложения. Новая страница = новый элемент в `children`. */
export const routes: RouteObject[] = [
    {
        path: '/login',
        element: <LoginPage />,
        errorElement: <RouteError />,
    },
    {
        path: '/register',
        element: <RegisterPage />,
        errorElement: <RouteError />,
    },
    {
        path: '/',
        element: (
            <ProtectedRoute>
                <AmountVisibilityProvider>
                    <AppLayout />
                </AmountVisibilityProvider>
            </ProtectedRoute>
        ),
        errorElement: <RouteError />,
        children: [
            { index: true, element: <Navigate to="/wallets" replace /> },
            { path: 'wallets', element: <WalletsPage /> },
            { path: '*', element: <NotFoundPage /> },
        ],
    },
];
