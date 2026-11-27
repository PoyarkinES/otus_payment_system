import { onlineManager } from '@tanstack/react-query';
import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';

import { WalletsPage } from './WalletsPage';
import { renderWithProviders } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

const summaryUrl = 'http://localhost:8080/bff/wallets';
const createUrl = 'http://localhost:8080/api/wallets';
const rub = {
    id: '40d4819d-6a79-4e48-aceb-8d7121cb4f80',
    currency: 'RUB',
    status: 'Active',
    available: 1234567.89,
    held: 500.25,
    total: 1235068.14,
    recentOperations: [
        {
            id: 'op2',
            type: 'TransferOut',
            amount: 2500,
            currency: 'RUB',
            createdAt: '2026-11-27T11:50:00Z',
        },
        {
            id: 'op1',
            type: 'TopUp',
            amount: 12000,
            currency: 'RUB',
            createdAt: '2026-11-26T11:50:00Z',
        },
    ],
};

function summary(wallets: object[]) {
    server.use(http.get(summaryUrl, () => HttpResponse.json({ wallets, totals: [] })));
}

function openPage(path = '/wallets') {
    return renderWithProviders(
        <MemoryRouter initialEntries={[path]}>
            <WalletsPage />
        </MemoryRouter>,
    );
}

describe('WalletsPage', () => {
    it('labels available and reserved amounts separately and formats each wallet in its own currency', async () => {
        summary([rub, { id: 'other', currency: 'USD', available: 0, held: 12.25 }]);
        openPage();

        expect(screen.getByRole('status')).toHaveTextContent('Загружаем кошельки');
        const card = within(await screen.findByRole('article', { name: 'Рублёвый кошелёк' }));
        expect(card.getByText('Доступно').nextElementSibling).toHaveTextContent('1 234 567,89 ₽');
        expect(card.getByText('В резерве').nextElementSibling).toHaveTextContent('500,25 ₽');
        expect(card.getByText('RUB')).toBeVisible();
        const other = within(screen.getByRole('article', { name: /кошелёк USD/i }));
        expect(other.getByText('Доступно').nextElementSibling).toHaveTextContent('0,00');
    });

    it('hides wallet and ledger amounts and restores signed credits and debits', async () => {
        summary([rub]);
        openPage();

        const history = within(await screen.findByRole('region', { name: 'Последние операции' }));
        const credit = history.getByText('Пополнение').closest('li')!;
        expect(credit).toHaveTextContent('+12 000,00 ₽');
        expect(credit).toHaveTextContent('26 ноября');
        expect(history.getByText('Исходящий перевод').closest('li')).toHaveTextContent(
            '−2 500,00 ₽',
        );
        await userEvent.click(screen.getByRole('button', { name: 'Скрыть суммы' }));
        for (const amount of [/1 234 567,89/, /500,25/, /12 000,00/, /2 500,00/]) {
            expect(screen.queryByText(amount)).not.toBeInTheDocument();
        }
        await userEvent.click(screen.getByRole('button', { name: 'Показать суммы' }));
        expect(credit).toHaveTextContent('+12 000,00 ₽');
        expect(screen.getByText(/1 234 567,89/)).toBeVisible();
    });

    it('explains why another RUB wallet cannot be created when one is already loaded', async () => {
        summary([rub]);
        openPage();
        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });

        const create = screen.getByRole('button', { name: 'Создать кошелёк' });
        expect(create).toBeDisabled();
        expect(create).toHaveAccessibleDescription('Рублёвый кошелёк уже существует.');
    });

    it('keeps unavailable amounts distinct from zero', async () => {
        summary([{ id: 'rub', currency: 'RUB', available: null, recentOperations: null }]);
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        expect(screen.getByText('Доступно').nextElementSibling).toHaveTextContent('—');
        expect(screen.getByText('В резерве').nextElementSibling).toHaveTextContent('—');
    });

    it('preserves the precision accepted by the wallet ledger for sub-kopeck balances', async () => {
        summary([{ ...rub, available: 0.0001, held: 12.3456 }]);
        openPage();
        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });

        expect(screen.getByText('Доступно').nextElementSibling).toHaveTextContent('0,0001 ₽');
        expect(screen.getByText('В резерве').nextElementSibling).toHaveTextContent('12,3456 ₽');
    });

    it('prevents duplicate submission if a reconnect refresh finds a wallet with the dialog open', async () => {
        summary([]);
        openPage();
        await screen.findByText('Кошельков пока нет');
        await userEvent.click(screen.getByRole('button', { name: 'Создать кошелёк' }));
        const dialog = within(screen.getByRole('dialog'));
        summary([rub]);
        act(() => {
            onlineManager.setOnline(false);
            onlineManager.setOnline(true);
        });

        expect(await dialog.findByText('Рублёвый кошелёк уже существует.')).toBeVisible();
        expect(dialog.getByRole('button', { name: 'Создать' })).toBeDisabled();
    });

    it('recovers after an unsuccessful list request', async () => {
        server.use(http.get(summaryUrl, () => HttpResponse.error()));
        openPage();
        expect(await screen.findByRole('alert')).toHaveTextContent('Сервер недоступен');

        summary([]);
        await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
        expect(await screen.findByText('Кошельков пока нет')).toBeVisible();
        expect(screen.getByRole('region', { name: 'Последние операции' })).toHaveAttribute(
            'id',
            'operations',
        );
    });

    it('focuses the operations section when opening its link before the summary loads', async () => {
        summary([rub]);
        openPage('/wallets#operations');

        expect(await screen.findByRole('region', { name: 'Последние операции' })).toHaveFocus();
    });

    it('keeps the form available after a creation failure and shows the support code', async () => {
        summary([]);
        server.use(
            http.post(createUrl, () =>
                HttpResponse.json(
                    { status: 503, detail: 'Попробуйте позже.', traceId: 'wallet-create-42' },
                    { status: 503, headers: { 'Content-Type': 'application/problem+json' } },
                ),
            ),
        );
        openPage();
        await screen.findByText('Кошельков пока нет');
        await userEvent.click(screen.getByRole('button', { name: 'Создать кошелёк' }));
        const dialog = within(screen.getByRole('dialog'));
        await userEvent.click(dialog.getByRole('button', { name: 'Создать' }));

        expect(await dialog.findByRole('alert')).toHaveTextContent('Сервис временно недоступен');
        expect(dialog.getByRole('alert')).toHaveTextContent('wallet-create-42');
        expect(dialog.getByRole('button', { name: 'Создать' })).toBeEnabled();
    });

    it.each([201, 409])('refreshes the wallet list after creation returns %s', async (status) => {
        let exists = false;
        server.use(
            http.get(summaryUrl, () =>
                HttpResponse.json({ wallets: exists ? [rub] : [], totals: [] }),
            ),
            http.post(createUrl, async ({ request }) => {
                if (
                    request.headers.get('Content-Type') !== 'application/json' ||
                    ((await request.json()) as { currency?: string }).currency !== 'RUB'
                ) {
                    return new HttpResponse(null, { status: 400 });
                }
                exists = true;
                return status === 409
                    ? HttpResponse.json(
                          { title: 'Conflict', status },
                          { status, headers: { 'Content-Type': 'application/problem+json' } },
                      )
                    : HttpResponse.json({ id: rub.id, currency: 'RUB' }, { status });
            }),
        );
        openPage();
        await screen.findByText('Кошельков пока нет');
        await userEvent.click(screen.getByRole('button', { name: 'Создать кошелёк' }));
        const dialog = within(screen.getByRole('dialog', { name: 'Создать кошелёк' }));
        expect(dialog.getByText(/RUB/)).toBeVisible();
        await userEvent.click(dialog.getByRole('button', { name: 'Создать' }));
        expect(await screen.findByRole('article', { name: 'Рублёвый кошелёк' })).toBeVisible();
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
        if (status === 409) {
            expect(screen.getByRole('alert')).toHaveTextContent('Рублёвый кошелёк уже существует');
        }
    });
});
