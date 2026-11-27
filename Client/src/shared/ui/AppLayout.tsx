import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import SvgIcon from '@mui/material/SvgIcon';
import Typography from '@mui/material/Typography';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import { useEffect, useState } from 'react';

import { getSnapshot } from '@/shared/api/auth/storage';

import { logout } from '@/shared/api/generated/identity/endpoints/auth/auth';
import { ensureFreshSession, getCurrentRefreshToken } from '@/shared/api/auth/session';
import { useAuth } from '@/shared/auth/AuthContext';

const sections = [
    {
        to: '/wallets',
        label: 'Кошельки',
        icon: 'M3 7h16a2 2 0 0 1 2 2v10H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h12v3 M21 12h-5v3h5',
    },
    {
        to: '/wallets#operations',
        label: 'Операции',
        icon: 'M3 7h17m-4-4 4 4-4 4M21 17H4m4 4-4-4 4-4',
    },
    {
        to: '/profile',
        label: 'Профиль',
        icon: 'M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM4 21v-2a8 8 0 0 1 16 0v2Z',
    },
];
export function AppLayout() {
    const { clearTokens } = useAuth();
    const navigate = useNavigate();
    const { pathname, hash } = useLocation();
    const [loggingOut, setLoggingOut] = useState(false);

    useEffect(() => {
        if (!hash) document.scrollingElement?.scrollTo({ top: 0 });
    }, [pathname, hash]);

    const handleLogout = async () => {
        if (loggingOut) return;
        setLoggingOut(true);
        const { sessionId } = getSnapshot();
        try {
            // Перед logout обновляем сессию, если access истёк.
            // По архитектуре проекта logout требует валидного access-токена (RequireAuthorization).
            const fresh = await ensureFreshSession();
            if (fresh && getSnapshot().sessionId === sessionId) {
                // Получаем актуальный refresh-токен после обновления сессии.
                const rt = getCurrentRefreshToken();
                if (rt) {
                    await logout({ refreshToken: rt });
                }
            }
        } catch {
            // Если logout упал — всё равно очищаем токены локально.
        } finally {
            if (getSnapshot().sessionId === sessionId) {
                // Покидаем защищённый экран до очистки токенов, чтобы guard не запомнил
                // страницу предыдущего пользователя как цель следующего входа.
                try {
                    await navigate('/login', { replace: true, flushSync: true });
                } finally {
                    if (getSnapshot().sessionId === sessionId) clearTokens();
                }
            }
        }
    };

    return (
        <Box className="min-h-screen md:flex">
            <Box
                component="aside"
                className="flex items-center justify-between gap-3 border-divider bg-background-paper px-4 py-2 md:sticky md:top-0 md:h-screen md:w-60 md:shrink-0 md:flex-col md:items-stretch md:justify-start md:gap-10 md:p-6"
                sx={{ borderStyle: 'solid', borderWidth: { xs: '0 0 1px', md: '0 1px 0 0' } }}
            >
                <Typography
                    component={Link}
                    to="/wallets"
                    variant="h5"
                    className="flex min-h-11 items-center gap-2 text-inherit no-underline"
                    sx={{ fontWeight: 750, letterSpacing: '-0.04em' }}
                >
                    <img src="/otus-pay-mark.svg" width="28" height="28" alt="" />
                    Otus Pay
                </Typography>
                <Box
                    component="nav"
                    aria-label="Основная навигация"
                    className="fixed left-1/2 z-10 flex -translate-x-1/2 items-center gap-1 rounded-full border border-divider bg-background-paper p-1 md:static md:z-auto md:translate-x-0 md:flex-col md:items-stretch md:rounded-none md:border-0 md:bg-transparent md:p-0"
                    sx={(theme) => ({
                        bottom: 'calc(0.75rem + env(safe-area-inset-bottom, 0px))',
                        boxShadow: { xs: theme.shadows[3], md: 'none' },
                    })}
                >
                    {sections.map(({ to, label, icon }) => {
                        const selected =
                            pathname + hash === to ||
                            (to === '/profile' && pathname.startsWith('/admin/'));
                        return (
                            <Button
                                key={to}
                                component={Link}
                                to={to}
                                aria-label={label}
                                aria-current={selected ? (hash ? 'location' : 'page') : undefined}
                                sx={{
                                    minHeight: 44,
                                    minWidth: { xs: 64, md: 0 },
                                    justifyContent: { xs: 'center', md: 'flex-start' },
                                    borderRadius: { xs: '99px', md: 1 },
                                    px: { xs: 1, md: 2 },
                                    gap: { md: 1.25 },
                                    color: selected ? 'primary.dark' : 'text.secondary',
                                    bgcolor: selected ? 'primary.light' : undefined,
                                    fontWeight: selected ? 650 : 500,
                                    '&:focus-visible': {
                                        outline: '2px solid currentColor',
                                        outlineOffset: -3,
                                    },
                                }}
                            >
                                <SvgIcon>
                                    <path
                                        d={icon}
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="1.7"
                                        strokeLinecap="round"
                                        strokeLinejoin="round"
                                    />
                                </SvgIcon>
                                <span className="hidden md:inline">{label}</span>
                            </Button>
                        );
                    })}
                </Box>
                <Button
                    onClick={handleLogout}
                    disabled={loggingOut}
                    className="min-h-11 md:mt-auto md:self-start"
                >
                    {loggingOut ? 'Выходим…' : 'Выйти'}
                </Button>
            </Box>
            <Box
                component="main"
                className="mx-auto w-full min-w-0 max-w-7xl flex-1 px-4 pt-6 pb-[calc(5.5rem+env(safe-area-inset-bottom,0px))] sm:px-8 md:py-10"
            >
                <Outlet />
            </Box>
        </Box>
    );
}
