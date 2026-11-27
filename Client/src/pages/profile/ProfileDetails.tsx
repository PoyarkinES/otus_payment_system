import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';

import type { GetProfileResultStatus } from '@/shared/api/generated/identity/model';

const statusLabels: Record<GetProfileResultStatus, string> = {
    Active: 'Активен',
    Blocked: 'Заблокирован',
};

type ProfileDetailsProps = {
    email?: string | null;
    status: GetProfileResultStatus;
    roleLabel?: string;
    userId?: string;
};

function Detail({ label, value }: { label: string; value: string }) {
    return (
        <div className="flex min-w-0 flex-col gap-1">
            <Typography component="dt" variant="body2" color="text.secondary">
                {label}
            </Typography>
            <Typography component="dd" className="m-0 wrap-anywhere">
                {value}
            </Typography>
        </div>
    );
}

export function ProfileDetails({ email, status, roleLabel, userId }: ProfileDetailsProps) {
    return (
        <Paper component="dl" variant="outlined" className="m-0 grid gap-5 p-5 sm:grid-cols-2">
            <Detail label="Email" value={email || 'Не указан'} />
            {roleLabel && <Detail label="Роль" value={roleLabel} />}
            <Detail
                label="Статус аккаунта"
                value={Object.hasOwn(statusLabels, status) ? statusLabels[status] : 'Неизвестен'}
            />
            {userId && <Detail label="UUID пользователя" value={userId} />}
        </Paper>
    );
}
