import { useState } from 'react';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import IconButton from '@mui/material/IconButton';
import Paper from '@mui/material/Paper';
import SvgIcon from '@mui/material/SvgIcon';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getBffWalletsQueryKey, useBffWallets } from '@/shared/api/generated/endpoints/bff/bff';
import { isApiProblem } from '@/shared/api/problem';
import { createRubWallet } from '@/shared/api/wallets';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';
import { RecentOperations } from './RecentOperations';
import { WalletCard } from './WalletCard';

export function WalletsPage() {
    const { data, error, isPending, refetch } = useBffWallets();
    const wallets = !error && data?.status === 200 ? (data.data.wallets ?? []) : undefined;
    const hasRubWallet = wallets?.some((wallet) => wallet.currency === 'RUB');
    const existingWalletMessage = 'Рублёвый кошелёк уже существует.';
    const [hidden, setHidden] = useState(false);
    const [dialogOpen, setDialogOpen] = useState(false);
    const queryClient = useQueryClient();
    const refreshAfterCreation = () => {
        setDialogOpen(false);
        return queryClient.invalidateQueries({ queryKey: getBffWalletsQueryKey() });
    };
    const create = useMutation({
        mutationFn: createRubWallet,
        onSuccess: refreshAfterCreation,
        onError: (problem) => {
            if (isApiProblem(problem) && problem.status === 409) return refreshAfterCreation();
        },
    });
    const duplicate = isApiProblem(create.error) && create.error.status === 409;
    const privacyLabel = hidden ? 'Показать суммы' : 'Скрыть суммы';

    return (
        <section className="grid min-w-0 gap-4 sm:gap-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <Typography
                    variant="h3"
                    component="h1"
                    sx={{
                        fontSize: { xs: '2rem', sm: '2.75rem' },
                    }}
                >
                    Кошельки
                </Typography>
                <div className="flex items-center gap-2">
                    {wallets && wallets.length > 0 && (
                        <Tooltip title={privacyLabel}>
                            <IconButton
                                aria-label={privacyLabel}
                                onClick={() => setHidden(!hidden)}
                            >
                                <SvgIcon
                                    fontSize="small"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    sx={{ fill: 'none', stroke: 'currentColor', strokeWidth: 1.7 }}
                                >
                                    <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" />
                                    <circle cx="12" cy="12" r="3" />
                                    {hidden && <path d="m3 3 18 18" />}
                                </SvgIcon>
                            </IconButton>
                        </Tooltip>
                    )}
                    <Tooltip title={hasRubWallet ? existingWalletMessage : ''} describeChild>
                        <span className="inline-flex" tabIndex={hasRubWallet ? 0 : undefined}>
                            <Button
                                variant="outlined"
                                disabled={!wallets || hasRubWallet || create.isPending}
                                aria-description={hasRubWallet ? existingWalletMessage : undefined}
                                startIcon={<span aria-hidden>＋</span>}
                                onClick={() => {
                                    create.reset();
                                    setDialogOpen(true);
                                }}
                            >
                                Создать кошелёк
                            </Button>
                        </span>
                    </Tooltip>
                </div>
            </div>
            {isPending && <LoadingState label="Загружаем кошельки…" />}
            {error && <ErrorState error={error} onRetry={() => refetch()} />}
            {duplicate && <Alert severity="info">{existingWalletMessage}</Alert>}
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
                            sx={{
                                bgcolor: 'primary.light',
                                borderRadius: 3,
                            }}
                        >
                            <Typography component="h2" variant="h5" sx={{ fontWeight: 700, mb: 1 }}>
                                Кошельков пока нет
                            </Typography>
                            <Typography color="text.secondary">
                                Создайте первый кошелёк в рублях.
                            </Typography>
                        </Paper>
                    )}
                    <RecentOperations wallets={wallets} hidden={hidden} />
                </>
            )}
            <Dialog
                open={dialogOpen}
                onClose={create.isPending ? undefined : () => setDialogOpen(false)}
                aria-labelledby="create-wallet-title"
                maxWidth="xs"
                fullWidth
            >
                <DialogTitle id="create-wallet-title">Создать кошелёк</DialogTitle>
                <DialogContent className="grid gap-4">
                    <div>
                        <Typography variant="body2" color="text.secondary">
                            Валюта кошелька
                        </Typography>
                        <Typography>Российский рубль (RUB)</Typography>
                    </div>
                    {hasRubWallet && dialogOpen && (
                        <Alert severity="info">{existingWalletMessage}</Alert>
                    )}
                    {create.error && !duplicate && <ErrorState error={create.error} />}
                </DialogContent>
                <DialogActions>
                    <Button disabled={create.isPending} onClick={() => setDialogOpen(false)}>
                        Отмена
                    </Button>
                    <Button
                        disabled={create.isPending || hasRubWallet}
                        variant="contained"
                        onClick={() => create.mutate()}
                    >
                        {create.isPending ? 'Создаём…' : 'Создать'}
                    </Button>
                </DialogActions>
            </Dialog>
        </section>
    );
}
