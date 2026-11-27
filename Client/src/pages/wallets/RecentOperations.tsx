import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router';

import type { BffWalletResult } from '@/shared/api/generated/model';
import { OperationList } from './OperationList';

export function RecentOperations({
    wallets,
    hidden,
    limit,
}: {
    wallets: BffWalletResult[];
    hidden: boolean;
    limit: number;
}) {
    const operations = wallets
        .flatMap((wallet) =>
            (wallet.recentOperations ?? []).map((operation) => ({
                ...operation,
                currency: operation.currency ?? wallet.currency,
            })),
        )
        .sort((a, b) => {
            const first = Date.parse(a.createdAt ?? '');
            const second = Date.parse(b.createdAt ?? '');
            return (
                (Number.isFinite(second) ? second : -Infinity) -
                (Number.isFinite(first) ? first : -Infinity)
            );
        })
        .slice(0, limit);

    return (
        <Paper
            component="section"
            elevation={0}
            id="operations"
            aria-labelledby="operations-heading"
            className="p-5 sm:p-7"
            sx={{ borderRadius: 3 }}
        >
            <Typography
                id="operations-heading"
                component="h2"
                variant="h5"
                sx={{ mb: 1.5, fontWeight: 700 }}
            >
                Последние операции
            </Typography>
            {operations.length ? (
                <OperationList operations={operations} hidden={hidden} />
            ) : (
                <Typography color="text.secondary">Операций пока нет</Typography>
            )}
            {wallets.length > 0 && (
                <div className="mt-3 border-t border-divider pt-2">
                    <Button
                        component={Link}
                        to="/operations"
                        endIcon={<span aria-hidden>→</span>}
                        sx={{ minHeight: 44 }}
                    >
                        Все операции
                    </Button>
                </div>
            )}
        </Paper>
    );
}
