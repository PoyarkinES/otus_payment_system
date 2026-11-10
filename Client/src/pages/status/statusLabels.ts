export type StatusLabel = {
    text: string;
    color: 'success' | 'error' | 'default';
};

/** Статус сервиса из /bff/health → подпись и цвет. Незнакомое значение показываем как есть. */
export function statusLabel(value: string | null | undefined): StatusLabel {
    switch (value) {
        case 'ok':
            return { text: 'Работает', color: 'success' };
        case 'unavailable':
            return { text: 'Недоступен', color: 'error' };
        case null:
        case undefined:
        case '':
            return { text: 'Нет данных', color: 'default' };
        default:
            return { text: value, color: 'default' };
    }
}

const SERVICE_TITLES: Record<string, string> = {
    gateway: 'Gateway',
    identity: 'Identity',
};

export function serviceTitle(key: string): string {
    return SERVICE_TITLES[key] ?? key;
}
