import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

    it('opens wallets without fetching system health on the index route', async () => {
        const health = vi.fn(() => HttpResponse.json({ status: 'ok', services: {} }));
        server.use(http.get('*/bff/health', health));
        renderRoute('/', true);

        expect(screen.getByText('Otus Pay')).toBeInTheDocument();
        expect(await screen.findByRole('heading', { level: 1, name: 'Кошельки' })).toBeVisible();
        expect(screen.queryByText('Статус системы')).not.toBeInTheDocument();
        expect(health).not.toHaveBeenCalled();
    });

    it('renders 404 for unknown route', () => {
        renderRoute('/no-such-page', true);

        expect(screen.getByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/');
    });

    it('opens wallets, recent operations and the profile from the sidebar', async () => {
        const user = userEvent.setup();
        renderRoute('/', true);
        await user.click(screen.getByRole('link', { name: 'Кошельки' }));
        expect(await screen.findByRole('heading', { level: 1, name: 'Кошельки' })).toBeVisible();
        await user.click(screen.getByRole('link', { name: 'Операции' }));
        expect(screen.getByRole('link', { name: 'Операции' })).toHaveAttribute('aria-current');
        await user.click(screen.getByRole('link', { name: 'Профиль' }));
        expect(await screen.findByRole('heading', { level: 1, name: 'Профиль' })).toBeVisible();
    });

    it('starts the next account on its wallets after an explicit logout from the profile', async () => {
        const user = userEvent.setup();
        const tokens = {
            accessToken: 'access-token',
            refreshToken: 'refresh-token',
            expiresAt: new Date(Date.now() + 3600_000).toISOString(),
            refreshExpiresAt: new Date(Date.now() + 86400_000).toISOString(),
        };
        const revoke = vi.fn(() => new HttpResponse(null, { status: 204 }));
        server.use(
            http.post('*/api/identity/auth/refresh', () => HttpResponse.json(tokens)),
            http.post('*/api/identity/auth/logout', revoke),
            http.post('*/api/identity/auth/login', () => HttpResponse.json(tokens)),
        );
        renderRoute('/profile', true);
        await screen.findByText('Активен');
        await user.click(screen.getByRole('button', { name: 'Выйти' }));
        expect(await screen.findByRole('heading', { name: 'Вход' })).toBeVisible();
        expect(revoke).toHaveBeenCalledOnce();
        await user.type(screen.getByLabelText('Email'), 'next@example.test');
        await user.type(screen.getByLabelText('Пароль'), 'test-password');
        await user.click(screen.getByRole('button', { name: 'Войти' }));
        expect(await screen.findByRole('heading', { level: 1, name: 'Кошельки' })).toBeVisible();
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
