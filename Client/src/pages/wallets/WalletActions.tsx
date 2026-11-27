import Button from '@mui/material/Button';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { z } from 'zod';

import { getBffWalletsQueryKey } from '@/shared/api/generated/endpoints/bff/bff';
import type { BffWalletResult } from '@/shared/api/generated/model';
import { apiFetch } from '@/shared/api/http';
import { ErrorState } from '@/shared/ui/ErrorState';
import { formatAmount } from './formatAmount';

type MoneyAction = 'deposit' | 'withdraw';
type HoldAction = 'confirm' | 'release';
type WalletHold = { id: string; amount: number; currency: string; recipientUserId: string };
type MoneyRequest = { action: MoneyAction; value: number; idempotencyKey: string };
type PlaceHoldRequest = { value: number; recipient: string; transferId: string };

const guidSchema = z.guid();
const amountPattern = /^\d+(?:\.\d{1,4})?$/;

function newGuid() {
    if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();

    const bytes = crypto.getRandomValues(new Uint8Array(16));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    const hex = [...bytes].map((byte) => byte.toString(16).padStart(2, '0')).join('');
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export function WalletActions({ wallet }: { wallet: BffWalletResult }) {
    const client = useQueryClient();
    const [amount, setAmount] = useState('');
    const [recipientUserId, setRecipientUserId] = useState('');
    const [recipientInvalid, setRecipientInvalid] = useState(false);
    const moneyRequests = useRef(new Map<string, MoneyRequest>());
    const [placeHoldRequest, setPlaceHoldRequest] = useState<PlaceHoldRequest | null>(null);
    const [holds, setHolds] = useState<WalletHold[]>([]);
    const [notice, setNotice] = useState('');
    const [error, setError] = useState<unknown>(null);
    const amountValue = Number(amount);
    const validAmount =
        Number.isFinite(amountValue) && amountValue > 0 && amountPattern.test(amount);
    const active = wallet.status === 'Active';

    async function refreshWalletData() {
        await Promise.all([
            client.invalidateQueries({ queryKey: getBffWalletsQueryKey() }),
            client.invalidateQueries({
                queryKey: [`/api/wallets/${encodeURIComponent(wallet.id)}/operations`],
            }),
        ]);
    }

    const moneyMutation = useMutation({
        mutationFn: async ({ action, value, idempotencyKey }: MoneyRequest) =>
            apiFetch(`/api/wallets/${encodeURIComponent(wallet.id)}/${action}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Idempotency-Key': idempotencyKey,
                },
                body: JSON.stringify({ amount: value }),
            }),
        onMutate: () => {
            setNotice('');
            setError(null);
        },
        onError: setError,
        onSuccess: async (_result, request) => {
            const requestKey = `${request.action}:${request.value}`;
            if (moneyRequests.current.get(requestKey) === request)
                moneyRequests.current.delete(requestKey);
            setAmount('');
            setNotice(request.action === 'deposit' ? 'Кошелёк пополнен.' : 'Средства списаны.');
            await refreshWalletData();
        },
    });

    const placeHoldMutation = useMutation({
        mutationFn: async ({ value, recipient, transferId }: PlaceHoldRequest) => {
            const { data } = await apiFetch<{ data: { id: string } }>(
                `/api/wallets/${encodeURIComponent(wallet.id)}/holds`,
                {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        transferId,
                        amount: value,
                        recipientUserId: recipient,
                    }),
                },
            );
            return {
                id: data.id,
                amount: value,
                currency: wallet.currency,
                recipientUserId: recipient,
            };
        },
        onMutate: () => {
            setNotice('');
            setError(null);
        },
        onError: setError,
        onSuccess: async (hold) => {
            setPlaceHoldRequest(null);
            setHolds((current) => [...current, hold]);
            setNotice('Резерв создан.');
            await refreshWalletData();
        },
    });

    const updateHoldMutation = useMutation({
        mutationFn: async ({ holdId, action }: { holdId: string; action: HoldAction }) =>
            apiFetch(`/api/wallets/holds/${encodeURIComponent(holdId)}/${action}`, {
                method: 'POST',
            }),
        onMutate: () => {
            setNotice('');
            setError(null);
        },
        onError: setError,
        onSuccess: async (_result, { holdId, action }) => {
            setHolds((current) => current.filter((hold) => hold.id !== holdId));
            setNotice(action === 'confirm' ? 'Резерв подтверждён.' : 'Резерв освобождён.');
            await refreshWalletData();
        },
    });

    const pending =
        moneyMutation.isPending || placeHoldMutation.isPending || updateHoldMutation.isPending;

    function runMoneyAction(action: MoneyAction) {
        if (!active || !validAmount || pending) return;
        const requestKey = `${action}:${amountValue}`;
        const request = moneyRequests.current.get(requestKey) ?? {
            action,
            value: amountValue,
            idempotencyKey: newGuid(),
        };
        moneyRequests.current.set(requestKey, request);
        moneyMutation.mutate(request);
    }

    function placeHold() {
        const recipient = recipientUserId.trim();
        if (!active || !validAmount || pending) return;
        if (!guidSchema.safeParse(recipient).success) {
            setRecipientInvalid(true);
            return;
        }
        const request =
            placeHoldRequest?.value === amountValue && placeHoldRequest.recipient === recipient
                ? placeHoldRequest
                : { value: amountValue, recipient, transferId: newGuid() };
        setPlaceHoldRequest(request);
        placeHoldMutation.mutate(request);
    }

    return (
        <section className="relative mt-6 grid gap-4" aria-label="Действия с кошельком">
            {!active && (
                <Typography color="text.secondary">
                    Операции доступны только для активного кошелька.
                </Typography>
            )}
            <div className="grid gap-3 sm:grid-cols-[minmax(12rem,1fr)_auto] sm:items-end">
                <TextField
                    label="Сумма операции"
                    type="number"
                    value={amount}
                    onChange={(event) => setAmount(event.target.value)}
                    slotProps={{ htmlInput: { min: 0.0001, step: 0.0001 } }}
                    helperText={
                        amount && !validAmount
                            ? 'Введите положительную сумму не более чем с 4 знаками после запятой.'
                            : 'До 4 знаков после запятой.'
                    }
                    error={Boolean(amount) && !validAmount}
                    disabled={!active || pending}
                    fullWidth
                />
                <div className="flex flex-wrap gap-2">
                    <Button
                        type="button"
                        variant="contained"
                        disableElevation
                        disabled={!active || !validAmount || pending}
                        onClick={() => runMoneyAction('deposit')}
                    >
                        {moneyMutation.isPending && moneyMutation.variables?.action === 'deposit'
                            ? 'Пополняем…'
                            : 'Пополнить'}
                    </Button>
                    <Button
                        type="button"
                        variant="outlined"
                        disabled={!active || !validAmount || pending}
                        onClick={() => runMoneyAction('withdraw')}
                    >
                        {moneyMutation.isPending && moneyMutation.variables?.action === 'withdraw'
                            ? 'Списываем…'
                            : 'Списать'}
                    </Button>
                </div>
            </div>

            <div className="grid gap-3 sm:grid-cols-[minmax(12rem,1fr)_auto] sm:items-end">
                <TextField
                    label="UUID получателя"
                    value={recipientUserId}
                    onChange={(event) => {
                        setRecipientUserId(event.target.value);
                        setRecipientInvalid(false);
                    }}
                    error={recipientInvalid}
                    helperText={
                        recipientInvalid
                            ? 'Введите UUID получателя в формате UUID.'
                            : 'Нужен для демонстрационного перевода через hold.'
                    }
                    disabled={!active || pending}
                    fullWidth
                />
                <Button
                    type="button"
                    variant="outlined"
                    disabled={!active || !validAmount || pending}
                    onClick={placeHold}
                >
                    {placeHoldMutation.isPending ? 'Создаём резерв…' : 'Зарезервировать'}
                </Button>
            </div>

            <Typography variant="caption" color="text.secondary">
                Wallets API не возвращает список ранее созданных резервов: здесь отображаются только
                hold, созданные на этой странице.
            </Typography>

            {holds.length > 0 && (
                <div className="grid gap-2" aria-label="Резервы текущего сеанса">
                    <Typography component="h3" variant="subtitle2">
                        Резервы текущего сеанса
                    </Typography>
                    {holds.map((hold) => (
                        <div
                            key={hold.id}
                            className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-divider p-3"
                        >
                            <Typography variant="body2" className="min-w-0 break-all">
                                {formatAmount(hold.amount, hold.currency)} · получатель{' '}
                                {hold.recipientUserId}
                            </Typography>
                            <div className="flex gap-2">
                                <Button
                                    type="button"
                                    size="small"
                                    disabled={pending}
                                    onClick={() =>
                                        updateHoldMutation.mutate({
                                            holdId: hold.id,
                                            action: 'confirm',
                                        })
                                    }
                                >
                                    Подтвердить перевод
                                </Button>
                                <Button
                                    type="button"
                                    size="small"
                                    color="inherit"
                                    disabled={pending}
                                    onClick={() =>
                                        updateHoldMutation.mutate({
                                            holdId: hold.id,
                                            action: 'release',
                                        })
                                    }
                                >
                                    Освободить резерв
                                </Button>
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {notice && <Typography role="status">{notice}</Typography>}
            {error !== null && <ErrorState error={error} />}
        </section>
    );
}
