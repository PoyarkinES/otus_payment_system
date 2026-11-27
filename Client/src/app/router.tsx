import { Navigate, type RouteObject } from 'react-router';

import { AdminRoute } from '@/app/AdminRoute';
import { ProtectedRoute } from '@/app/ProtectedRoute';
import { RouteError } from '@/app/RouteError';
import { LoginPage } from '@/pages/login/LoginPage';
import { NotFoundPage } from '@/pages/not-found/NotFoundPage';
import { AdminUserPage } from '@/pages/profile/AdminUserPage';
import { ProfilePage } from '@/pages/profile/ProfilePage';
import { RegisterPage } from '@/pages/register/RegisterPage';
import { StatusPage } from '@/pages/status/StatusPage';
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
                <AppLayout />
            </ProtectedRoute>
        ),
        errorElement: <RouteError />,
        children: [
            { index: true, element: <Navigate to="/wallets" replace /> },
            { path: 'wallets', element: <WalletsPage /> },
            { path: 'profile', element: <ProfilePage /> },
            {
                element: <AdminRoute />,
                children: [
                    { path: 'admin/users', element: <AdminUserPage /> },
                    { path: 'admin/status', element: <StatusPage /> },
                ],
            },
            { path: '*', element: <NotFoundPage /> },
        ],
    },
];
