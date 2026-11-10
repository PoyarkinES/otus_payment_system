import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import { Link } from 'react-router';

export function NotFoundPage() {
    return (
        <section className="flex flex-col items-start gap-4">
            <Typography variant="h4" component="h1">
                Страница не найдена
            </Typography>
            <Typography color="text.secondary">
                Такого адреса нет. Возможно, ссылка устарела.
            </Typography>
            <Button component={Link} to="/" variant="contained">
                На главную
            </Button>
        </section>
    );
}
