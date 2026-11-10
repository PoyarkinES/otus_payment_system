import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import { renderRoute } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

const URL = 'http://localhost:8080/bff/health';

function health(body: object, status = 200) {
    server.use(http.get(URL, () => HttpResponse.json(body, { status })));
}

function problem(body: object, status: number) {
    server.use(
        http.get(URL, () =>
            HttpResponse.json(body, {
                status,
                headers: { 'Content-Type': 'application/problem+json' },
            }),
        ),
    );
}

function serviceRow(name: string): HTMLElement {
    return screen.getByRole('listitem', { name });
}

describe('StatusPage', () => {
    it('shows loading state first', () => {
        health({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } });

        renderRoute('/');

        expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('shows all services ok', async () => {
        health({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } });

        renderRoute('/');

        expect(await screen.findByText('Все сервисы работают')).toBeInTheDocument();
        expect(within(serviceRow('Gateway')).getByText('Работает')).toBeInTheDocument();
        expect(within(serviceRow('Identity')).getByText('Работает')).toBeInTheDocument();
    });

    it('shows degraded state on 503', async () => {
        health({ status: 'degraded', services: { gateway: 'ok', identity: 'unavailable' } }, 503);

        renderRoute('/');

        expect(await screen.findByText('Система работает с ограничениями')).toBeInTheDocument();
        expect(within(serviceRow('Identity')).getByText('Недоступен')).toBeInTheDocument();
        expect(within(serviceRow('Gateway')).getByText('Работает')).toBeInTheDocument();
        expect(screen.queryByText('Ошибка на сервере')).not.toBeInTheDocument();
    });

    it('shows problem with correlation id on 500', async () => {
        problem(
            {
                status: 500,
                title: 'Internal Server Error',
                detail: 'Внутренняя ошибка',
                traceId: 'trace-123',
            },
            500,
        );

        renderRoute('/');

        expect(await screen.findByText('Ошибка на сервере')).toBeInTheDocument();
        expect(screen.getByText('Внутренняя ошибка')).toBeInTheDocument();
        expect(screen.getByText('Код для поддержки: trace-123')).toBeInTheDocument();
    });

    it('shows rate limit message on 429', async () => {
        problem({ status: 429, title: 'Too Many Requests', detail: 'Слишком много попыток.' }, 429);

        renderRoute('/');

        expect(await screen.findByText('Слишком много запросов')).toBeInTheDocument();
    });

    it('shows unavailable state when network fails and recovers on retry', async () => {
        server.use(http.get(URL, () => HttpResponse.error()));
        renderRoute('/');

        expect(await screen.findByText('Сервер недоступен')).toBeInTheDocument();

        health({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } });
        await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

        expect(await screen.findByText('Все сервисы работают')).toBeInTheDocument();
    });

    it('hides stale status when refresh fails', async () => {
        health({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } });
        renderRoute('/');
        expect(await screen.findByText('Все сервисы работают')).toBeInTheDocument();

        server.use(http.get(URL, () => HttpResponse.error()));
        await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

        expect(await screen.findByText('Сервер недоступен')).toBeInTheDocument();
        expect(screen.queryByText('Все сервисы работают')).not.toBeInTheDocument();
        expect(screen.queryByRole('listitem', { name: 'Gateway' })).not.toBeInTheDocument();
    });

    it('renders non-string service status without crashing', async () => {
        health({
            status: 'ok',
            services: { gateway: 'ok', identity: { status: 'ok', latencyMs: 5 } },
        });

        renderRoute('/');

        expect(await screen.findByText('Все сервисы работают')).toBeInTheDocument();
        expect(
            within(serviceRow('Identity')).getByText('{"status":"ok","latencyMs":5}'),
        ).toBeInTheDocument();
    });

    it('renders unknown service status neutrally', async () => {
        health({ status: 'ok', services: { gateway: 'ok', identity: 'starting', wallets: 'ok' } });

        renderRoute('/');

        expect(await screen.findByText('Все сервисы работают')).toBeInTheDocument();
        expect(within(serviceRow('Identity')).getByText('starting')).toBeInTheDocument();
        expect(within(serviceRow('wallets')).getByText('Работает')).toBeInTheDocument();
    });
});
