import './app/styles.css';

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from '@/app/App';
import { env } from '@/shared/config/env';

async function enableMocks(): Promise<void> {
    if (!env.apiMocks) return;
    // Динамический импорт: без флага MSW не попадает в бандл.
    const { worker } = await import('@/shared/testing/browser');
    await worker.start({ onUnhandledFrame: 'bypass' });
}

await enableMocks();

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <App />
    </StrictMode>,
);
