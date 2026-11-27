import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useCallback, type ReactNode } from 'react';

import { AuthProvider, useAuth } from '@/shared/auth/AuthContext';
import { clearTokens, getAccessToken, getRefreshToken } from '@/shared/api/auth/storage';
import { AppProviders } from '@/app/providers';
import { createQueryClient } from '@/app/queryClient';
import { apiFetch, REFRESH_URL } from '@/shared/api/http';
import { server } from '@/shared/testing/server';

afterEach(() => {
    clearTokens();
});

function renderWithAuth(ui: ReactNode) {
    const queryClient = createQueryClient();
    queryClient.setDefaultOptions({ queries: { retry: false } });
    return render(<AppProviders queryClient={queryClient}>{ui}</AppProviders>);
}

describe('AuthContext', () => {
    it('throws when useAuth is called outside AuthProvider', () => {
        function BrokenComponent() {
            useAuth();
            return null;
        }

        const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
        expect(() => {
            render(<BrokenComponent />);
            consoleErrorSpy.mockRestore();
        }).toThrow('useAuth must be used within AuthProvider');
    });

    it('provides default state with no tokens', () => {
        renderWithAuth(
            <AuthProvider>
                <AuthDisplay />
            </AuthProvider>,
        );

        expect(screen.getByTestId('is-authenticated')).toHaveTextContent('false');
        expect(screen.getByTestId('access-token')).toHaveTextContent('null');
        expect(screen.getByTestId('refresh-token')).toHaveTextContent('null');
    });

    it('setTokens updates state and syncs with storage', async () => {
        renderWithAuth(
            <AuthProvider>
                <SetTokensDisplay />
            </AuthProvider>,
        );

        await userEvent.click(screen.getByRole('button', { name: 'Set tokens' }));

        expect(await screen.findByText('test-at')).toBeInTheDocument();
        expect(screen.getByText('test-rt')).toBeInTheDocument();

        // Storage должен быть синхронизирован
        expect(getAccessToken()).toBe('test-at');
        expect(getRefreshToken()).toBe('test-rt');
    });

    it('clearTokens resets state and storage', async () => {
        renderWithAuth(
            <AuthProvider>
                <ClearTokensDisplay />
            </AuthProvider>,
        );

        // Сначала установим токены
        await userEvent.click(screen.getByRole('button', { name: 'Set tokens' }));
        expect(await screen.findByText('test-at')).toBeInTheDocument();

        // Затем очистим
        await userEvent.click(screen.getByRole('button', { name: 'Clear tokens' }));

        expect(screen.getByTestId('access-token')).toHaveTextContent('null');

        // Storage должен быть очищен
        expect(getAccessToken()).toBeNull();
        expect(getRefreshToken()).toBeNull();
    });

    it('isAuthenticated is true when refresh token is present', async () => {
        renderWithAuth(<SetTokensDisplay />);

        // Изначально false
        expect(screen.getByTestId('is-authenticated')).toHaveTextContent('false');

        // Установим токены
        await userEvent.click(screen.getByRole('button', { name: 'Set tokens' }));

        // isAuthenticated должен стать true (есть refreshToken)
        expect(await screen.findByTestId('is-authenticated')).toHaveTextContent('true');
    });

    it('removes private data on session expiry and before another login', async () => {
        const client = createQueryClient();
        render(
            <AppProviders queryClient={client}>
                <SetTokensDisplay />
            </AppProviders>,
        );
        const key = ['/bff/me'];
        const login = screen.getByRole('button', { name: 'Set tokens' });
        await userEvent.click(login);
        client.setQueryData(key, { email: 'first@example.com' });

        server.use(
            http.get('*/bff/me', () => new HttpResponse(null, { status: 401 })),
            http.post('*' + REFRESH_URL, () => new HttpResponse(null, { status: 401 })),
        );
        await act(async () => {
            await expect(apiFetch('/bff/me')).rejects.toMatchObject({ status: 401 });
        });
        expect(screen.getByTestId('is-authenticated')).toHaveTextContent('false');
        expect(client.getQueryData(key)).toBeUndefined();

        client.setQueryData(key, { email: 'stale@example.com' });
        await userEvent.click(login);
        expect(client.getQueryData(key)).toBeUndefined();
    });

    it('isAuthenticated is true even when access token is expired but refresh exists', async () => {
        renderWithAuth(<ExpiredAccessTokenDisplay />);

        // Установим истёкший access-токен, но валидный refresh
        await userEvent.click(screen.getByRole('button', { name: 'Set expired access token' }));

        // isAuthenticated должен быть true — refresh-токен есть, access можно обновить
        expect(await screen.findByTestId('is-authenticated')).toHaveTextContent('true');
    });
});

// --- Test components ---

function AuthDisplay() {
    const { isAuthenticated, accessToken, refreshToken } = useAuth();
    return (
        <>
            <span data-testid="is-authenticated">{isAuthenticated ? 'true' : 'false'}</span>
            <span data-testid="access-token">{accessToken ?? 'null'}</span>
            <span data-testid="refresh-token">{refreshToken ?? 'null'}</span>
        </>
    );
}

function SetTokensDisplay() {
    const { isAuthenticated, accessToken, refreshToken, setTokens } = useAuth();
    const handleSet = useCallback(() => {
        setTokens({
            accessToken: 'test-at',
            refreshToken: 'test-rt',
            expiresAt: new Date(Date.now() + 3600000).toISOString(),
            refreshExpiresAt: new Date(Date.now() + 86400000).toISOString(),
        });
    }, [setTokens]);

    return (
        <>
            <span data-testid="is-authenticated">{isAuthenticated ? 'true' : 'false'}</span>
            <span>{accessToken ?? 'null'}</span>
            <span>{refreshToken ?? 'null'}</span>
            <button onClick={handleSet}>Set tokens</button>
        </>
    );
}

function ClearTokensDisplay() {
    const { isAuthenticated, accessToken, clearTokens, setTokens } = useAuth();
    const handleSet = useCallback(() => {
        setTokens({
            accessToken: 'test-at',
            refreshToken: 'test-rt',
            expiresAt: new Date(Date.now() + 3600000).toISOString(),
            refreshExpiresAt: new Date(Date.now() + 86400000).toISOString(),
        });
    }, [setTokens]);

    const handleClear = useCallback(() => {
        clearTokens();
    }, [clearTokens]);

    return (
        <>
            <span data-testid="is-authenticated">{isAuthenticated ? 'true' : 'false'}</span>
            <span data-testid="access-token">{accessToken ?? 'null'}</span>
            <button onClick={handleSet}>Set tokens</button>
            <button onClick={handleClear}>Clear tokens</button>
        </>
    );
}

function ExpiredAccessTokenDisplay() {
    const { isAuthenticated, setTokens } = useAuth();
    const handleSetExpired = useCallback(() => {
        // Access-токен истёк, но refresh-токен валиден — isAuthenticated должен быть true.
        setTokens({
            accessToken: 'expired-at',
            refreshToken: 'valid-rt',
            expiresAt: new Date(Date.now() - 1000).toISOString(),
            refreshExpiresAt: new Date(Date.now() + 86400000).toISOString(),
        });
    }, [setTokens]);

    return (
        <>
            <span data-testid="is-authenticated">{isAuthenticated ? 'true' : 'false'}</span>
            <button onClick={handleSetExpired}>Set expired access token</button>
        </>
    );
}
