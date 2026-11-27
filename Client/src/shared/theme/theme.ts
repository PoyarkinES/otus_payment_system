import { createTheme } from '@mui/material/styles';

const inter = '"Inter Variable", system-ui, sans-serif';
const heading = { fontFamily: inter, fontWeight: 650, letterSpacing: '-0.025em' };
export const amountTypography = {
    fontFamily: inter,
    fontVariantNumeric: 'lining-nums tabular-nums',
};

/**
 * Тема MUI — единственный источник цветов, шрифтов и брейкпоинтов.
 * `cssVariables: true` публикует их как `--mui-*`, и Tailwind берёт значения оттуда (см. app/styles.css).
 */
export const theme = createTheme({
    cssVariables: true,
    palette: {
        mode: 'light',
        primary: { main: '#405A80', light: '#DFE7F3', contrastText: '#FFFFFF' },
        background: { default: '#F1F4F8', paper: '#FFFFFF' },
        text: { primary: '#202B3A', secondary: '#516174' },
        success: { main: '#19633F' },
    },
    typography: {
        fontFamily: '"Source Sans 3 Variable", system-ui, sans-serif',
        h1: heading,
        h2: heading,
        h3: heading,
        h4: heading,
        h5: heading,
        h6: heading,
        body1: { fontSize: '1.0625rem', lineHeight: 1.5 },
        body2: { fontSize: '0.9375rem', lineHeight: 1.45 },
        button: { textTransform: 'none', fontSize: '1rem', fontWeight: 600, letterSpacing: 0 },
    },
    shape: { borderRadius: 10 },
});
