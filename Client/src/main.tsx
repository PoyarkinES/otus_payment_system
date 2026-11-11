import './app/styles.css';

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

import { App } from '@/app/App';

async function enableMocks(): Promise<void> {
    // Именно import.meta.env, а не shared/config: Vite подставляет значение при сборке и
    // выкидывает эту ветку — без флага MSW и faker не попадают в сборку вовсе.
    if (import.meta.env.VITE_API_MOCKS !== 'true') return;
    const { worker } = await import('@/shared/testing/browser');
    await worker.start({ onUnhandledFrame: 'bypass' });
}

await enableMocks();

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <App />
    </StrictMode>,
);
