import { useQuery } from '@tanstack/react-query';

import type { BffWalletOperationResult } from '@/shared/api/generated/model';
import { apiFetch } from '@/shared/api/http';

/** Страница WalletOperationsPageResult, которую Gateway проксирует из Wallets. */
interface WalletOperationsPage {
    items: BffWalletOperationResult[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export function useWalletOperations(walletId: string, page: number, pageSize: number) {
    const path = `/api/wallets/${encodeURIComponent(walletId)}/operations`;
    return useQuery({
        queryKey: [path, { page, pageSize }],
        queryFn: async ({ signal }) => {
            const { data } = await apiFetch<{ data: WalletOperationsPage }>(
                `${path}?page=${page}&pageSize=${pageSize}`,
                { signal },
            );
            return data;
        },
    });
}
