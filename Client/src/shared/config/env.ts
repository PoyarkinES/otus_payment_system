export type Env = {
    /** Адрес Gateway без слэша на конце. Единственный адрес бэка, который знает фронт. */
    apiBaseUrl: string;
};

type RawEnv = {
    VITE_API_BASE_URL?: string;
};

const DEFAULT_API_BASE_URL = 'http://localhost:8080';

export function readEnv(raw: RawEnv): Env {
    const baseUrl = raw.VITE_API_BASE_URL?.trim() || DEFAULT_API_BASE_URL;

    return {
        apiBaseUrl: baseUrl.replace(/\/+$/, ''),
    };
}

export const env: Env = readEnv(import.meta.env);
