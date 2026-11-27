import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { clearTokens } from '@/shared/api/auth/storage';
import { handlers } from '@/shared/testing/handlers';
import { renderRoute } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

const createWallet = { name: 'Создать кошелёк' };

afterEach(clearTokens);

describe('wallet in the profile', () => {
    it('opens a RUB wallet on request and shows the current owner’s wallet after switching accounts', async () => {
        server.use(...handlers);
        const user = userEvent.setup();
        renderRoute('/login');
        async function signIn(email: string) {
            await user.type(screen.getByLabelText('Email'), email);
            await user.type(screen.getByLabelText('Пароль'), 'test-password');
            await user.click(screen.getByRole('button', { name: 'Войти' }));
            await user.click(await screen.findByRole('link', { name: 'Профиль' }));
        }

        await signIn('new-wallet@example.test');
        await user.click(await screen.findByRole('button', createWallet));
        const wallet = await screen.findByText(/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i);
        expect(wallet).toBeVisible();
        expect(screen.getByText('RUB')).toBeVisible();
        expect(screen.queryByRole('button', createWallet)).not.toBeInTheDocument();

        const id = wallet.textContent!;
        await user.click(screen.getByRole('button', { name: 'Выйти' }));
        await screen.findByRole('heading', { name: 'Вход' });
        await signIn('demo@otus.local');
        expect(await screen.findByText('00000000-0000-4000-8000-000000000002')).toBeVisible();
        expect(screen.queryByText(id)).not.toBeInTheDocument();
        expect(screen.queryByRole('button', createWallet)).not.toBeInTheDocument();
    });

    it('waits for a successful wallet lookup before offering creation and skips operations', async () => {
        const requestedLimits: (string | null)[] = [];
        server.use(
            http.get(
                '*/bff/wallets',
                ({ request }) => {
                    requestedLimits.push(new URL(request.url).searchParams.get('recentOperations'));
                    return new HttpResponse(null, { status: 503 });
                },
                { once: true },
            ),
        );
        renderRoute('/profile', true);

        const alert = await screen.findByRole('alert');
        expect(alert).toHaveTextContent('Сервис временно недоступен');
        expect(screen.queryByRole('button', createWallet)).not.toBeInTheDocument();
        await userEvent.click(within(alert).getByRole('button', { name: 'Повторить' }));
        expect(await screen.findByRole('button', createWallet)).toBeEnabled();
        expect(requestedLimits).toEqual(['0']);
    });

    it('skips wallet lookup and creation for a blocked account', async () => {
        const wallets = vi.fn(() => HttpResponse.json({ wallets: [], totals: [] }));
        server.use(
            http.get('*/api/identity/users/me', () => HttpResponse.json({ status: 'Blocked' })),
            http.get('*/bff/wallets', wallets),
        );
        renderRoute('/profile', true);

        expect(await screen.findByText('Заблокирован')).toBeVisible();
        expect(wallets).not.toHaveBeenCalled();
        expect(screen.queryByRole('button', createWallet)).not.toBeInTheDocument();
    });
});
