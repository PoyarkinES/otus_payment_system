import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { RouteError } from '@/app/RouteError';
import { clearTokens } from '@/shared/api/auth/storage';
import { renderRoute, renderWithProviders } from '@/shared/testing/render';

describe('router', () => {
    afterEach(() => {
        vi.restoreAllMocks();
        clearTokens();
    });

    it('renders layout title on index route', () => {
        renderRoute('/', true);

        expect(screen.getByText('Otus Pay')).toBeInTheDocument();
        expect(
            screen.getByRole('heading', { level: 1, name: 'Статус системы' }),
        ).toBeInTheDocument();
    });

    it('renders 404 for unknown route', () => {
        renderRoute('/no-such-page', true);

        expect(screen.getByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/');
    });

    it('opens wallets and recent operations from the sidebar', async () => {
        const user = userEvent.setup();
        renderRoute('/', true);
        await user.click(screen.getByRole('link', { name: 'Кошельки' }));
        expect(await screen.findByRole('heading', { level: 1, name: 'Кошельки' })).toBeVisible();
        await user.click(screen.getByRole('link', { name: 'Операции' }));
        expect(screen.getByRole('link', { name: 'Операции' })).toHaveAttribute('aria-current');
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
