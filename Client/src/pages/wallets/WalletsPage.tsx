import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';

import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';
import { AmountVisibilityButton, useAmountVisibility } from './AmountVisibility';
import { RecentOperations } from './RecentOperations';
import { WalletCard } from './WalletCard';

export function WalletsPage() {
    const { data, error, isPending, refetch } = useBffWallets();
    const wallets = !error && data?.status === 200 ? (data.data.wallets ?? []) : undefined;
    const { hidden } = useAmountVisibility();

    return (
        <section className="grid min-w-0 gap-4 sm:gap-6">
            <div className="flex items-center justify-between gap-3">
                <Typography
                    variant="h3"
                    component="h1"
                    sx={{ fontSize: { xs: '2rem', sm: '2.75rem' } }}
                >
                    Кошельки
                </Typography>
                {wallets && wallets.length > 0 && <AmountVisibilityButton />}
            </div>
            {isPending && <LoadingState label="Загружаем кошельки…" />}
            {error && <ErrorState error={error} onRetry={() => refetch()} />}
            {wallets && (
                <>
                    {wallets.length > 0 ? (
                        wallets.map((wallet, index) => (
                            <WalletCard key={wallet.id ?? index} wallet={wallet} hidden={hidden} />
                        ))
                    ) : (
                        <Paper
                            elevation={0}
                            className="p-5 sm:p-8"
                            sx={{ bgcolor: 'primary.light', borderRadius: 3 }}
                        >
                            <Typography component="h2" variant="h5">
                                Кошелька пока нет
                            </Typography>
                        </Paper>
                    )}
                    <RecentOperations wallets={wallets} hidden={hidden} />
                </>
            )}
        </section>
    );
}
