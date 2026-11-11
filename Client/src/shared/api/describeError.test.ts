import { describe, expect, it } from 'vitest';

import { describeError } from '@/shared/api/describeError';
import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';

function problem(status: number, detail?: string): ApiProblem {
    return new ApiProblem({ status, title: 'Title', detail, traceId: 'trace-1' });
}

describe('describeError', () => {
    it('describes network failure', () => {
        expect(describeError(new ApiNetworkError('net-1'))).toEqual({
            title: 'Сервер недоступен',
            detail: 'Проверьте, что Gateway запущен, и повторите попытку.',
            correlationId: 'net-1',
        });
    });

    it.each([
        [400, 'Проверьте введённые данные'],
        [401, 'Нужно войти заново'],
        [403, 'Недостаточно прав'],
        [404, 'Не найдено'],
        [409, 'Конфликт данных'],
        [502, 'Сервис временно недоступен'],
        [503, 'Сервис временно недоступен'],
        [504, 'Сервис временно недоступен'],
        [500, 'Ошибка на сервере'],
        [418, 'Не удалось выполнить запрос'],
    ])('describes %i', (status, title) => {
        const described = describeError(problem(status, 'Пояснение сервера'));

        expect(described.title).toBe(title);
        expect(described.detail).toBe('Пояснение сервера');
        expect(described.correlationId).toBe('trace-1');
    });

    it('describes 429 with fixed advice', () => {
        expect(describeError(problem(429, 'Слишком много попыток.'))).toEqual({
            title: 'Слишком много запросов',
            detail: 'Подождите немного и повторите.',
            correlationId: 'trace-1',
        });
    });

    it('omits detail when server sent none', () => {
        expect(describeError(problem(500)).detail).toBeUndefined();
    });

    it('describes unknown error without correlation id', () => {
        expect(describeError(new Error('boom'))).toEqual({ title: 'Что-то пошло не так' });
    });
});
