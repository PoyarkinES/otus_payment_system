import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { RouteError } from '@/app/RouteError';
import { clearTokens } from '@/shared/api/auth/storage';
import { getBffWalletsMockHandler } from '@/shared/api/generated/endpoints/bff/bff.msw';
import { renderRoute, renderWithProviders } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

describe('router', () => {
    afterEach(() => {
        vi.restoreAllMocks();
        clearTokens();
    });

    it('opens wallets from the index and keeps the system status out of account navigation', async () => {
        renderRoute('/', true);

        expect(screen.getByText('Otus Pay')).toBeInTheDocument();
        expect(await screen.findByRole('heading', { level: 1, name: 'Кошельки' })).toBeVisible();
        expect(screen.queryByRole('link', { name: 'Статус системы' })).not.toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Выйти' })).toBeEnabled();
    });

    it('renders 404 for unknown route', () => {
        renderRoute('/no-such-page', true);

        expect(screen.getByRole('heading', { name: 'Страница не найдена' })).toBeInTheDocument();
        expect(screen.getByRole('link', { name: 'На главную' })).toHaveAttribute('href', '/');
    });

    it('marks the selected destination when switching between balances and operations', async () => {
        const user = userEvent.setup();
        server.use(getBffWalletsMockHandler({ wallets: [{ currency: 'RUB' }] }));
        renderRoute('/wallets', true);
        const navigation = within(screen.getByRole('navigation', { name: 'Основная навигация' }));
        const wallets = navigation.getByRole('link', { name: 'Кошельки' });
        const operations = navigation.getByRole('link', { name: 'Операции' });

        expect(wallets).toHaveAttribute('aria-current', 'page');
        await user.click(operations);
        expect(await screen.findByRole('region', { name: 'Последние операции' })).toHaveFocus();
        expect(operations).toHaveAttribute('aria-current', 'location');
        expect(wallets).not.toHaveAttribute('aria-current');

        await user.click(wallets);
        expect(wallets).toHaveAttribute('aria-current', 'page');
        expect(operations).not.toHaveAttribute('aria-current');
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
