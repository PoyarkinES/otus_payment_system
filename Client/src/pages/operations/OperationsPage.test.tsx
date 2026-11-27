import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router';
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

function serveHistory(count: number) {
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
            if (page < 1 || ![10, 20, 50].includes(pageSize)) {
                return new HttpResponse(null, { status: 400 });
            }
            return HttpResponse.json({
                items: records.slice((page - 1) * pageSize, page * pageSize),
                page,
                pageSize,
                totalCount: count,
                totalPages: Math.ceil(count / pageSize),
            });
        }),
    );
}

function openPage(path = '/operations') {
    const router = createMemoryRouter(
        [
            {
                path: '/operations',
                element: (
                    <AmountVisibilityProvider>
                        <OperationsPage />
                    </AmountVisibilityProvider>
                ),
            },
        ],
        { initialEntries: [path] },
    );
    renderWithProviders(<RouterProvider router={router} />);
    return router;
}

describe('OperationsPage', () => {
    it('opens a deep-linked page for the RUB wallet and displays the server order and totals', async () => {
        serveWallets([{ id: 'other', currency: 'USD' }, rub]);
        serveHistory(23);
        openPage('/operations?page=3');

        expect(screen.getByRole('status')).toHaveTextContent('Загружаем');
        const rows = await screen.findAllByRole('listitem');
        expect(rows).toHaveLength(3);
        expect(rows[0]).toHaveTextContent('+3,00 ₽');
        expect(rows[2]).toHaveTextContent('+1,00 ₽');
        expect(screen.getByText('21–23 из 23')).toBeVisible();
        expect(screen.getByText('Страница 3 из 3')).toBeVisible();
        expect(screen.getByRole('button', { name: 'Следующая страница' })).toBeDisabled();
    });

    it('changes pages, resets the page when resizing and restores it on Back', async () => {
        serveWallets();
        serveHistory(23);
        const router = openPage();
        await screen.findByText('1–10 из 23');
        expect(screen.getByRole('button', { name: 'Предыдущая страница' })).toBeDisabled();

        await userEvent.click(screen.getByRole('button', { name: 'Следующая страница' }));
        await screen.findByText('11–20 из 23');
        expect(new URLSearchParams(router.state.location.search).get('page')).toBe('2');
        await userEvent.selectOptions(screen.getByRole('combobox', { name: 'На странице' }), '20');
        await screen.findByText('1–20 из 23');
        expect(screen.getAllByRole('listitem')).toHaveLength(20);
        expect(new URLSearchParams(router.state.location.search).get('pageSize')).toBe('20');

        await act(() => router.navigate(-1));
        await screen.findByText('11–20 из 23');
        expect(screen.getByRole('combobox', { name: 'На странице' })).toHaveValue('10');
    });

    it('redirects a link beyond the end to the last available page', async () => {
        serveWallets();
        serveHistory(12);
        const router = openPage('/operations?page=9');
        await screen.findByText('11–12 из 12');
        expect(new URLSearchParams(router.state.location.search).get('page')).toBe('2');
        expect(screen.getAllByRole('listitem')).toHaveLength(2);
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

    it('cancels an obsolete request when a new page size is selected', async () => {
        serveWallets();
        serveHistory(23);
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
        openPage();
        try {
            await waitFor(() => expect(firstRequest).toBeDefined());
            await userEvent.selectOptions(
                screen.getByRole('combobox', { name: 'На странице' }),
                '50',
            );
            await screen.findByText('1–23 из 23');
            expect(firstRequest?.signal.aborted).toBe(true);
        } finally {
            release();
        }
    });
});
