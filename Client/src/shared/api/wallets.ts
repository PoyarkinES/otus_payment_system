import { apiFetch } from '@/shared/api/http';

/** Wallets принимает только RUB. Путь проходит через прокси Gateway. */
export async function createRubWallet(): Promise<void> {
    await apiFetch('/api/wallets', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ currency: 'RUB' }),
    });
}
