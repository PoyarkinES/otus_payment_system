import { createTheme } from '@mui/material/styles';

/**
 * Тема MUI — единственный источник цветов, шрифтов и брейкпоинтов.
 * `cssVariables: true` публикует их как `--mui-*`, и Tailwind берёт значения оттуда (см. app/styles.css).
 */
export const theme = createTheme({
    cssVariables: true,
    palette: {
        mode: 'light',
        primary: { main: '#1e5bd8' },
        background: { default: '#f5f7fb' },
    },
    shape: { borderRadius: 10 },
});
