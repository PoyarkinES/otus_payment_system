import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { clearTokens, getSnapshot } from '@/shared/api/auth/storage';
import { renderRoute } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

const ownId = '2fd8905a-d0f6-4824-83fc-371d40f3b808';
const lookupId = 'f6d4294a-5d21-48d8-84d6-eabfe625e677';
const createdAt = '2026-11-01T12:00:00Z';

function mockAccount(role: string | null, status: string) {
    server.use(
        http.get('*/bff/me', () =>
            HttpResponse.json({ userId: ownId, email: 'me@example.com', role }),
        ),
        http.get('*/api/identity/users/me', () =>
            HttpResponse.json({
                userId: ownId,
                email: 'from-identity@example.com',
                status,
                createdAt,
            }),
        ),
    );
}

afterEach(clearTokens);

describe('ProfilePage', () => {
    it('revokes the refreshed session from the profile and opens the default page for the next account', async () => {
        const user = userEvent.setup();
        const tokens = {
            accessToken: 'access-token',
            refreshToken: 'refresh-token',
            expiresAt: new Date(Date.now() + 3600_000).toISOString(),
            refreshExpiresAt: new Date(Date.now() + 86400_000).toISOString(),
        };
        const revoked = vi.fn();
        server.use(
            http.post('*/api/identity/auth/refresh', () => HttpResponse.json(tokens)),
            http.post('*/api/identity/auth/logout', async ({ request }) => {
                revoked(request.headers.get('Authorization'), await request.json());
                return new HttpResponse(null, { status: 204 });
            }),
            http.post('*/api/identity/auth/login', () => HttpResponse.json(tokens)),
        );
        mockAccount('Admin', 'Active');
        renderRoute('/admin/users', true);
        await screen.findByRole('textbox', { name: 'UUID пользователя' });
        expect(screen.queryByRole('button', { name: 'Выйти' })).not.toBeInTheDocument();
        await user.click(screen.getByRole('link', { name: 'Профиль' }));
        await user.click(within(screen.getByRole('main')).getByRole('button', { name: 'Выйти' }));
        expect(await screen.findByRole('heading', { name: 'Вход' })).toBeVisible();
        expect(revoked.mock.calls).toEqual([
            ['Bearer access-token', { refreshToken: 'refresh-token' }],
        ]);
        expect(getSnapshot().refreshToken).toBeNull();
        await user.type(screen.getByLabelText('Email'), 'next@example.test');
        await user.type(screen.getByLabelText('Пароль'), 'test-password');
        await user.click(screen.getByRole('button', { name: 'Войти' }));
        expect(await screen.findByRole('heading', { level: 1, name: 'Кошельки' })).toBeVisible();
    });

    it.each([
        ['User', 'Active', 'Пользователь', 'Активен'],
        ['Admin', 'Blocked', 'Администратор', 'Заблокирован'],
        [null, 'Unknown', 'Неизвестна', 'Неизвестен'],
    ] as const)(
        'shows the BFF identity and the %s account status %s',
        async (role, status, roleLabel, statusLabel) => {
            mockAccount(role, status);

            renderRoute('/profile', true);

            expect(screen.getByRole('status')).toBeInTheDocument();
            expect(
                within(screen.getByRole('main')).getByRole('button', { name: 'Выйти' }),
            ).toBeEnabled();
            expect(await screen.findByText('me@example.com')).toBeInTheDocument();
            expect(screen.getByText(roleLabel)).toBeInTheDocument();
            expect(screen.getByText(statusLabel)).toBeInTheDocument();
            expect(screen.queryByText('Статус системы')).not.toBeInTheDocument();
            expect(
                screen.queryByRole('link', { name: 'Поиск пользователя' }),
            ).not.toBeInTheDocument();
        },
    );

    it('shows a retry when Identity is unavailable and enables the admin link after recovery', async () => {
        mockAccount('Admin', 'Active');
        server.use(
            http.get('*/api/identity/users/me', () => new HttpResponse(null, { status: 503 })),
        );
        renderRoute('/profile', true);

        expect(await screen.findByText('Сервис временно недоступен')).toBeInTheDocument();
        expect(
            within(screen.getByRole('main')).getByRole('button', { name: 'Выйти' }),
        ).toBeEnabled();
        expect(screen.queryByRole('link', { name: 'Поиск пользователя' })).not.toBeInTheDocument();

        mockAccount('Admin', 'Active');
        await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
        expect(await screen.findByRole('link', { name: 'Поиск пользователя' })).toHaveAttribute(
            'href',
            '/admin/users',
        );
    });
});

