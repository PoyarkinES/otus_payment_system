import { fireEvent, screen, within } from '@testing-library/react';
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
const walletUrl = `${createUrl}/40d4819d-6a79-4e48-aceb-8d7121cb4f80`;
const recipientId = 'c78b9c9b-ea07-4be4-a6a8-f6e81ea4d4a2';
const holdId = '2b94ff18-3ec6-475f-8473-894dd6b40be0';
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

    it('deposits funds with an idempotency key and refreshes the wallet balance', async () => {
        const user = userEvent.setup();
        const updated = {
            ...rub,
            available: 1234668.39,
            total: 1235168.64,
            recentOperations: [
                {
                    id: 'op3',
                    type: 'TopUp',
                    amount: 100.5,
                    currency: 'RUB',
                    createdAt: '2026-11-27T12:00:00Z',
                },
                ...rub.recentOperations,
            ],
        };
        let deposits = 0;
        let body: unknown;
        let idempotencyKey: string | null = null;
        server.use(
            http.get(summaryUrl, () =>
                HttpResponse.json({ wallets: [deposits ? updated : rub], totals: [] }),
            ),
            http.post(`${walletUrl}/deposit`, async ({ request }) => {
                body = await request.json();
                idempotencyKey = request.headers.get('Idempotency-Key');
                deposits++;
                return HttpResponse.json(updated);
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '100.5');
        await user.click(screen.getByRole('button', { name: 'Пополнить' }));

        expect(await screen.findByText('1 234 668,39 ₽')).toBeVisible();
        expect(body).toEqual({ amount: 100.5 });
        expect(idempotencyKey).toMatch(
            /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
        );
        expect(deposits).toBe(1);
    });

    it('reuses the idempotency key when retrying a deposit after a lost response', async () => {
        const user = userEvent.setup();
        const keys: string[] = [];
        let attempts = 0;
        summary([rub]);
        server.use(
            http.post(`${walletUrl}/deposit`, ({ request }) => {
                keys.push(request.headers.get('Idempotency-Key') ?? '');
                attempts++;
                return attempts === 1 ? HttpResponse.error() : HttpResponse.json(rub);
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '20');
        await user.click(screen.getByRole('button', { name: 'Пополнить' }));
        await screen.findByRole('alert');
        await user.click(screen.getByRole('button', { name: 'Пополнить' }));

        expect(await screen.findByText('Кошелёк пополнен.')).toBeVisible();
        expect(keys).toHaveLength(2);
        expect(keys[0]).toBeTruthy();
        expect(keys[1]).toBe(keys[0]);
    });

    it('withdraws funds and shows a server error when the balance is insufficient', async () => {
        const user = userEvent.setup();
        let body: unknown;
        summary([rub]);
        server.use(
            http.post(`${walletUrl}/withdraw`, async ({ request }) => {
                body = await request.json();
                return HttpResponse.json(
                    {
                        title: 'Недостаточно средств',
                        detail: 'Недостаточно средств для списания.',
                        status: 409,
                    },
                    { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
                );
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '5000000');
        await user.click(screen.getByRole('button', { name: 'Списать' }));

        expect(await screen.findByRole('alert')).toHaveTextContent(
            'Недостаточно средств для списания.',
        );
        expect(body).toEqual({ amount: 5000000 });
    });

    it('withdraws funds and refreshes the wallet balance', async () => {
        const user = userEvent.setup();
        const updated = { ...rub, available: 1234517.64, total: 1235017.89 };
        let withdrawals = 0;
        let body: unknown;
        let idempotencyKey: string | null = null;
        server.use(
            http.get(summaryUrl, () =>
                HttpResponse.json({ wallets: [withdrawals ? updated : rub], totals: [] }),
            ),
            http.post(`${walletUrl}/withdraw`, async ({ request }) => {
                body = await request.json();
                idempotencyKey = request.headers.get('Idempotency-Key');
                withdrawals++;
                return HttpResponse.json(updated);
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '50.25');
        await user.click(screen.getByRole('button', { name: 'Списать' }));

        expect(await screen.findByText('1 234 517,64 ₽')).toBeVisible();
        expect(body).toEqual({ amount: 50.25 });
        expect(idempotencyKey).toMatch(
            /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
        );
        expect(withdrawals).toBe(1);
    });

    it('validates the amount before sending a money operation', async () => {
        let requests = 0;
        summary([rub]);
        server.use(
            http.post(`${walletUrl}/deposit`, () => {
                requests++;
                return HttpResponse.json(rub);
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        const amount = screen.getByRole('spinbutton', { name: 'Сумма операции' });
        fireEvent.change(amount, { target: { value: '1.12345' } });
        expect(screen.getByRole('button', { name: 'Пополнить' })).toBeDisabled();
        expect(
            screen.getByText('Введите положительную сумму не более чем с 4 знаками после запятой.'),
        ).toBeVisible();

        fireEvent.change(amount, { target: { value: '0' } });
        expect(screen.getByRole('button', { name: 'Пополнить' })).toBeDisabled();
        expect(requests).toBe(0);
    });

    it('rejects an invalid recipient UUID before creating a hold', async () => {
        const user = userEvent.setup();
        let holdRequests = 0;
        summary([rub]);
        server.use(
            http.post(`${walletUrl}/holds`, () => {
                holdRequests++;
                return HttpResponse.json({ id: holdId });
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '50');
        await user.type(screen.getByRole('textbox', { name: 'UUID получателя' }), 'not-a-guid');
        await user.click(screen.getByRole('button', { name: 'Зарезервировать' }));

        expect(screen.getByText('Введите UUID получателя в формате UUID.')).toBeVisible();
        expect(holdRequests).toBe(0);
    });

    it('disables money and hold actions for a frozen wallet', async () => {
        summary([{ ...rub, status: 'Frozen' }]);
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });

        expect(screen.getByRole('button', { name: 'Пополнить' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Списать' })).toBeDisabled();
        expect(screen.getByRole('button', { name: 'Зарезервировать' })).toBeDisabled();
        expect(screen.getByText('Операции доступны только для активного кошелька.')).toBeVisible();
    });

    it('creates a hold and lets the user confirm it', async () => {
        const user = userEvent.setup();
        let body: unknown;
        let confirmed = 0;
        summary([rub]);
        server.use(
            http.post(`${walletUrl}/holds`, async ({ request }) => {
                body = await request.json();
                return HttpResponse.json({
                    id: holdId,
                    walletId: rub.id,
                    transferId: (body as { transferId: string }).transferId,
                    userId: 'owner-id',
                    recipientUserId: recipientId,
                    amount: 50,
                    currency: 'RUB',
                    status: 'Active',
                    createdAt: '2026-11-27T12:00:00Z',
                    updatedAt: '2026-11-27T12:00:00Z',
                });
            }),
            http.post(`${createUrl}/holds/${holdId}/confirm`, () => {
                confirmed++;
                return new HttpResponse(null, { status: 204 });
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '50');
        await user.type(screen.getByRole('textbox', { name: 'UUID получателя' }), recipientId);
        await user.click(screen.getByRole('button', { name: 'Зарезервировать' }));

        expect(await screen.findByText(/резерв создан/i)).toBeVisible();
        expect(body).toMatchObject({ amount: 50, recipientUserId: recipientId });
        expect((body as { transferId: string }).transferId).toMatch(
            /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i,
        );
        await user.click(screen.getByRole('button', { name: 'Подтвердить перевод' }));
        expect(await screen.findByText(/резерв подтверждён/i)).toBeVisible();
        expect(confirmed).toBe(1);
    });

    it('lets the user release a hold created in the current page session', async () => {
        const user = userEvent.setup();
        let released = 0;
        summary([rub]);
        server.use(
            http.post(`${walletUrl}/holds`, () =>
                HttpResponse.json({
                    id: holdId,
                    walletId: rub.id,
                    transferId: 'b17d0e97-b967-43df-bca0-68810faf45ab',
                    userId: 'owner-id',
                    recipientUserId: recipientId,
                    amount: 50,
                    currency: 'RUB',
                    status: 'Active',
                    createdAt: '2026-11-27T12:00:00Z',
                    updatedAt: '2026-11-27T12:00:00Z',
                }),
            ),
            http.post(`${createUrl}/holds/${holdId}/release`, () => {
                released++;
                return new HttpResponse(null, { status: 204 });
            }),
        );
        openPage();

        await screen.findByRole('article', { name: 'Рублёвый кошелёк' });
        await user.type(screen.getByRole('spinbutton', { name: 'Сумма операции' }), '50');
        await user.type(screen.getByRole('textbox', { name: 'UUID получателя' }), recipientId);
        await user.click(screen.getByRole('button', { name: 'Зарезервировать' }));
        await screen.findByRole('button', { name: 'Освободить резерв' });
        await user.click(screen.getByRole('button', { name: 'Освободить резерв' }));

        expect(await screen.findByText(/резерв освобождён/i)).toBeVisible();
        expect(released).toBe(1);
        expect(
            screen.queryByRole('button', { name: 'Подтвердить перевод' }),
        ).not.toBeInTheDocument();
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
