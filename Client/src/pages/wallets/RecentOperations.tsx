import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import { amountTypography } from '@/shared/theme/theme';

import type { BffWalletResult } from '@/shared/api/generated/model';
import { formatAmount } from './formatAmount';

const operationTypes: Readonly<
    Partial<Record<string, { label: string; icon: string; sign: string }>>
> = {
    TopUp: { label: 'Пополнение', icon: '+', sign: '+' },
    Withdrawal: { label: 'Списание', icon: '−', sign: '−' },
    TransferIn: { label: 'Входящий перевод', icon: '↙', sign: '+' },
    TransferOut: { label: 'Исходящий перевод', icon: '↗', sign: '−' },
    Fee: { label: 'Комиссия', icon: '−', sign: '−' },
    Refund: { label: 'Возврат', icon: '↩', sign: '+' },
};

const date = new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', year: 'numeric' });

export function RecentOperations({
    wallets,
    hidden,
}: {
    wallets: BffWalletResult[];
    hidden: boolean;
}) {
    const operations = wallets
        .flatMap((wallet) =>
            (wallet.recentOperations ?? []).map((operation) => ({
                ...operation,
                currency: operation.currency ?? wallet.currency,
                timestamp: Date.parse(operation.createdAt ?? ''),
            })),
        )
        .sort(
            (a, b) =>
                (Number.isFinite(b.timestamp) ? b.timestamp : -Infinity) -
                (Number.isFinite(a.timestamp) ? a.timestamp : -Infinity),
        );

    return (
        <Paper
            component="section"
            elevation={0}
            id="operations"
            tabIndex={-1}
            aria-labelledby="operations-heading"
            className="scroll-mt-4 p-5 sm:p-7"
            sx={{ borderRadius: 3 }}
        >
            <Typography
                component="h2"
                id="operations-heading"
                variant="h5"
                className="mb-3"
                sx={{ fontWeight: 700 }}
            >
                Последние операции
            </Typography>
            {operations.length === 0 ? (
                <Typography color="text.secondary">Операций пока нет</Typography>
            ) : (
                <ul className="m-0 list-none divide-y divide-divider p-0">
                    {operations.map((operation, index) => {
                        const type = operation.type ? operationTypes[operation.type] : undefined;
                        return (
                            <Box
                                component="li"
                                key={operation.id ?? index}
                                className="grid items-center gap-2 py-3 sm:gap-3"
                                sx={{
                                    gridTemplateColumns: {
                                        xs: 'auto minmax(0, 1fr) minmax(0, 0.85fr)',
                                        sm: 'auto minmax(0, 1fr) minmax(0, 0.7fr) minmax(0, 0.7fr)',
                                    },
                                }}
                            >
                                <Box
                                    aria-hidden
                                    className="grid place-items-center"
                                    sx={{
                                        width: 34,
                                        height: 34,
                                        bgcolor: 'action.selected',
                                        borderRadius: 1,
                                        fontSize: '1.25rem',
                                        color: 'primary.dark',
                                    }}
                                >
                                    {type?.icon ?? '·'}
                                </Box>
                                <div className="min-w-0 sm:contents">
                                    <Typography sx={{ fontWeight: 600, overflowWrap: 'anywhere' }}>
                                        {type?.label ?? operation.type ?? 'Операция'}
                                    </Typography>
                                    <Typography variant="body2" color="text.secondary">
                                        {Number.isFinite(operation.timestamp) ? (
                                            <time dateTime={operation.createdAt}>
                                                {date.format(operation.timestamp)}
                                            </time>
                                        ) : (
                                            '—'
                                        )}
                                    </Typography>
                                </div>
                                <Typography
                                    className="text-right"
                                    sx={{
                                        ...amountTypography,
                                        fontWeight: 600,
                                        overflowWrap: 'anywhere',
                                        color: type?.sign === '+' ? 'success.dark' : 'text.primary',
                                    }}
                                >
                                    {hidden
                                        ? '••••'
                                        : formatAmount(
                                              operation.amount,
                                              operation.currency,
                                              type?.sign,
                                          )}
                                </Typography>
                            </Box>
                        );
                    })}
                </ul>
            )}
        </Paper>
    );
}
