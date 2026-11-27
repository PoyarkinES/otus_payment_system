import Button from '@mui/material/Button';
import { useState } from 'react';
import { useNavigate } from 'react-router';

import { ensureFreshSession, getCurrentRefreshToken } from '@/shared/api/auth/session';
import { getSnapshot } from '@/shared/api/auth/storage';
import { logout } from '@/shared/api/generated/identity/endpoints/auth/auth';
import { useAuth } from '@/shared/auth/AuthContext';

export function LogoutButton() {
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
        <Button
            type="button"
            variant="outlined"
            onClick={handleLogout}
            disabled={loggingOut}
            sx={{ minHeight: 44 }}
        >
            {loggingOut ? 'Выходим…' : 'Выйти'}
        </Button>
    );
}
