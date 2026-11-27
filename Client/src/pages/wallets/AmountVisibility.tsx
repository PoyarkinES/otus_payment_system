import IconButton from '@mui/material/IconButton';
import SvgIcon from '@mui/material/SvgIcon';
import Tooltip from '@mui/material/Tooltip';
import { createContext, useContext, useState, type ReactNode } from 'react';

const AmountVisibility = createContext<{ hidden: boolean; toggle: () => void } | null>(null);

export function AmountVisibilityProvider({ children }: { children: ReactNode }) {
    const [hidden, setHidden] = useState(false);
    return (
        <AmountVisibility.Provider value={{ hidden, toggle: () => setHidden((value) => !value) }}>
            {children}
        </AmountVisibility.Provider>
    );
}

// eslint-disable-next-line react-refresh/only-export-components -- обе страницы читают одну настройку
export function useAmountVisibility() {
    const value = useContext(AmountVisibility);
    if (!value) throw new Error('AmountVisibilityProvider is required');
    return value;
}

export function AmountVisibilityButton() {
    const { hidden, toggle } = useAmountVisibility();
    const label = hidden ? 'Показать суммы' : 'Скрыть суммы';
    return (
        <Tooltip title={label}>
            <IconButton aria-label={label} onClick={toggle} sx={{ width: 44, height: 44 }}>
                <SvgIcon
                    viewBox="0 0 24 24"
                    sx={{ fill: 'none', stroke: 'currentColor', strokeWidth: 1.7 }}
                >
                    <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" />
                    <circle cx="12" cy="12" r="3" />
                    {hidden && <path d="m3 3 18 18" />}
                </SvgIcon>
            </IconButton>
        </Tooltip>
    );
}
