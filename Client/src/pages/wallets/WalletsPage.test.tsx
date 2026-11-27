import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { MemoryRouter } from 'react-router';
import { describe, expect, it, vi } from 'vitest';

import { AmountVisibilityProvider } from './AmountVisibility';
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
            <AmountVisibilityProvider>
                <WalletsPage />
            </AmountVisibilityProvider>
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
        expect(card.queryByText('RUB')).not.toBeInTheDocument();
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

    it('offers the privacy control without a wallet creation action', async () => {
        summary([rub]);
        openPage();
        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        expect(screen.getByRole('button', { name: 'Скрыть суммы' })).toBeVisible();
        expect(screen.queryByRole('button', { name: /создать кошелёк/i })).not.toBeInTheDocument();
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

    it('opens one RUB wallet on demand and refreshes its balance and recent history', async () => {
        const zeroWallet = { ...rub, available: 0, held: 0, total: 0, recentOperations: [] };
        const create = vi.fn(() => HttpResponse.json(zeroWallet, { status: 201 }));
        server.use(
            http.post(createUrl, create),
            http.get(summaryUrl, () =>
                HttpResponse.json({ wallets: create.mock.calls.length ? [zeroWallet] : [] }),
            ),
        );
        openPage();

        expect(await screen.findByRole('heading', { name: 'Кошелька пока нет' })).toBeVisible();
        expect(screen.getByText(/один рублёвый кошелёк с нулевым балансом/i)).toBeVisible();
        expect(
            screen.queryByRole('region', { name: 'Последние операции' }),
        ).not.toBeInTheDocument();
        const action = screen.getByRole('button', { name: 'Создать кошелёк' });
        expect(create).not.toHaveBeenCalled();
        await userEvent.click(action);

        const wallet = within(await screen.findByRole('article', { name: 'Рублёвый кошелёк' }));
        expect(wallet.getByText('Доступно').nextElementSibling).toHaveTextContent('0,00 ₽');
        expect(wallet.getByText('В резерве').nextElementSibling).toHaveTextContent('0,00 ₽');
        expect(screen.getByRole('region', { name: 'Последние операции' })).toHaveTextContent(
            'Операций пока нет',
        );
        expect(screen.queryByRole('button', { name: 'Создать кошелёк' })).not.toBeInTheDocument();
        expect(create).toHaveBeenCalledTimes(1);
    });

    it('recovers after an unsuccessful list request', async () => {
        server.use(http.get(summaryUrl, () => HttpResponse.error()));
        openPage();
        expect(await screen.findByRole('alert')).toHaveTextContent('Сервер недоступен');

        summary([]);
        await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
        expect(await screen.findByText('Кошелька пока нет')).toBeVisible();
        expect(screen.getByRole('button', { name: 'Создать кошелёк' })).toBeEnabled();
    });

    it('links to the complete history and displays at most five recent operations', async () => {
        const recentOperations = Array.from({ length: 8 }, (_, i) => ({
            ...rub.recentOperations[0],
            id: String(i),
            createdAt: new Date(Date.UTC(2026, 10, i + 1)).toISOString(),
        }));
        summary([{ ...rub, recentOperations }]);
        openPage();
        const history = within(await screen.findByRole('region', { name: 'Последние операции' }));
        const rows = history.getAllByRole('listitem');
        expect(rows).toHaveLength(5);
        expect(rows[0]).toHaveTextContent('8 ноября');
        expect(rows[4]).toHaveTextContent('4 ноября');
        const link = history.getByRole('link', { name: 'Посмотреть всю историю' });
        expect(link).toHaveAttribute('href', '/operations');
        expect(
            rows[4].compareDocumentPosition(link) & Node.DOCUMENT_POSITION_FOLLOWING,
        ).toBeTruthy();
    });
});
