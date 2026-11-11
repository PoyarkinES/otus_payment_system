/**
 * Ошибка API в формате ProblemDetails (RFC 9457), как её отдают Gateway и сервисы —
 * контракт в docs/api-errors.ru.md. Разбирать по `status`, а не по тексту `title`/`detail`.
 */
export class ApiProblem extends Error {
    readonly status: number;
    readonly title: string;
    readonly detail?: string;
    readonly type?: string;
    /** Correlation ID запроса — по нему ищут запрос в логах всех сервисов. */
    readonly traceId?: string;
    /** Ошибки по полям (только у 400): ключ — имя поля в camelCase. */
    readonly errors: Record<string, string[]>;

    constructor(init: {
        status: number;
        title: string;
        detail?: string;
        type?: string;
        traceId?: string;
        errors?: Record<string, string[]>;
    }) {
        super(init.detail ?? init.title);
        this.name = 'ApiProblem';
        this.status = init.status;
        this.title = init.title;
        this.detail = init.detail;
        this.type = init.type;
        this.traceId = init.traceId;
        this.errors = init.errors ?? {};
    }
}

/** Запрос не дошёл до сервера: Gateway не запущен, нет сети, CORS. */
export class ApiNetworkError extends Error {
    readonly correlationId: string;

    constructor(correlationId: string, options?: { cause?: unknown }) {
        super('Сервер недоступен', options);
        this.name = 'ApiNetworkError';
        this.correlationId = correlationId;
    }
}

export function isApiProblem(error: unknown): error is ApiProblem {
    return error instanceof ApiProblem;
}
