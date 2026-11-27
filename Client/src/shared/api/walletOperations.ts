import { useInfiniteQuery, type InfiniteData } from '@tanstack/react-query';

import type { BffWalletOperationResult } from '@/shared/api/generated/model';
import { apiFetch } from '@/shared/api/http';

/** Страница WalletOperationsPageResult с фильтрами, которую Gateway проксирует из Wallets. */
interface WalletOperationsPage {
    items: BffWalletOperationResult[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

const pageSize = 20;

export type WalletOperationFilters = {
    type?: string;
    from?: string;
    to?: string;
};

function collectOperations({ pages }: InfiniteData<WalletOperationsPage>) {
    // Новые проводки могут сдвинуть границу между уже прочитанной и следующей страницей.
    const seen = new Set<string>();
    const items = pages.flatMap((page) =>
        page.items.filter(({ id }) => {
            if (id === undefined) return true;
            if (seen.has(id)) return false;
            seen.add(id);
            return true;
        }),
    );
    return { items, totalCount: pages[pages.length - 1].totalCount };
}

export function useWalletOperations(
    walletId: string,
    filters: WalletOperationFilters = {},
    enabled = true,
) {
    const path = `/api/wallets/${encodeURIComponent(walletId)}/operations`;
    const filterKey = {
        type: filters.type || undefined,
        from: filters.from || undefined,
        to: filters.to || undefined,
    };
    return useInfiniteQuery({
        queryKey: [path, 'history', pageSize, filterKey],
        enabled,
        initialPageParam: 1,
        queryFn: async ({ pageParam, signal }) => {
            const params = new URLSearchParams({
                page: String(pageParam),
                pageSize: String(pageSize),
            });
            if (filterKey.type) params.set('type', filterKey.type);
            if (filterKey.from) params.set('from', filterKey.from);
            if (filterKey.to) params.set('to', filterKey.to);
            const { data } = await apiFetch<{ data: WalletOperationsPage }>(`${path}?${params}`, {
                signal,
            });
            return data;
        },
        getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
        select: collectOperations,
    });
}
