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
                sx={{ mb: 1, fontSize: { xs: '1.25rem', sm: '1.5rem' }, fontWeight: 700 }}
            >
                Последние операции
            </Typography>
            {operations.length ? (
                <>
                    <OperationList operations={operations} hidden={hidden} />
                    <div className="mt-2 flex justify-center border-t border-divider pt-4">
                        <Button
                            component={Link}
                            to="/operations"
                            variant="outlined"
                            endIcon={<span aria-hidden>→</span>}
                            sx={{ width: { xs: '100%', sm: 'auto' }, minHeight: 44, px: 3 }}
                        >
                            Посмотреть всю историю
                        </Button>
                    </div>
                </>
            ) : (
                <Typography color="text.secondary">Операций пока нет</Typography>
            )}
        </Paper>
    );
}
