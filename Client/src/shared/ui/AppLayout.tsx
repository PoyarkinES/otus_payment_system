import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import SvgIcon from '@mui/material/SvgIcon';
import Typography from '@mui/material/Typography';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState, type FormEvent } from 'react';

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
        to: '/operations',
        label: 'Операции',
        icon: 'M3 7h17m-4-4 4 4-4 4M21 17H4m4 4-4-4 4-4',
    },
];

export function AppLayout() {
    const { clearTokens } = useAuth();
    const navigate = useNavigate();
    const { pathname, hash } = useLocation();
    const queryClient = useQueryClient();
    const [loggingOut, setLoggingOut] = useState(false);

    useEffect(() => {
        if (!hash) document.scrollingElement?.scrollTo({ top: 0 });
    }, [pathname, hash]);

    const handleLogout = async (e: FormEvent) => {
        e.preventDefault();
        if (loggingOut) return;
        setLoggingOut(true);
        try {
            // Перед logout обновляем сессию, если access истёк.
            // По архитектуре проекта logout требует валидного access-токена (RequireAuthorization).
            // Если refresh не удался — токены уже очищены, отзывать нечего.
            const fresh = await ensureFreshSession();
            if (fresh) {
                // Получаем актуальный refresh-токен после обновления сессии.
                const rt = getCurrentRefreshToken();
                if (rt) {
                    await logout({ refreshToken: rt });
                }
            }
        } catch {
            // Если logout упал — всё равно очищаем токены локально.
        } finally {
            // Очищаем кэш React Query, чтобы при входе другим пользователем
            // не показались кратковременно чужие данные.
            queryClient.clear();
            clearTokens();
            navigate('/login', { replace: true });
        }
    };

    return (
        <Box className="min-h-screen md:flex">
            <Box
                component="aside"
                className="flex items-center justify-center bg-linear-to-b from-primary-light to-background-default px-4 pt-[env(safe-area-inset-top,0px)] md:sticky md:top-0 md:h-screen md:w-60 md:shrink-0 md:flex-col md:items-stretch md:justify-start md:gap-10 md:border-r md:border-divider md:bg-background-paper md:bg-none md:p-6"
            >
                <Typography
                    component={Link}
                    to="/wallets"
                    variant="h5"
                    className="inline-flex min-h-14 items-center gap-2 text-inherit no-underline md:min-h-11"
                    sx={{
                        fontSize: { xs: '1.1875rem', md: '1.5rem' },
                        fontWeight: 750,
                        letterSpacing: '-0.04em',
                    }}
                >
                    <img
                        src="/otus-pay-mark.svg"
                        width="28"
                        height="28"
                        className="size-6 md:size-7"
                        alt=""
                    />
                    Otus Pay
                </Typography>
                <Box
                    component="nav"
                    aria-label="Основная навигация"
                    className="fixed left-1/2 z-10 flex -translate-x-1/2 items-center gap-1 rounded-full border border-divider bg-background-paper p-1 md:static md:z-auto md:flex-1 md:translate-x-0 md:flex-col md:items-stretch md:rounded-none md:border-0 md:bg-transparent md:p-0"
                    sx={(theme) => ({
                        bottom: 'calc(0.75rem + env(safe-area-inset-bottom, 0px))',
                        boxShadow: { xs: theme.shadows[3], md: 'none' },
                    })}
                >
                    {sections.map(({ to, label, icon }) => {
                        const selected = pathname === to;
                        return (
                            <Button
                                key={to}
                                component={Link}
                                to={to}
                                aria-label={label}
                                aria-current={selected ? 'page' : undefined}
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
                    <Button
                        onClick={handleLogout}
                        disabled={loggingOut}
                        className="min-h-11 rounded-full md:mt-auto md:self-start"
                    >
                        {loggingOut ? 'Выходим…' : 'Выйти'}
                    </Button>
                </Box>
            </Box>
            <Box
                component="main"
                className="mx-auto w-full min-w-0 max-w-7xl flex-1 px-4 pt-4 pb-[calc(5.5rem+env(safe-area-inset-bottom,0px))] sm:px-8 md:py-10"
            >
                <Outlet />
            </Box>
        </Box>
    );
}
