export type Env = {
    /** Адрес Gateway без слэша на конце. Единственный адрес бэка, который знает фронт. */
    apiBaseUrl: string;
    /** Отвечать на запросы моками MSW вместо настоящего бэка. */
    apiMocks: boolean;
};

type RawEnv = {
    VITE_API_BASE_URL?: string;
    VITE_API_MOCKS?: string;
};

const DEFAULT_API_BASE_URL = 'http://localhost:8080';

export function readEnv(raw: RawEnv): Env {
    const baseUrl = raw.VITE_API_BASE_URL?.trim() || DEFAULT_API_BASE_URL;

    return {
        apiBaseUrl: baseUrl.replace(/\/+$/, ''),
        apiMocks: raw.VITE_API_MOCKS === 'true',
    };
}

export const env: Env = readEnv(import.meta.env);
