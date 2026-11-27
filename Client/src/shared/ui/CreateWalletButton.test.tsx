import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import { renderWithProviders } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';
import { CreateWalletButton } from './CreateWalletButton';

function WalletCount({ limit }: { limit: number }) {
    const { data } = useBffWallets({ recentOperations: limit });
    return (
        <span>{data?.status === 200 && `Кошельки (${limit}): ${data.data.wallets?.length}`}</span>
    );
}

describe('CreateWalletButton', () => {
    it.each([201, 409])(
        'refreshes both wallet summaries after opening returns %s',
        async (status) => {
            let exists = false;
            const create = vi.fn(async ({ request }: { request: Request }) => {
                expect(await request.json()).toEqual({ currency: 'RUB' });
                exists = true;
                return status === 409
                    ? HttpResponse.json(
                          { status, title: 'Conflict' },
                          { status, headers: { 'Content-Type': 'application/problem+json' } },
                      )
                    : HttpResponse.json({ currency: 'RUB' }, { status });
            });
            server.use(
                http.get('*/bff/wallets', () =>
                    HttpResponse.json({ wallets: exists ? [{ currency: 'RUB' }] : [] }),
                ),
                http.post('*/api/wallets', create),
            );
            renderWithProviders(
                <>
                    <WalletCount limit={0} />
                    <WalletCount limit={5} />
                    <CreateWalletButton />
                </>,
            );
            await screen.findByText('Кошельки (0): 0');
            await screen.findByText('Кошельки (5): 0');
            expect(create).not.toHaveBeenCalled();
            await userEvent.click(screen.getByRole('button', { name: 'Создать кошелёк' }));
            expect(await screen.findByText('Кошельки (0): 1')).toBeVisible();
            expect(await screen.findByText('Кошельки (5): 1')).toBeVisible();
            expect(create).toHaveBeenCalledOnce();
            expect(screen.queryByRole('alert')).not.toBeInTheDocument();
        },
    );

    it('shows a creation error and keeps the action available', async () => {
        server.use(http.post('*/api/wallets', () => HttpResponse.error()));
        renderWithProviders(<CreateWalletButton />);
        await userEvent.click(screen.getByRole('button', { name: 'Создать кошелёк' }));
        expect(await screen.findByRole('alert')).toHaveTextContent('Сервер недоступен');
        expect(screen.getByRole('button', { name: 'Создать кошелёк' })).toBeEnabled();
    });
});
