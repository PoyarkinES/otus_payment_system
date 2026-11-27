import Button from '@mui/material/Button';
import Box from '@mui/material/Box';
import SvgIcon from '@mui/material/SvgIcon';
import Typography from '@mui/material/Typography';
import { Link, Outlet, useLocation, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';

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
];

export function AppLayout() {
    const { clearTokens } = useAuth();
    const navigate = useNavigate();
    const { pathname, hash } = useLocation();
    const queryClient = useQueryClient();
    const [loggingOut, setLoggingOut] = useState(false);

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
                className="flex flex-col gap-4 bg-background-paper px-4 py-4 md:sticky md:top-0 md:h-screen md:w-60 md:shrink-0 md:gap-10 md:p-6"
                sx={{
                    borderColor: 'divider',
                    borderStyle: 'solid',
                    borderWidth: { xs: '0 0 1px', md: '0 1px 0 0' },
                }}
            >
                <Box className="flex items-center justify-between gap-3">
                    <Typography
                        component={Link}
                        to="/wallets"
                        variant="h5"
                        className="flex items-center gap-2 text-inherit no-underline"
                        sx={{ fontWeight: 750, letterSpacing: '-0.04em' }}
                    >
                        <Box
                            component="span"
                            aria-hidden="true"
                            sx={{
                                width: 27,
                                height: 20,
                                color: 'primary.main',
                                display: 'flex',
                                gap: '2px',
                                '&::before, &::after': {
                                    content: '""',
                                    flex: 1,
                                    bgcolor: 'currentColor',
                                    borderRadius: '100% 0',
                                },
                            }}
                        />
                        Otus Pay
                    </Typography>
                </Box>
                <Box
                    component="nav"
                    aria-label="Основная навигация"
                    className="flex flex-wrap gap-1 md:flex-col"
                >
                    {sections.map(({ to, label, icon }) => {
                        const selected = pathname + hash === to;
                        return (
                            <Button
                                key={to}
                                component={Link}
                                to={to}
                                aria-current={selected ? 'page' : undefined}
                                startIcon={
                                    <SvgIcon>
                                        <path
                                            d={icon}
                                            fill="none"
                                            stroke="currentColor"
                                            strokeWidth="1.6"
                                            strokeLinecap="round"
                                            strokeLinejoin="round"
                                        />
                                    </SvgIcon>
                                }
                                sx={{
                                    justifyContent: 'flex-start',
                                    color: 'text.primary',
                                    bgcolor: selected ? 'primary.light' : undefined,
                                    px: { xs: 1, md: 2 },
                                    py: 1,
                                    fontWeight: selected ? 650 : 500,
                                }}
                            >
                                {label}
                            </Button>
                        );
                    })}
                </Box>
                <Box className="flex gap-1 md:mt-auto md:flex-col md:items-start">
                    <Button
                        component={Link}
                        size="small"
                        to="/"
                        aria-current={pathname === '/' ? 'page' : undefined}
                    >
                        Статус системы
                    </Button>
                    <Button onClick={handleLogout} disabled={loggingOut}>
                        {loggingOut ? 'Выходим…' : 'Выйти'}
                    </Button>
                </Box>
            </Box>
            <Box
                component="main"
                className="mx-auto w-full min-w-0 max-w-7xl flex-1 px-4 py-6 sm:px-8 md:py-10"
            >
                <Outlet />
            </Box>
        </Box>
    );
}
