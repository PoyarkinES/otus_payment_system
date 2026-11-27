const decimal = new Intl.NumberFormat('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 4,
});

/** В ответе BFF валюта и сумма могут отсутствовать независимо друг от друга. */
export function formatAmount(value?: number, currency?: string | null, sign = '') {
    if (typeof value !== 'number' || !Number.isFinite(value)) return '—';
    const unit = currency === 'RUB' ? '₽' : currency;
    const number = sign + decimal.format(value);
    return unit ? `${number}\u00a0${unit}` : number;
}
