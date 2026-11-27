import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router';

import { CurrentAccount } from '@/pages/profile/CurrentAccount';
import { LogoutButton } from '@/pages/profile/LogoutButton';
import { ProfileDetails } from '@/pages/profile/ProfileDetails';
import { ProfileWalletSection } from '@/pages/profile/ProfileWalletSection';

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
        <section className="grid min-w-0 gap-4 sm:gap-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
                <Typography
                    variant="h3"
                    component="h1"
                    sx={{ fontSize: { xs: '2rem', sm: '2.75rem' } }}
                >
                    Профиль
                </Typography>
                <LogoutButton />
            </div>
            <CurrentAccount>
                {({ claims, status, isActiveAdmin }) => (
                    <>
                        <ProfileDetails
                            email={claims.email}
                            roleLabel={roleLabel(claims.role)}
                            status={status}
                        />
                        {status === 'Active' && <ProfileWalletSection />}
                        {isActiveAdmin && (
                            <nav aria-label="Администрирование" className="flex flex-wrap gap-3">
                                <Button component={Link} to="/admin/users" variant="outlined">
                                    Поиск пользователя
                                </Button>
                                <Button component={Link} to="/admin/status" variant="outlined">
                                    Статус системы
                                </Button>
                            </nav>
                        )}
                    </>
                )}
            </CurrentAccount>
        </section>
    );
}
