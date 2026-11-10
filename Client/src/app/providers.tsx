import CssBaseline from '@mui/material/CssBaseline';
import GlobalStyles from '@mui/material/GlobalStyles';
import { StyledEngineProvider, ThemeProvider } from '@mui/material/styles';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';

import { createQueryClient } from '@/app/queryClient';
import { theme } from '@/shared/theme/theme';

type AppProvidersProps = {
    children: ReactNode;
    /** Свой клиент нужен тестам; приложение создаёт его само. */
    queryClient?: QueryClient;
};

export function AppProviders({ children, queryClient }: AppProvidersProps) {
    const [client] = useState(() => queryClient ?? createQueryClient());

    return (
        // enableCssLayer кладёт стили MUI в слой `mui`, чтобы утилиты Tailwind перебивали их без !important.
        <StyledEngineProvider enableCssLayer>
            <GlobalStyles styles="@layer theme, base, mui, components, utilities;" />
            <ThemeProvider theme={theme}>
                <CssBaseline />
                <QueryClientProvider client={client}>{children}</QueryClientProvider>
            </ThemeProvider>
        </StyledEngineProvider>
    );
}
