import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { Link, useLocation, useNavigate } from 'react-router';
import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { z } from 'zod';

import { login } from '@/shared/api/generated/identity/endpoints/auth/auth';
import { useAuth } from '@/shared/auth/AuthContext';
import { ApiProblem } from '@/shared/api/problem';
import type { AuthTokens } from '@/shared/api/generated/identity/model';

const loginSchema = z.object({
    email: z.string().email('Некорректный формат email').max(320),
    password: z.string().min(1, 'Введите пароль'),
});

type LoginFields = z.infer<typeof loginSchema>;

/** Фоллбэк для ошибок API без detail — переводит HTTP-статус на русский. */
function describeError(status: number, title?: string): string {
    if (title) return title;
    const messages: Record<number, string> = {
        400: 'Некорректный запрос.',
        401: 'Неверный email или пароль.',
        403: 'Доступ запрещён. Аккаунт может быть заблокирован.',
        408: 'Превышено время ожидания.',
        429: 'Слишком много запросов. Попробуйте позже.',
        500: 'Внутренняя ошибка сервера.',
        502: 'Ошибка шлюза.',
        503: 'Сервис временно недоступен.',
    };
    return messages[status] ?? `Ошибка ${status}.`;
}

/** Страница входа — email + пароль, валидация, переход на запрошенную страницу после успеха. */
export function LoginPage() {
    const { setTokens } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const queryClient = useQueryClient();
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [errors, setErrors] = useState<Partial<Record<keyof LoginFields, string>>>({});
    const [generalError, setGeneralError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    // Восстанавливаем страницу, на которую пользователь хотел попасть до редиректа на login.
    const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/';

    const handleSubmit = async (e: FormEvent) => {
        e.preventDefault();
        setGeneralError(null);
        setErrors({});

        const result = loginSchema.safeParse({ email, password });
        if (!result.success) {
            const fieldErrors: Partial<Record<keyof LoginFields, string>> = {};
            for (const err of result.error.issues) {
                const field = err.path[0] as keyof LoginFields;
                if (field) {
                    fieldErrors[field] = err.message;
                }
            }
            setErrors(fieldErrors);
            return;
        }

        setLoading(true);
        try {
            const resp = await login({ email: result.data.email, password: result.data.password });
            // apiFetch бросает на 4xx/5xx, поэтому при успехе data — AuthTokens.
            const tokens = resp.data as AuthTokens;
            setTokens(tokens);
            // Очищаем кэш React Query, чтобы данные подгрузились свежими.
            queryClient.invalidateQueries();
            navigate(from, { replace: true });
        } catch (err) {
            if (err instanceof ApiProblem && err.status === 400 && err.errors) {
                const fieldErrors: Partial<Record<keyof LoginFields, string>> = {};
                for (const [key, messages] of Object.entries(err.errors)) {
                    // Бэкенд возвращает camelCase ключи
                    const field = key as keyof LoginFields;
                    if (field in loginSchema.shape) {
                        fieldErrors[field] = messages[0] ?? '';
                    }
                }
                setErrors(fieldErrors);
            } else if (err instanceof ApiProblem) {
                // detail содержит русский текст (DomainExceptionMiddleware), title — ReasonPhrase на английском.
                setGeneralError(err.detail ?? describeError(err.status, err.title));
            } else {
                setGeneralError('Не удалось войти. Проверьте соединение.');
            }
        } finally {
            setLoading(false);
        }
    };

    return (
        <Container maxWidth="sm" className="flex items-center justify-center min-h-[60vh]">
            <Paper elevation={2} className="w-full max-w-md p-8">
                <Typography variant="h4" component="h1" className="mb-6 text-center">
                    Вход
                </Typography>

                <form onSubmit={handleSubmit} className="flex flex-col gap-4">
                    <TextField
                        label="Email"
                        type="email"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        error={!!errors.email}
                        helperText={errors.email}
                        disabled={loading}
                        autoComplete="email"
                        fullWidth
                    />

                    <TextField
                        label="Пароль"
                        type="password"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        error={!!errors.password}
                        helperText={errors.password}
                        disabled={loading}
                        autoComplete="current-password"
                        fullWidth
                    />

                    {generalError && <Alert severity="error">{generalError}</Alert>}

                    <Button type="submit" variant="contained" size="large" disabled={loading}>
                        {loading ? 'Входим…' : 'Войти'}
                    </Button>

                    <Typography variant="body2" className="text-center">
                        Нет аккаунта?{' '}
                        <Link to="/register" className="no-underline">
                            Зарегистрироваться
                        </Link>
                    </Typography>
                </form>
            </Paper>
        </Container>
    );
}
