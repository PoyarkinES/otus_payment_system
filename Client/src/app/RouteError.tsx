import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';

/** Запасной экран, если страница упала при рендере: вместо белого экрана — понятное сообщение. */
export function RouteError() {
    return (
        <main className="mx-auto flex max-w-xl flex-col items-start gap-4 p-8">
            <Typography variant="h4" component="h1">
                Что-то пошло не так
            </Typography>
            <Typography color="text.secondary">
                Страница не смогла отобразиться. Попробуйте перезагрузить её — если ошибка
                повторится, сообщите команде.
            </Typography>
            <Button variant="contained" onClick={() => window.location.reload()}>
                Перезагрузить страницу
            </Button>
        </main>
    );
}
