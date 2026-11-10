import { render, type RenderResult } from '@testing-library/react';
import type { ReactElement } from 'react';
import { createMemoryRouter, RouterProvider } from 'react-router';

import { AppProviders } from '@/app/providers';
import { createQueryClient } from '@/app/queryClient';
import { routes } from '@/app/router';

/** Рендерит элемент внутри всех провайдеров приложения; повторы запросов в тестах выключены. */
export function renderWithProviders(ui: ReactElement): RenderResult {
    const queryClient = createQueryClient();
    queryClient.setDefaultOptions({ queries: { retry: false } });

    return render(<AppProviders queryClient={queryClient}>{ui}</AppProviders>);
}

/** Рендерит приложение целиком, открытое на `path`, — как если бы пользователь зашёл по ссылке. */
export function renderRoute(path = '/'): RenderResult {
    const router = createMemoryRouter(routes, { initialEntries: [path] });

    return renderWithProviders(<RouterProvider router={router} />);
}
