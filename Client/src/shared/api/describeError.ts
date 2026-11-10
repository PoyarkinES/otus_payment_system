import { ApiNetworkError, ApiProblem } from '@/shared/api/problem';

export type ErrorDescription = {
    title: string;
    detail?: string;
    /** Показывается пользователю как «Код для поддержки». */
    correlationId?: string;
};

/** Превращает любую ошибку запроса в текст для пользователя. */
export function describeError(error: unknown): ErrorDescription {
    if (error instanceof ApiNetworkError) {
        return {
            title: 'Сервер недоступен',
            detail: 'Проверьте, что Gateway запущен, и повторите попытку.',
            correlationId: error.correlationId,
        };
    }

    if (error instanceof ApiProblem) {
        if (error.status === 429) {
            return {
                title: 'Слишком много запросов',
                detail: 'Подождите немного и повторите.',
                correlationId: error.traceId,
            };
        }
        return {
            title: titleFor(error.status),
            detail: error.detail,
            correlationId: error.traceId,
        };
    }

    return { title: 'Что-то пошло не так' };
}

function titleFor(status: number): string {
    switch (status) {
        case 400:
            return 'Проверьте введённые данные';
        case 401:
            return 'Нужно войти заново';
        case 403:
            return 'Недостаточно прав';
        case 404:
            return 'Не найдено';
        case 409:
            return 'Конфликт данных';
        case 502:
        case 503:
        case 504:
            return 'Сервис временно недоступен';
        default:
            return status >= 500 ? 'Ошибка на сервере' : 'Не удалось выполнить запрос';
    }
}
