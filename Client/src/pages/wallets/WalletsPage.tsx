import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { Navigate, useLocation } from 'react-router';

import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import { CreateWalletButton } from '@/shared/ui/CreateWalletButton';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';
import { AmountVisibilityButton, useAmountVisibility } from './AmountVisibility';
import { RecentOperations } from './RecentOperations';
import { WalletCard } from './WalletCard';

const recentLimit = 5;

export function WalletsPage() {
    const { hash } = useLocation();
    return hash === '#operations' ? <Navigate to="/operations" replace /> : <WalletOverview />;
}

function WalletOverview() {
    const { data, error, isPending, refetch } = useBffWallets({ recentOperations: recentLimit });
    const wallets = !error && data?.status === 200 ? (data.data.wallets ?? []) : undefined;
    const { hidden } = useAmountVisibility();
    const hasWallet = wallets && wallets.length > 0;

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
                {hasWallet && <AmountVisibilityButton />}
            </div>
            {isPending && <LoadingState label="Загружаем кошельки…" />}
            {error && <ErrorState error={error} onRetry={() => refetch()} />}
            {hasWallet === false && (
                <Paper
                    component="section"
                    aria-labelledby="empty-wallet-heading"
                    elevation={0}
                    className="p-5 sm:p-8"
                    sx={{ bgcolor: 'primary.light', borderRadius: 3 }}
                >
                    <Typography id="empty-wallet-heading" component="h2" variant="h5">
                        Кошелька пока нет
                    </Typography>
                    <Typography color="text.secondary" className="mt-2 mb-5">
                        Можно открыть один рублёвый кошелёк с нулевым балансом.
                    </Typography>
                    <CreateWalletButton />
                </Paper>
            )}
            {hasWallet && (
                <>
                    {wallets.map((wallet, index) => (
                        <WalletCard key={wallet.id ?? index} wallet={wallet} hidden={hidden} />
                    ))}
                    <RecentOperations wallets={wallets} hidden={hidden} limit={recentLimit} />
                </>
            )}
        </section>
    );
}
