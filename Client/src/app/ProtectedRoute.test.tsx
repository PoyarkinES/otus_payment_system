import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';

import { clearTokens } from '@/shared/api/auth/storage';
import { renderRoute } from '@/shared/testing/render';

afterEach(() => {
    clearTokens();
});

describe('ProtectedRoute', () => {
    it('redirects unauthenticated user to /login when accessing protected route', () => {
        // Без токенов доступ к '/' должен перенаправить на /login
        renderRoute('/');

        // После редиректа должна быть страница входа
        expect(screen.getByRole('heading', { name: 'Вход' })).toBeInTheDocument();
    });

    it('allows authenticated user to access protected route', async () => {
        // С токенами доступ к '/' должен показать главную страницу
        renderRoute('/', true);

        expect(screen.getByText('Otus Pay')).toBeInTheDocument();
        expect(screen.getByRole('heading', { level: 1, name: 'Кошельки' })).toBeInTheDocument();
    });

    it('redirects to login when accessing any protected child route without auth', () => {
        // Любой маршрут под '/' защищён
        renderRoute('/admin/status');

        expect(screen.getByRole('heading', { name: 'Вход' })).toBeInTheDocument();
    });

    it('allows authenticated user to access child routes', () => {
        renderRoute('/profile', true);

        expect(screen.getByText('Otus Pay')).toBeInTheDocument();
    });

    it('does not redirect on public /login route', () => {
        renderRoute('/login');

        expect(screen.getByRole('heading', { name: 'Вход' })).toBeInTheDocument();
    });

    it('does not redirect on public /register route', () => {
        renderRoute('/register');

        expect(screen.getByRole('heading', { name: 'Регистрация' })).toBeInTheDocument();
    });
});
