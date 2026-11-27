import { useNavigate } from 'react-router';
import { useState } from 'react';
import { z } from 'zod';

import { login, register } from '@/shared/api/generated/identity/endpoints/auth/auth';
import { useAuth } from '@/shared/auth/AuthContext';
import { AuthForm } from '@/shared/ui/AuthForm';
import { ApiProblem } from '@/shared/api/problem';
import type { AuthTokens } from '@/shared/api/generated/identity/model';

/**
 * Валидация пароля дублирует правила бэкенда (RegisterRequestValidator):
 * 8–128 символов, минимум одна буква и одна цифра.
 */
const registerSchema = z.object({
    email: z.string().email('Некорректный формат email').max(320),
    password: z
        .string()
        .min(8, 'Пароль должен быть не короче 8 символов')
        .max(128, 'Пароль должен быть не длиннее 128 символов')
        .regex(/[A-Za-z]/, 'Пароль должен содержать хотя бы одну букву')
        .regex(/[0-9]/, 'Пароль должен содержать хотя бы одну цифру'),
});

type RegisterFields = z.infer<typeof registerSchema>;

/** Фоллбэк для ошибок API без detail — переводит HTTP-статус на русский. */
function describeError(status: number, title?: string): string {
    if (title) return title;
    const messages: Record<number, string> = {
        400: 'Некорректный запрос.',
        401: 'Не авторизован.',
        403: 'Доступ запрещён.',
        408: 'Превышено время ожидания.',
        429: 'Слишком много запросов. Попробуйте позже.',
        500: 'Внутренняя ошибка сервера.',
        502: 'Ошибка шлюза.',
        503: 'Сервис временно недоступен.',
    };
    return messages[status] ?? `Ошибка ${status}.`;
}

/** Страница регистрации — email + пароль, валидация, переход на главную после успеха. */
export function RegisterPage() {
    const { setTokens } = useAuth();
    const navigate = useNavigate();
    const [errors, setErrors] = useState<Partial<Record<keyof RegisterFields, string>>>({});
    const [generalError, setGeneralError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);

    const handleSubmit = async (fields: RegisterFields) => {
        setGeneralError(null);
        setErrors({});

        const result = registerSchema.safeParse(fields);
        if (!result.success) {
            const fieldErrors: Partial<Record<keyof RegisterFields, string>> = {};
            for (const err of result.error.issues) {
                const field = err.path[0] as keyof RegisterFields;
                if (field) {
                    fieldErrors[field] = err.message;
                }
            }
            setErrors(fieldErrors);
            return;
        }

        setLoading(true);
        try {
            // Регистрация создаёт аккаунт (201 + RegisterResult), но не возвращает токены.
            // Сразу после регистрации входим теми же данными для получения токенов.
            await register({ email: result.data.email, password: result.data.password });
            const loginResp = await login({
                email: result.data.email,
                password: result.data.password,
            });
            // apiFetch бросает на 4xx/5xx, поэтому при успехе data — AuthTokens.
            const tokens = loginResp.data as AuthTokens;
            setTokens(tokens);
            navigate('/wallets', { replace: true });
        } catch (err) {
            if (err instanceof ApiProblem && err.status === 400 && err.errors) {
                const fieldErrors: Partial<Record<keyof RegisterFields, string>> = {};
                for (const [key, messages] of Object.entries(err.errors)) {
                    const field = key as keyof RegisterFields;
                    if (field in registerSchema.shape) {
                        fieldErrors[field] = messages[0] ?? '';
                    }
                }
                setErrors(fieldErrors);
            } else if (err instanceof ApiProblem && err.status === 409) {
                setGeneralError('Пользователь с таким email уже зарегистрирован.');
            } else if (err instanceof ApiProblem) {
                // detail содержит русский текст (DomainExceptionMiddleware), title — ReasonPhrase на английском.
                setGeneralError(err.detail ?? describeError(err.status, err.title));
            } else {
                setGeneralError('Не удалось зарегистрироваться. Проверьте соединение.');
            }
        } finally {
            setLoading(false);
        }
    };

    return (
        <AuthForm
            mode="register"
            errors={errors}
            generalError={generalError}
            loading={loading}
            onSubmit={handleSubmit}
        />
    );
}
