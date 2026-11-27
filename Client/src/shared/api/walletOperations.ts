import { useInfiniteQuery, type InfiniteData } from '@tanstack/react-query';

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

const pageSize = 20;

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

export function useWalletOperations(walletId: string) {
    const path = `/api/wallets/${encodeURIComponent(walletId)}/operations`;
    return useInfiniteQuery({
        queryKey: [path, 'history', pageSize],
        initialPageParam: 1,
        queryFn: async ({ pageParam, signal }) => {
            const { data } = await apiFetch<{ data: WalletOperationsPage }>(
                `${path}?page=${pageParam}&pageSize=${pageSize}`,
                { signal },
            );
            return data;
        },
        getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
        select: collectOperations,
    });
}
