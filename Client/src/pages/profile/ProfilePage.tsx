import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router';

import { CurrentAccount } from '@/pages/profile/CurrentAccount';
import { ProfileDetails } from '@/pages/profile/ProfileDetails';

function roleLabel(role?: string | null): string {
    switch (role) {
        case 'Admin':
            return 'Администратор';
        case 'User':
            return 'Пользователь';
        default:
            return 'Неизвестна';
    }
}

export function ProfilePage() {
    return (
        <section className="flex flex-col gap-4">
            <Typography variant="h4" component="h1">
                Профиль
            </Typography>
            <CurrentAccount>
                {({ claims, status, canLookUpUsers }) => (
                    <>
                        <ProfileDetails
                            email={claims.email}
                            roleLabel={roleLabel(claims.role)}
                            status={status}
                        />
                        {canLookUpUsers && (
                            <Button
                                component={Link}
                                to="/admin/users"
                                variant="outlined"
                                className="self-start"
                            >
                                Поиск пользователя
                            </Button>
                        )}
                    </>
                )}
            </CurrentAccount>
        </section>
    );
}
