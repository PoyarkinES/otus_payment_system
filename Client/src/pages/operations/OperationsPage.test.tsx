import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { VirtuosoMockContext } from 'react-virtuoso';
import { describe, expect, it } from 'vitest';

import { AmountVisibilityProvider } from '@/pages/wallets/AmountVisibility';
import { renderWithProviders } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';
import { OperationsPage } from './OperationsPage';

const summaryUrl = 'http://localhost:8080/bff/wallets';
const rub = { id: '426b813a-6764-4540-962e-999346fe6b70', currency: 'RUB' };
const historyUrl = `http://localhost:8080/api/wallets/${rub.id}/operations`;

function serveWallets(wallets = [rub]) {
    server.use(
        http.get(summaryUrl, ({ request }) =>
            new URL(request.url).searchParams.get('recentOperations') === '0'
                ? HttpResponse.json({ wallets, totals: [] })
                : new HttpResponse(null, { status: 400 }),
        ),
    );
}

function serveHistory(
    count: number,
    { failPageTwoOnce = false, prependBeforePageTwo = false } = {},
) {
    const requests: number[] = [];
    const records = Array.from({ length: count }, (_, index) => ({
        id: `op-${index}`,
        referenceId: null,
        type: 'TopUp',
        amount: count - index,
        currency: 'RUB',
        balanceBefore: 0,
        balanceAfter: count - index,
        description: null,
        createdAt: new Date(Date.UTC(2026, 10, 27, 12, 0, -index)).toISOString(),
    }));
    server.use(
        http.get(historyUrl, ({ request }) => {
            const params = new URL(request.url).searchParams;
            const page = Number(params.get('page'));
            const pageSize = Number(params.get('pageSize'));
            requests.push(page);
            if (page === 2 && failPageTwoOnce) {
                failPageTwoOnce = false;
                return new HttpResponse(null, { status: 503 });
            }
            if (page < 1 || pageSize !== 20) {
                return new HttpResponse(null, { status: 400 });
            }
            if (page === 2 && prependBeforePageTwo) {
                records.unshift({ ...records[0], id: 'new-operation' });
                prependBeforePageTwo = false;
            }
            return HttpResponse.json({
                items: records.slice((page - 1) * pageSize, page * pageSize),
                page,
                pageSize,
                totalCount: records.length,
                totalPages: Math.ceil(records.length / pageSize),
            });
        }),
    );
    return requests;
}

function openPage(viewportHeight = 160) {
    const router = createMemoryRouter(
        [
            {
                path: '/operations',
                element: (
                    <AmountVisibilityProvider>
                        <VirtuosoMockContext.Provider value={{ itemHeight: 64, viewportHeight }}>
                            <OperationsPage />
                        </VirtuosoMockContext.Provider>
                    </AmountVisibilityProvider>
                ),
            },
        ],
        { initialEntries: ['/operations'] },
    );
    return renderWithProviders(<RouterProvider router={router} />);
}

