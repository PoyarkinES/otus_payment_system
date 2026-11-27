import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import type { BffWalletOperationResult } from '@/shared/api/generated/model';
import { amountTypography, incomingOperationColors } from '@/shared/theme/theme';
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

export function OperationRow({
    operation,
    hidden,
}: {
    operation: BffWalletOperationResult;
    hidden: boolean;
}) {
    const timestamp = Date.parse(operation.createdAt ?? '');
    const type = operation.type ? operationTypes[operation.type] : undefined;
    const incoming = type?.sign === '+';
    return (
        <Box
            className="grid items-center gap-2 py-3 sm:gap-3"
            sx={{
                gridTemplateColumns: {
                    xs: 'auto minmax(0, 1fr) fit-content(44%)',
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
                    bgcolor: incoming ? incomingOperationColors.background : 'action.selected',
                    borderRadius: 1,
                    fontSize: '1.25rem',
                    color: incoming ? incomingOperationColors.foreground : 'primary.dark',
                }}
            >
                {type?.icon ?? '·'}
            </Box>
            <div className="min-w-0 sm:contents">
                <div className="min-w-0">
                    <Typography sx={{ fontWeight: 600, overflowWrap: 'anywhere' }}>
                        {type?.label ?? operation.type ?? 'Операция'}
                    </Typography>
                    {operation.description && (
                        <Typography
                            variant="body2"
                            color="text.secondary"
                            sx={{ overflowWrap: 'anywhere' }}
                        >
                            {operation.description}
                        </Typography>
                    )}
                </div>
                <Typography variant="body2" color="text.secondary">
                    {Number.isFinite(timestamp) ? (
                        <time dateTime={operation.createdAt}>{date.format(timestamp)}</time>
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
                    color: incoming ? incomingOperationColors.foreground : 'text.primary',
                }}
            >
                {hidden ? '••••' : formatAmount(operation.amount, operation.currency, type?.sign)}
            </Typography>
        </Box>
    );
}

export function OperationList({
    operations,
    hidden,
}: {
    operations: BffWalletOperationResult[];
    hidden: boolean;
}) {
    return (
        <ul className="m-0 list-none divide-y divide-divider p-0">
            {operations.map((operation, index) => (
                <li key={operation.id ?? index}>
                    <OperationRow operation={operation} hidden={hidden} />
                </li>
            ))}
        </ul>
    );
}
