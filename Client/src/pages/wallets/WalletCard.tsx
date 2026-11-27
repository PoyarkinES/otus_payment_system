import { useId } from 'react';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';

import type { BffWalletResult } from '@/shared/api/generated/model';
import { amountTypography } from '@/shared/theme/theme';
import { formatAmount } from './formatAmount';

type WalletCardProps = { wallet: BffWalletResult; hidden: boolean };

export function WalletCard({ wallet, hidden }: WalletCardProps) {
    const titleId = useId();
    const title =
        wallet.currency === 'RUB' ? 'Рублёвый кошелёк' : `Кошелёк ${wallet.currency ?? ''}`.trim();

    return (
        <Paper
            component="article"
            aria-labelledby={titleId}
            elevation={0}
            className="relative overflow-hidden p-5 sm:p-8"
            sx={{
                bgcolor: 'primary.light',
                borderRadius: 3,
                '&::before': {
                    content: '""',
                    position: 'absolute',
                    right: 0,
                    top: 0,
                    width: '42%',
                    height: '70%',
                    color: 'primary.main',
                    opacity: 0.14,
                    pointerEvents: 'none',
                    backgroundImage:
                        'repeating-radial-gradient(ellipse at 90% -50%, transparent 0 10px, currentColor 11px, transparent 12px)',
                    maskImage: 'linear-gradient(215deg, black, transparent 78%)',
                },
            }}
        >
            <div className="relative mb-6 flex flex-wrap items-center gap-3">
                <Typography id={titleId} component="h2" variant="h5">
                    {title}
                </Typography>
                {wallet.currency === 'RUB' && (
                    <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ letterSpacing: '0.06em' }}
                    >
                        RUB
                    </Typography>
                )}
                {wallet.status === 'Frozen' && <Typography variant="body2">Заморожен</Typography>}
                {wallet.status === 'Closed' && <Typography variant="body2">Закрыт</Typography>}
            </div>
            <dl className="relative m-0 tabular-nums">
                <div>
                    <Typography component="dt" color="text.secondary">
                        Доступно
                    </Typography>
                    <Typography
                        component="dd"
                        className="mx-0 my-1"
                        sx={{
                            ...amountTypography,
                            fontSize: { xs: '2.45rem', sm: '3.7rem' },
                            fontWeight: 630,
                            letterSpacing: '-0.03em',
                            lineHeight: 1.15,
                            overflowWrap: 'anywhere',
                        }}
                    >
                        {hidden ? '••••' : formatAmount(wallet.available, wallet.currency)}
                    </Typography>
                </div>
                <div className="mt-3 flex flex-wrap items-baseline gap-x-2.5">
                    <Typography component="dt" color="text.secondary">
                        В резерве
                    </Typography>
                    <Typography
                        component="dd"
                        className="m-0"
                        sx={{ ...amountTypography, fontWeight: 550, overflowWrap: 'anywhere' }}
                    >
                        {hidden ? '••••' : formatAmount(wallet.held, wallet.currency)}
                    </Typography>
                </div>
            </dl>
        </Paper>
    );
}