describe('admin system status', () => {
    it('opens system status from the profile of an active administrator', async () => {
        mockAccount('Admin', 'Active');
        renderRoute('/profile', true);

        const link = await screen.findByRole('link', { name: 'Статус системы' });
        expect(link).toHaveAttribute('href', '/admin/status');
        await userEvent.click(link);

        expect(await screen.findByText('Все сервисы работают')).toBeVisible();
    });

    it.each([
        ['User', 'Active'],
        ['Admin', 'Blocked'],
    ])('denies direct access without fetching health for %s / %s', async (role, status) => {
        mockAccount(role, status);
        const health = vi.fn(() => HttpResponse.json({ status: 'ok', services: {} }));
        server.use(http.get('*/bff/health', health));

        renderRoute('/admin/status', true);

        expect(await screen.findByText('Недостаточно прав')).toBeVisible();
        expect(screen.queryByText('Статус системы')).not.toBeInTheDocument();
        expect(health).not.toHaveBeenCalled();
    });
});

describe('AdminUserPage', () => {
    it.each([
        ['User', 'Active'],
        ['Admin', 'Blocked'],
        ['Unknown', 'Active'],
        ['Admin', 'Unknown'],
    ])('denies direct entry to role %s and status %s', async (role, status) => {
        const lookup = vi.fn(() => HttpResponse.error());
        server.use(http.get('*/api/identity/users/:id', lookup));
        mockAccount(role, status);

        renderRoute('/admin/users', true);

        expect(await screen.findByText('Недостаточно прав')).toBeInTheDocument();
        expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
        expect(lookup).not.toHaveBeenCalled();
    });

    it('looks up a user by UUID only after an active admin submits a valid identifier', async () => {
        const lookup = vi.fn(() =>
            HttpResponse.json({
                userId: lookupId,
                email: 'found@example.com',
                status: 'Blocked',
                createdAt,
            }),
        );
        server.use(http.get(`*/api/identity/users/${lookupId}`, lookup));
        mockAccount('Admin', 'Active');
        renderRoute('/profile', true);
        const user = userEvent.setup();

        await user.click(await screen.findByRole('link', { name: 'Поиск пользователя' }));
        const input = await screen.findByRole('textbox', { name: 'UUID пользователя' });
        await user.type(input, 'wrong');
        await user.click(screen.getByRole('button', { name: 'Найти' }));
        expect(
            screen.getByText('Введите UUID в формате xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.'),
        ).toBeInTheDocument();
        expect(lookup).not.toHaveBeenCalled();

        await user.clear(input);
        await user.type(input, lookupId);
        expect(lookup).not.toHaveBeenCalled();
        await user.click(screen.getByRole('button', { name: 'Найти' }));
        expect(await screen.findByText('found@example.com')).toBeInTheDocument();
        expect(screen.getByText('Заблокирован')).toBeInTheDocument();
        expect(lookup).toHaveBeenCalledTimes(1);
    });

    it('shows the lookup error when the requested user does not exist', async () => {
        server.use(
            http.get(
                `*/api/identity/users/${lookupId}`,
                () => new HttpResponse(null, { status: 404 }),
            ),
        );
        mockAccount('Admin', 'Active');
        renderRoute('/admin/users', true);
        const user = userEvent.setup();

        await user.type(
            await screen.findByRole('textbox', { name: 'UUID пользователя' }),
            lookupId,
        );
        await user.click(screen.getByRole('button', { name: 'Найти' }));

        expect(await screen.findByText('Не найдено')).toBeInTheDocument();
        expect(screen.getByRole('textbox', { name: 'UUID пользователя' })).toHaveValue(lookupId);
    });
});
