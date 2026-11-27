import Button from '@mui/material/Button';
import MenuItem from '@mui/material/MenuItem';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useState } from 'react';
import { Link } from 'react-router';
import { Virtuoso, type Components } from 'react-virtuoso';

import { AmountVisibilityButton, useAmountVisibility } from '@/pages/wallets/AmountVisibility';
import { OperationRow } from '@/pages/wallets/OperationList';
import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import type { BffWalletOperationResult } from '@/shared/api/generated/model';
import { useWalletOperations } from '@/shared/api/walletOperations';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

const operationTypes = [
    { value: 'TopUp', label: 'Пополнение' },
    { value: 'Withdrawal', label: 'Списание' },
    { value: 'TransferIn', label: 'Входящий перевод' },
    { value: 'TransferOut', label: 'Исходящий перевод' },
    { value: 'Fee', label: 'Комиссия' },
    { value: 'Refund', label: 'Возврат' },
] as const;

const listComponents: Components<BffWalletOperationResult, number, HTMLUListElement> = {
    List: ({ context: _total, ...props }) => (
        <ul
            {...props}
            aria-labelledby="ledger-heading"
            className="m-0 list-none divide-y divide-divider p-0"
        />
    ),
    Item: ({ context: total, item: _operation, ...props }) => (
        <li {...props} aria-setsize={total} aria-posinset={props['data-item-index'] + 1} />
    ),
};

function WalletHistory({ walletId }: { walletId: string }) {
    const { hidden } = useAmountVisibility();
    const [type, setType] = useState('');
    const [from, setFrom] = useState('');
    const [to, setTo] = useState('');
    const validDateRange = !from || !to || from <= to;
    const hasFilters = Boolean(type || from || to);
    const {
        data,
        error,
        isPending,
        refetch,
        hasNextPage,
        fetchNextPage,
        isFetchingNextPage,
        isFetchNextPageError,
    } = useWalletOperations(walletId, { type, from, to }, validDateRange);

    return (
        <Paper
            component="section"
            aria-labelledby="ledger-heading"
            elevation={0}
            className="min-w-0 p-4 sm:p-7"
            sx={{ borderRadius: 3 }}
        >
            <Typography
                variant="h5"
                component="h2"
                id="ledger-heading"
                sx={{ fontWeight: 700, mb: 1.5 }}
            >
                Рублёвый кошелёк
            </Typography>
            <div className="mb-4 grid gap-3 sm:grid-cols-3">
                <TextField
                    select
                    label="Тип операции"
                    value={type}
                    onChange={(event) => setType(event.target.value)}
                    fullWidth
                >
                    <MenuItem value="">Все операции</MenuItem>
                    {operationTypes.map((operationType) => (
                        <MenuItem key={operationType.value} value={operationType.value}>
                            {operationType.label}
                        </MenuItem>
                    ))}
                </TextField>
                <TextField
                    type="date"
                    label="С даты"
                    value={from}
                    onChange={(event) => setFrom(event.target.value)}
                    slotProps={{
                        inputLabel: { shrink: true },
                        htmlInput: { max: to || undefined },
                    }}
                    helperText="Дата указана по UTC."
                    fullWidth
                />
                <TextField
                    type="date"
                    label="По дату"
                    value={to}
                    onChange={(event) => setTo(event.target.value)}
                    slotProps={{
                        inputLabel: { shrink: true },
                        htmlInput: { min: from || undefined },
                    }}
                    error={!validDateRange}
                    helperText={
                        validDateRange
                            ? 'Дата включается в период.'
                            : 'Начало периода не может быть позже окончания.'
                    }
                    fullWidth
                />
            </div>
            {hasFilters && (
                <Button
                    type="button"
                    className="mb-3 self-start"
                    onClick={() => {
                        setType('');
                        setFrom('');
                        setTo('');
                    }}
                >
                    Сбросить фильтры
                </Button>
            )}
            {validDateRange && isPending && <LoadingState label="Загружаем операции…" />}
            {error && !isFetchNextPageError && (
                <ErrorState error={error} onRetry={() => refetch()} />
            )}
            {data &&
                (data.items.length ? (
                    <Virtuoso
                        useWindowScroll
                        data={data.items}
                        context={data.totalCount}
                        components={listComponents}
                        computeItemKey={(index, operation) => operation.id ?? index}
                        increaseViewportBy={{ top: 120, bottom: 240 }}
                        endReached={() => {
                            if (hasNextPage && !isFetchingNextPage && !isFetchNextPageError)
                                void fetchNextPage();
                        }}
                        itemContent={(_, operation) => (
                            <OperationRow operation={operation} hidden={hidden} />
                        )}
                    />
                ) : (
                    <Typography color="text.secondary">
                        {hasFilters ? 'По выбранным фильтрам операций нет' : 'Операций пока нет'}
                    </Typography>
                ))}
            {isFetchingNextPage && <LoadingState label="Загружаем ещё операции…" />}
            {isFetchNextPageError && <ErrorState error={error} onRetry={() => fetchNextPage()} />}
        </Paper>
    );
}

export function OperationsPage() {
    const { data, error, isPending, refetch } = useBffWallets({ recentOperations: 0 });
    const walletId =
        data?.status === 200
            ? data.data.wallets?.find((wallet) => wallet.currency === 'RUB')?.id
            : undefined;

    return (
        <section className="grid min-w-0 gap-4 sm:gap-6">
            <div className="flex items-center justify-between gap-2">
                <Typography
                    variant="h3"
                    component="h1"
                    className="min-w-0"
                    sx={{ fontSize: { xs: '2rem', sm: '2.75rem' } }}
                >
                    История операций
                </Typography>
                {!error && walletId && <AmountVisibilityButton />}
            </div>
            {isPending && <LoadingState label="Загружаем кошелёк…" />}
            {error && <ErrorState error={error} onRetry={() => refetch()} />}
            {!error &&
                data?.status === 200 &&
                (walletId ? (
                    <WalletHistory key={walletId} walletId={walletId} />
                ) : (
                    <Paper elevation={0} className="p-5 sm:p-7" sx={{ borderRadius: 3 }}>
                        <Typography component="h2" variant="h5" sx={{ fontWeight: 700, mb: 2 }}>
                            Рублёвого кошелька пока нет
                        </Typography>
                        <Button component={Link} to="/wallets" variant="outlined">
                            Перейти к кошелькам
                        </Button>
                    </Paper>
                ))}
        </section>
    );
}
