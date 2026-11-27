import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Link from '@mui/material/Link';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useState, type FormEvent } from 'react';
import { Link as RouterLink } from 'react-router';

type AuthFormProps = {
    mode: 'login' | 'register';
    errors: { email?: string; password?: string };
    generalError: string | null;
    loading: boolean;
    onSubmit: (fields: { email: string; password: string }) => Promise<void>;
};

const formCopy = {
    login: {
        title: 'Вход',
        submit: 'Войти',
        submitting: 'Входим…',
        prompt: 'Нет аккаунта?',
        link: 'Зарегистрироваться',
        to: '/register',
    },
    register: {
        title: 'Регистрация',
        submit: 'Зарегистрироваться',
        submitting: 'Регистрируем…',
        prompt: 'Уже есть аккаунт?',
        link: 'Войти',
        to: '/login',
    },
};

export function AuthForm({ mode, errors, generalError, loading, onSubmit }: AuthFormProps) {
    const copy = formCopy[mode];
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const passwordHint =
        mode === 'register'
            ? 'От 8 до 128 символов, хотя бы одна латинская буква и одна цифра.'
            : undefined;

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        void onSubmit({ email, password });
    };

    return (
        <main className="grid min-h-svh place-items-center px-3 py-5 sm:p-8">
            <Paper
                elevation={0}
                className="grid w-full max-w-5xl overflow-hidden border border-divider md:grid-cols-[0.85fr_1.15fr]"
                sx={{ borderRadius: { xs: 2, sm: 3 } }}
            >
                <Box
                    className="relative isolate overflow-hidden bg-primary-light px-6 py-5 sm:px-10 md:p-10"
                    sx={{
                        '&::after': {
                            content: '""',
                            position: 'absolute',
                            zIndex: -1,
                            pointerEvents: 'none',
                            width: { xs: 164, md: '108%' },
                            aspectRatio: '1',
                            right: { xs: -12, md: '-7%' },
                            bottom: { xs: -82, md: '-18%' },
                            color: 'primary.main',
                            opacity: 0.35,
                            backgroundImage:
                                'repeating-radial-gradient(ellipse at 0% 100%, currentColor 0 1px, transparent 1.5px 7px)',
                            mask: 'url("/otus-pay-mark.svg") center / contain no-repeat',
                        },
                    }}
                >
                    <Typography
                        component="div"
                        variant="h5"
                        className="flex items-center gap-2"
                        sx={{ fontWeight: 750, letterSpacing: '-0.04em' }}
                    >
                        <img src="/otus-pay-mark.svg" width="28" height="28" alt="" />
                        Otus Pay
                    </Typography>
                </Box>
                <div className="mx-auto w-full min-w-0 max-w-lg px-5 py-8 sm:px-10 sm:py-12 md:p-14">
                    <Typography
                        component="h1"
                        variant="h4"
                        sx={{ fontSize: { xs: '1.85rem', sm: '2.25rem' }, lineHeight: 1.2 }}
                    >
                        {copy.title}
                    </Typography>
                    <Box
                        component="form"
                        onSubmit={handleSubmit}
                        className="mt-7 grid gap-5"
                        sx={{
                            '& .MuiFormHelperText-root': { mx: 0, mt: 0.75, typography: 'body2' },
                        }}
                    >
                        <div className="grid gap-1.5">
                            <Typography
                                component="label"
                                htmlFor="auth-email"
                                sx={{ fontWeight: 600 }}
                            >
                                Email
                            </Typography>
                            <TextField
                                id="auth-email"
                                name="email"
                                type="email"
                                placeholder="name@example.com"
                                value={email}
                                onChange={(event) => setEmail(event.target.value)}
                                error={!!errors.email}
                                helperText={errors.email}
                                disabled={loading}
                                autoComplete="email"
                                fullWidth
                            />
                        </div>
                        <div className="grid gap-1.5">
                            <Typography
                                component="label"
                                htmlFor="auth-password"
                                sx={{ fontWeight: 600 }}
                            >
                                Пароль
                            </Typography>
                            <TextField
                                id="auth-password"
                                name="password"
                                type="password"
                                value={password}
                                onChange={(event) => setPassword(event.target.value)}
                                error={!!errors.password}
                                helperText={errors.password || passwordHint}
                                disabled={loading}
                                autoComplete={
                                    mode === 'login' ? 'current-password' : 'new-password'
                                }
                                fullWidth
                            />
                        </div>
                        {generalError && <Alert severity="error">{generalError}</Alert>}
                        <Button
                            type="submit"
                            variant="contained"
                            size="large"
                            disabled={loading}
                            disableElevation
                            className="mt-1 min-h-12"
                        >
                            {loading ? copy.submitting : copy.submit}
                        </Button>
                    </Box>
                    <Typography
                        component="p"
                        color="text.secondary"
                        className="mt-6 flex flex-wrap items-baseline justify-center gap-x-1.5 border-t border-divider pt-5"
                    >
                        {copy.prompt}
                        <Link
                            component={RouterLink}
                            to={copy.to}
                            underline="always"
                            sx={{ fontWeight: 600, textUnderlineOffset: '0.16em' }}
                        >
                            {copy.link}
                        </Link>
                    </Typography>
                </div>
            </Paper>
        </main>
    );
}
