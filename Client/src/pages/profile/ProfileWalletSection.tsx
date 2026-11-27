import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import type { ReactNode } from 'react';

import { Detail } from '@/pages/profile/ProfileDetails';
import { useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import { CreateWalletButton } from '@/shared/ui/CreateWalletButton';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

export function ProfileWalletSection() {
    const { data, error, isPending, refetch } = useBffWallets({ recentOperations: 0 });
    const wallets = data?.status === 200 ? data.data?.wallets : undefined;

    let content: ReactNode;
    if (isPending) {
        content = <LoadingState label="Загружаем кошелёк…" />;
    } else if (error || !wallets) {
        content = <ErrorState error={error} onRetry={() => refetch()} />;
    } else if (wallets.length === 0) {
        content = (
            <>
                <Typography color="text.secondary">
                    Для этого аккаунта можно открыть один кошелёк в рублях (RUB).
                </Typography>
                <CreateWalletButton />
            </>
        );
    } else {
        content = wallets.map((wallet, index) => (
            <dl key={wallet.id ?? index} className="m-0 flex flex-wrap gap-x-10 gap-y-4">
                <Detail label="Валюта" value={wallet.currency || '—'} />
                {wallet.id && <Detail label="Номер кошелька" value={wallet.id} />}
            </dl>
        ));
    }

    return (
        <Paper
            component="section"
            aria-labelledby="profile-wallet-heading"
            variant="outlined"
            className="grid min-w-0 gap-4 p-5 sm:p-6"
        >
            <Typography component="h2" id="profile-wallet-heading" variant="h5">
                Кошелёк
            </Typography>
            {content}
        </Paper>
    );
}
