/// <reference types="vite/client" />

interface ImportMetaEnv {
    /** Адрес Gateway. По умолчанию http://localhost:8080. */
    readonly VITE_API_BASE_URL?: string;
    /** `true` — отвечать на запросы моками MSW, без бэка. */
    readonly VITE_API_MOCKS?: string;
}

interface ImportMeta {
    readonly env: ImportMetaEnv;
}
