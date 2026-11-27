import { useRef } from 'react';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import NativeSelect from '@mui/material/NativeSelect';
import Paper from '@mui/material/Paper';
import SvgIcon from '@mui/material/SvgIcon';
import Typography from '@mui/material/Typography';
import { Link, Navigate, useSearchParams } from 'react-router';

import { AmountVisibilityButton, useAmountVisibility } from '@/pages/wallets/AmountVisibility';
import { OperationList } from '@/pages/wallets/OperationList';
import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import { useWalletOperations } from '@/shared/api/walletOperations';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

const pageSizes = [10, 20, 50];

function WalletHistory({ walletId }: { walletId: string }) {
    const { hidden } = useAmountVisibility();
    const heading = useRef<HTMLHeadingElement>(null);
    const [search, setSearch] = useSearchParams();
    const requestedSize = Number(search.get('pageSize'));
    const pageSize = pageSizes.includes(requestedSize) ? requestedSize : pageSizes[0];
    const requestedPage = Number(search.get('page'));
    // Wallets использует Int32 для смещения (page - 1) * pageSize.
    const maxPage = Math.floor(2_147_483_647 / pageSize) + 1;
    const page =
        Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= maxPage
            ? requestedPage
            : 1;
    const { data, error, isPending, refetch } = useWalletOperations(walletId, page, pageSize);
    const lastPage = data && Math.max(1, data.totalPages);

    function paginationParams(nextPage: number, nextSize: number) {
        const params = new URLSearchParams(search);
        if (nextPage === 1) params.delete('page');
        else params.set('page', String(nextPage));
        if (nextSize === pageSizes[0]) params.delete('pageSize');
        else params.set('pageSize', String(nextSize));
        return params;
    }

    function goToPage(nextPage: number) {
        setSearch(paginationParams(nextPage, pageSize));
        heading.current?.focus();
    }

    if (!error && lastPage && page > lastPage) {
        return (
            <Navigate replace to={{ search: paginationParams(lastPage, pageSize).toString() }} />
        );
    }

    return (
        <Paper
            component="section"
            aria-labelledby="ledger-heading"
            elevation={0}
            className="min-w-0 p-4 sm:p-7"
            sx={{ borderRadius: 3 }}
        >
            <div className="mb-3 flex flex-wrap items-center justify-between gap-x-6 gap-y-2">
                <Typography
                    variant="h5"
                    component="h2"
                    id="ledger-heading"
                    ref={heading}
                    tabIndex={-1}
                    sx={{ fontWeight: 700 }}
                >
                    Рублёвый кошелёк
                </Typography>
                <div className="flex items-center gap-2">
                    <Typography
                        component="label"
                        variant="body2"
                        color="text.secondary"
                        htmlFor="ledger-page-size"
                    >
                        На странице
                    </Typography>
                    <NativeSelect
                        inputProps={{ id: 'ledger-page-size' }}
                        value={pageSize}
                        onChange={(event) =>
                            setSearch(paginationParams(1, Number(event.target.value)))
                        }
                        sx={{ '& select': { minHeight: 44, boxSizing: 'border-box' } }}
                    >
                        {pageSizes.map((size) => (
                            <option key={size} value={size}>
                                {size}
                            </option>
                        ))}
                    </NativeSelect>
                </div>
            </div>
            {isPending && <LoadingState label="Загружаем операции…" />}
            {error && <ErrorState error={error} onRetry={() => refetch()} />}
            {!error &&
                data &&
                (data.totalCount === 0 ? (
                    <Typography color="text.secondary">Операций пока нет</Typography>
                ) : (
                    <>
                        <OperationList operations={data.items} hidden={hidden} />
                        <div className="mt-3 flex flex-wrap items-center justify-between gap-x-4 gap-y-1 border-t border-divider pt-3">
                            <Typography variant="body2" color="text.secondary">
                                {(data.page - 1) * data.pageSize + 1}–
                                {(data.page - 1) * data.pageSize + data.items.length} из{' '}
                                {data.totalCount}
                            </Typography>
                            <nav
                                aria-label="Страницы истории операций"
                                className="flex max-w-full items-center gap-1"
                            >
                                <IconButton
                                    aria-label="Предыдущая страница"
                                    disabled={page <= 1}
                                    onClick={() => goToPage(page - 1)}
                                    sx={{ width: 44, height: 44 }}
                                >
                                    <SvgIcon>
                                        <path d="M14.4 5.4 7.8 12l6.6 6.6 1.4-1.4-5.2-5.2 5.2-5.2z" />
                                    </SvgIcon>
                                </IconButton>
                                <Typography
                                    variant="body2"
                                    aria-live="polite"
                                    className="text-center"
                                >
                                    Страница {data.page} из {data.totalPages}
                                </Typography>
                                <IconButton
                                    aria-label="Следующая страница"
                                    disabled={page >= data.totalPages}
                                    onClick={() => goToPage(page + 1)}
                                    sx={{ width: 44, height: 44 }}
                                >
                                    <SvgIcon>
                                        <path d="m9.6 5.4-1.4 1.4 5.2 5.2-5.2 5.2 1.4 1.4 6.6-6.6z" />
                                    </SvgIcon>
                                </IconButton>
                            </nav>
                        </div>
                    </>
                ))}
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
                    <WalletHistory walletId={walletId} />
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
