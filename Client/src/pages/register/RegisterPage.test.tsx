import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { clearTokens } from '@/shared/api/auth/storage';
import { renderRoute } from '@/shared/testing/render';
import { server } from '@/shared/testing/server';

afterEach(() => {
    clearTokens();
    vi.restoreAllMocks();
});

const REGISTER_URL = 'http://localhost:8080/api/identity/auth/register';
const LOGIN_URL = 'http://localhost:8080/api/identity/auth/login';

function mockRegisterSuccess() {
    server.use(
        http.post(REGISTER_URL, () =>
            HttpResponse.json({ userId: 'test-id', email: 'new@example.com' }, { status: 201 }),
        ),
    );
}

function mockRegisterProblem(body: object, status: number) {
    server.use(
        http.post(REGISTER_URL, () =>
            HttpResponse.json(body, {
                status,
                headers: { 'Content-Type': 'application/problem+json' },
            }),
        ),
    );
}

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

function mockRegisterError() {
    server.use(http.post(REGISTER_URL, () => HttpResponse.error()));
}

describe('RegisterPage', () => {
    it('shows email and password fields', () => {
        renderRoute('/register');

        expect(screen.getByLabelText('Email')).toBeInTheDocument();
        expect(screen.getByLabelText('Пароль')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Зарегистрироваться' })).toBeInTheDocument();
        expect(screen.getByRole('heading', { name: 'Регистрация' })).toBeInTheDocument();
    });

    it('links to login page', () => {
        renderRoute('/register');

        expect(screen.getByRole('link', { name: 'Войти' })).toHaveAttribute('href', '/login');
    });

    it('shows validation error for short password', async () => {
        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'abc1');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        expect(screen.getByText('Пароль должен быть не короче 8 символов')).toBeInTheDocument();
    });

    it('shows validation error for password without letter', async () => {
        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), '12345678');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        expect(screen.getByText('Пароль должен содержать хотя бы одну букву')).toBeInTheDocument();
    });

    it('shows validation error for password without digit', async () => {
        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'abcdefgh');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        expect(screen.getByText('Пароль должен содержать хотя бы одну цифру')).toBeInTheDocument();
    });

    it('calls register then login API and sets tokens on success', async () => {
        const { getAccessToken } = await import('@/shared/api/auth/storage');

        mockRegisterSuccess();
        mockLoginSuccess();

        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'new@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'Password1');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        // После register (201) вызывается login, токены сохраняются
        expect(await screen.findByText('Otus Payment System')).toBeInTheDocument();
        expect(getAccessToken()).toBe('test-at');
    });

    it('shows duplicate email error on 409 response', async () => {
        mockRegisterProblem(
            { status: 409, title: 'Conflict', detail: 'Пользователь уже существует' },
            409,
        );

        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'existing@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'Password1');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        expect(
            await screen.findByText('Пользователь с таким email уже зарегистрирован.'),
        ).toBeInTheDocument();
    });

    it('shows field errors on 400 response', async () => {
        mockRegisterProblem(
            {
                status: 400,
                title: 'Bad Request',
                errors: { password: ['Пароль слишком слабый'] },
            },
            400,
        );

        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'Password1');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        expect(await screen.findByText('Пароль слишком слабый')).toBeInTheDocument();
    });

    it('shows network error when server is down', async () => {
        mockRegisterError();

        renderRoute('/register');

        await userEvent.type(screen.getByLabelText('Email'), 'test@example.com');
        await userEvent.type(screen.getByLabelText('Пароль'), 'Password1');
        await userEvent.click(screen.getByRole('button', { name: 'Зарегистрироваться' }));

        expect(await screen.findByText(/Не удалось зарегистрироваться/)).toBeInTheDocument();
    });
});
