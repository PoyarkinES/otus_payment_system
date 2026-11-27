import { screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { RouteError } from '@/app/RouteError';
import { clearTokens } from '@/shared/api/auth/storage';
import { renderRoute, renderWithProviders } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

describe('router', () => {
    afterEach(() => {
        vi.restoreAllMocks();
        clearTokens();
    });

    it('opens the profile without fetching system health on the index route', async () => {
        const health = vi.fn(() => HttpResponse.json({ status: 'ok', services: {} }));
        server.use(http.get('*/bff/health', health));
        renderRoute('/', true);

        expect(screen.getByText('Otus Payment System')).toBeInTheDocument();
        expect(await screen.findByRole('heading', { level: 1, name: 'Профиль' })).toBeVisible();
        expect(await screen.findByText('Активен')).toBeVisible();
        expect(screen.queryByText('Статус системы')).not.toBeInTheDocument();
        expect(health).not.toHaveBeenCalled();
    });

    it('renders 404 for unknown route', () => {
        renderRoute('/no-such-page', true);

        expect(screen.getByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/');
    });

    it('renders route error fallback when page throws', () => {
        // React и роутер пишут пойманную ошибку в консоль — в тесте это ожидаемо.
        vi.spyOn(console, 'error').mockImplementation(() => {});
        vi.spyOn(console, 'warn').mockImplementation(() => {});
        function Broken(): never {
            throw new Error('boom');
        }
        const router = createMemoryRouter([
            { path: '/', element: <Broken />, errorElement: <RouteError /> },
        ]);

        renderWithProviders(<RouterProvider router={router} />);

        expect(screen.getByText('Что-то пошло не так')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Перезагрузить страницу' })).toBeInTheDocument();
    });
});
