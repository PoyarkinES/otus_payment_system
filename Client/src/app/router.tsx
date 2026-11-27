import type { RouteObject } from 'react-router';

import { ProtectedRoute } from '@/app/ProtectedRoute';
import { RouteError } from '@/app/RouteError';
import { LoginPage } from '@/pages/login/LoginPage';
import { NotFoundPage } from '@/pages/not-found/NotFoundPage';
import { AdminUserPage } from '@/pages/profile/AdminUserPage';
import { ProfilePage } from '@/pages/profile/ProfilePage';
import { RegisterPage } from '@/pages/register/RegisterPage';
import { StatusPage } from '@/pages/status/StatusPage';
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
            { index: true, element: <StatusPage /> },
            { path: 'profile', element: <ProfilePage /> },
            { path: 'admin/users', element: <AdminUserPage /> },
            { path: '*', element: <NotFoundPage /> },
        ],
    },
];
