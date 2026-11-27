import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router';
import { Virtuoso, type Components } from 'react-virtuoso';

import { AmountVisibilityButton, useAmountVisibility } from '@/pages/wallets/AmountVisibility';
import { OperationRow } from '@/pages/wallets/OperationList';
import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import type { BffWalletOperationResult } from '@/shared/api/generated/model';
import { useWalletOperations } from '@/shared/api/walletOperations';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

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
    const {
        data,
        error,
        isPending,
        refetch,
        hasNextPage,
        fetchNextPage,
        isFetchingNextPage,
        isFetchNextPageError,
    } = useWalletOperations(walletId);

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
            {isPending && <LoadingState label="Загружаем операции…" />}
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
                    <Typography color="text.secondary">Операций пока нет</Typography>
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
