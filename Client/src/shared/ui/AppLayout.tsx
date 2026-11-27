import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { Link, Outlet, useNavigate } from 'react-router';
import { useState } from 'react';

import { getSnapshot } from '@/shared/api/auth/storage';
import { logout } from '@/shared/api/generated/identity/endpoints/auth/auth';
import { ensureFreshSession, getCurrentRefreshToken } from '@/shared/api/auth/session';
import { useAuth } from '@/shared/auth/AuthContext';

/** Общий каркас всех страниц: шапка с названием и кнопкой выхода, область контента. */
export function AppLayout() {
    const { clearTokens } = useAuth();
    const navigate = useNavigate();
    const [loggingOut, setLoggingOut] = useState(false);

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
        <div className="flex min-h-screen flex-col">
            <AppBar position="static" elevation={0}>
                <Toolbar>
                    <Typography
                        variant="h6"
                        component={Link}
                        to="/"
                        className="text-inherit no-underline"
                    >
                        Otus Payment System
                    </Typography>
                    <Box className="ml-auto flex items-center gap-4">
                        <Button component={Link} to="/profile" color="inherit">
                            Профиль
                        </Button>
                        <Button color="inherit" onClick={handleLogout} disabled={loggingOut}>
                            {loggingOut ? 'Выходим…' : 'Выйти'}
                        </Button>
                    </Box>
                </Toolbar>
            </AppBar>
            <Container component="main" maxWidth="md" className="flex-1 py-8">
                <Outlet />
            </Container>
        </div>
    );
}