describe('OperationsPage', () => {
    it('shows the newest records in server order and only renders the viewport and its buffer', async () => {
        serveWallets([{ id: 'other', currency: 'USD' }, rub]);
        const requests = serveHistory(68);
        openPage();
        const first = await screen.findByText('+68,00 ₽');
        const rows = screen.getAllByRole('listitem');
        expect(rows[0]).toContainElement(first);
        expect(rows[1]).toHaveTextContent('+67,00 ₽');
        expect(rows.length).toBeLessThan(20);
        expect(rows[0]).toHaveAttribute('aria-setsize', '68');
        expect(screen.getByRole('combobox', { name: 'Тип операции' })).toBeVisible();
        expect(requests).toEqual([1]);
    });

    it('sends type and UTC date filters and can clear them', async () => {
        const user = userEvent.setup();
        serveWallets();
        const requests: string[] = [];
        const records = [
            {
                id: 'top-up',
                referenceId: null,
                type: 'TopUp',
                amount: 30,
                currency: 'RUB',
                balanceBefore: 0,
                balanceAfter: 30,
                description: null,
                createdAt: '2026-11-27T12:00:00Z',
            },
            {
                id: 'recent-withdrawal',
                referenceId: null,
                type: 'Withdrawal',
                amount: 20,
                currency: 'RUB',
                balanceBefore: 30,
                balanceAfter: 10,
                description: null,
                createdAt: '2026-11-27T11:00:00Z',
            },
            {
                id: 'old-withdrawal',
                referenceId: null,
                type: 'Withdrawal',
                amount: 5,
                currency: 'RUB',
                balanceBefore: 35,
                balanceAfter: 30,
                description: null,
                createdAt: '2026-11-25T11:00:00Z',
            },
        ];
        server.use(
            http.get(historyUrl, ({ request }) => {
                const params = new URL(request.url).searchParams;
                requests.push(params.toString());
                const type = params.get('type');
                const from = params.get('from');
                const to = params.get('to');
                const filtered = records.filter((operation) => {
                    const date = operation.createdAt.slice(0, 10);
                    return (
                        (!type || operation.type === type) &&
                        (!from || date >= from) &&
                        (!to || date <= to)
                    );
                });
                const page = Number(params.get('page'));
                const pageSize = Number(params.get('pageSize'));
                return HttpResponse.json({
                    items: filtered.slice((page - 1) * pageSize, page * pageSize),
                    page,
                    pageSize,
                    totalCount: filtered.length,
                    totalPages: Math.ceil(filtered.length / pageSize),
                });
            }),
        );
        openPage(2000);

        expect(await screen.findByText('+30,00 ₽')).toBeVisible();
        await user.click(screen.getByRole('combobox', { name: 'Тип операции' }));
        await user.click(await screen.findByRole('option', { name: 'Списание' }));
        await waitFor(() => expect(requests.at(-1)).toContain('type=Withdrawal'));
        expect(await screen.findByText('−20,00 ₽')).toBeVisible();
        expect(screen.getByText('−5,00 ₽')).toBeVisible();

        fireEvent.change(screen.getByLabelText('С даты'), { target: { value: '2026-11-27' } });
        fireEvent.change(screen.getByLabelText('По дату'), { target: { value: '2026-11-27' } });
        await waitFor(() => {
            expect(requests.at(-1)).toContain('type=Withdrawal');
            expect(requests.at(-1)).toContain('from=2026-11-27');
            expect(requests.at(-1)).toContain('to=2026-11-27');
        });
        await waitFor(() => expect(screen.queryByText('−5,00 ₽')).not.toBeInTheDocument());

        await user.click(screen.getByRole('button', { name: 'Сбросить фильтры' }));
        expect(await screen.findByText('−5,00 ₽')).toBeVisible();
    });

    it('keeps filters on subsequent history pages', async () => {
        const user = userEvent.setup();
        serveWallets();
        const requests: URLSearchParams[] = [];
        const records = Array.from({ length: 22 }, (_, index) => ({
            id: `withdrawal-${index}`,
            referenceId: null,
            type: 'Withdrawal',
            amount: index + 1,
            currency: 'RUB',
            balanceBefore: index + 2,
            balanceAfter: 1,
            description: null,
            createdAt: '2026-11-27T12:00:00Z',
        }));
        server.use(
            http.get(historyUrl, ({ request }) => {
                const params = new URL(request.url).searchParams;
                requests.push(new URLSearchParams(params));
                const filtered = records.filter(
                    (operation) => !params.get('type') || operation.type === params.get('type'),
                );
                const page = Number(params.get('page'));
                const pageSize = Number(params.get('pageSize'));
                return HttpResponse.json({
                    items: filtered.slice((page - 1) * pageSize, page * pageSize),
                    page,
                    pageSize,
                    totalCount: filtered.length,
                    totalPages: Math.ceil(filtered.length / pageSize),
                });
            }),
        );
        openPage(2000);

        await screen.findByText('−22,00 ₽');
        await user.click(screen.getByRole('combobox', { name: 'Тип операции' }));
        await user.click(await screen.findByRole('option', { name: 'Списание' }));

        await waitFor(() => {
            expect(
                requests.some(
                    (params) => params.get('page') === '2' && params.get('type') === 'Withdrawal',
                ),
            ).toBe(true);
        });
        expect(screen.getAllByRole('listitem')).toHaveLength(22);
    });

    it('fetches more when the viewport reaches the end and stops after the last page', async () => {
        serveWallets();
        const requests = serveHistory(23);
        openPage(2000);
        await screen.findByText('+1,00 ₽');
        expect(screen.getAllByRole('listitem')).toHaveLength(23);
        expect(requests).toEqual([1, 2]);
    });

    it('shows each operation once when a new credit shifts the page boundary', async () => {
        serveWallets();
        serveHistory(23, { prependBeforePageTwo: true });
        openPage(2000);
        await screen.findByText('+1,00 ₽');
        expect(screen.getAllByRole('listitem')).toHaveLength(23);
        expect(screen.getAllByText('+4,00 ₽')).toHaveLength(1);
    });

    it('keeps the first page visible and retries an unsuccessful append', async () => {
        serveWallets();
        const requests = serveHistory(23, { failPageTwoOnce: true });
        openPage(2000);
        expect(await screen.findByRole('alert')).toHaveTextContent('Сервис временно недоступен');
        expect(screen.getByText('+23,00 ₽')).toBeVisible();
        await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
        await screen.findByText('+1,00 ₽');
        expect(requests).toEqual([1, 2, 2]);
    });

    it('displays the empty ledger after retrying an unavailable history endpoint', async () => {
        serveWallets();
        server.use(http.get(historyUrl, () => new HttpResponse(null, { status: 503 })));
        openPage();
        expect(await screen.findByRole('alert')).toHaveTextContent('Сервис временно недоступен');
        serveHistory(0);
        await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
        expect(await screen.findByText('Операций пока нет')).toBeVisible();
        expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    });

    it('links to wallets when the user has no RUB wallet', async () => {
        serveWallets([]);
        openPage();
        expect(await screen.findByText('Рублёвого кошелька пока нет')).toBeVisible();
        expect(screen.getByRole('link', { name: 'Перейти к кошелькам' })).toHaveAttribute(
            'href',
            '/wallets',
        );
    });

    it('cancels the request when leaving the history', async () => {
        serveWallets();
        let firstRequest: Request | undefined;
        let release!: () => void;
        const deferred = new Promise<void>((resolve) => {
            release = resolve;
        });
        server.use(
            http.get(
                historyUrl,
                async ({ request }) => {
                    firstRequest = request;
                    await deferred;
                    return HttpResponse.json({
                        items: [],
                        page: 1,
                        pageSize: 10,
                        totalCount: 0,
                        totalPages: 0,
                    });
                },
                { once: true },
            ),
        );
        const { unmount } = openPage();
        try {
            await waitFor(() => expect(firstRequest).toBeDefined());
            unmount();
            expect(firstRequest?.signal.aborted).toBe(true);
        } finally {
            release();
        }
    });
});
