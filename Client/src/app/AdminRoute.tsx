import Alert from '@mui/material/Alert';
import { Outlet } from 'react-router';

import { CurrentAccount } from '@/pages/profile/CurrentAccount';

export function AdminRoute() {
    return (
        <CurrentAccount>
            {({ isActiveAdmin }) =>
                isActiveAdmin ? <Outlet /> : <Alert severity="warning">Недостаточно прав</Alert>
            }
        </CurrentAccount>
    );
}
