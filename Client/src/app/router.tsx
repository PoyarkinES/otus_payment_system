import type { RouteObject } from 'react-router';

import { RouteError } from '@/app/RouteError';
import { NotFoundPage } from '@/pages/not-found/NotFoundPage';
import { StatusPage } from '@/pages/status/StatusPage';
import { AppLayout } from '@/shared/ui/AppLayout';

/** Все маршруты приложения. Новая страница = новый элемент в `children`. */
export const routes: RouteObject[] = [
    {
        path: '/',
        element: <AppLayout />,
        errorElement: <RouteError />,
        children: [
            { index: true, element: <StatusPage /> },
            { path: '*', element: <NotFoundPage /> },
        ],
    },
];
