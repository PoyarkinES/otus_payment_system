import { getBffHealthMockHandler } from '@/shared/api/generated/endpoints/bff/bff.msw';

/**
 * Ответы для режима без бэка (VITE_API_MOCKS=true). Хендлеры сгенерированы orval из спеки;
 * здесь только задаём правдоподобные данные вместо случайных.
 */
export const handlers = [
    getBffHealthMockHandler({ status: 'ok', services: { gateway: 'ok', identity: 'ok' } }),
];
