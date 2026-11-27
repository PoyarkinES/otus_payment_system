import { screen, waitFor } from '@testing-library/react';
import { createMemoryRouter, createPath, RouterProvider } from 'react-router';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { clearTokens } from '@/shared/api/auth/storage';
import { LoginPage } from '@/pages/login/LoginPage';
import { renderRoute, renderWithProviders } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

afterEach(() => {
    clearTokens();
    vi.restoreAllMocks();
});

const LOGIN_URL = 'http://localhost:8080/api/identity/auth/login';

function mockLoginSuccess() {
    server.use(
        http.post(LOGIN_URL, () =>
            HttpResponse.json({
                accessToken: 'test-at',
                refreshToken: 'test-rt',
                expiresAt: new Date(Date.now() + 3600000).toISOString(),
                refreshExpiresAt: new Date(Date.now() + 86400000).toISOString(),
            }),
        ),
    );
}

function mockProblem(body: object, status: number) {
    server.use(
        http.post(LOGIN_URL, () =>
            HttpResponse.json(body, {
                status,
                headers: { 'Content-Type': 'application/problem+json' },
            }),
        ),
    );
}

describe('LoginPage', () => {
    it('shows email and password fields', () => {
        renderRoute('/login');

        expect(screen.getByLabelText('Email')).toBeInTheDocument();
        expect(screen.getByLabelText('Пароль')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Войти' })).toBeInTheDocument();
        expect(screen.getByRole('heading', { name: 'Вход' })).toBeInTheDocument();
    });

    it('links to register page', () => {
        renderRoute('/login');

        expect(screen.getByRole('link', { name: 'Зарегистрироваться' })).toHaveAttribute(
            'href',
            '/register',
        );
    });

    it('shows validation error for empty password', async () => {
        renderRoute('/login');

        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        expect(screen.getByText('Введите пароль')).toBeInTheDocument();
    });

    it('calls login API and sets tokens on success', async () => {
        const { getAccessToken } = await import('@/shared/api/auth/storage');

        mockLoginSuccess();

        renderRoute('/login');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'password123');
        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        // После успешного входа токены должны быть сохранены в storage
        expect(await screen.findByRole('heading', { name: 'Кошельки', level: 1 })).toBeVisible();
        expect(getAccessToken()).toBe('test-at');
    });

    it('shows field errors on 400 response', async () => {
        mockProblem(
            {
                status: 400,
                title: 'Bad Request',
                errors: { email: ['Email уже используется'] },
            },
            400,
        );

        renderRoute('/login');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'password123');
        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        expect(await screen.findByText('Email уже используется')).toBeInTheDocument();
    });

    it.each([
        ['/wallets', null],
        [
            '/wallets?page=2#operations',
            { from: { pathname: '/wallets', search: '?page=2', hash: '#operations' } },
        ],
    ] as const)('returns to %s after login', async (expectedPath, state) => {
        mockLoginSuccess();
        const router = createMemoryRouter(
            [
                { path: '/login', element: <LoginPage /> },
                { path: '*', element: <div /> },
            ],
            { initialEntries: [{ pathname: '/login', state }] },
        );
        renderWithProviders(<RouterProvider router={router} />);

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'password123');
        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        await waitFor(() => expect(createPath(router.state.location)).toBe(expectedPath));
    });

    it('shows detail message on 401 response', async () => {
        mockProblem(
            { status: 401, title: 'Unauthorized', detail: 'Неверный email или пароль' },
            401,
        );

        renderRoute('/login');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'wrong-password');
        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        expect(await screen.findByText('Неверный email или пароль')).toBeInTheDocument();
    });

    it('shows title when no detail', async () => {
        mockProblem({ status: 500, title: 'Internal Server Error' }, 500);

        renderRoute('/login');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'password123');
        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        // err.detail отсутствует, err.title = ReasonPhrase — показывается title
        expect(await screen.findByText('Internal Server Error')).toBeInTheDocument();
    });

    it('shows network error when server is down', async () => {
        server.use(http.post(LOGIN_URL, () => HttpResponse.error()));

        renderRoute('/login');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'password123');
        await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

        expect(await screen.findByText(/Не удалось войти/)).toBeInTheDocument();
    });
});
