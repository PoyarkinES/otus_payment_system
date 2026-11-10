import { setupServer } from 'msw/node';

/** MSW-сервер для тестов: каждый тест подставляет свои ответы через `server.use(...)`. */
export const server = setupServer();
