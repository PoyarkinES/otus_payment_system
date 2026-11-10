import { setupWorker } from 'msw/browser';

import { handlers } from '@/shared/testing/handlers';

export const worker = setupWorker(...handlers);
