import type { ReactNode } from 'react';

import { useBffMe } from '@/shared/api/generated/endpoints/bff/bff';
import { useGetMyProfile } from '@/shared/api/generated/identity/endpoints/users/users';
import type { BffMeResult } from '@/shared/api/generated/model';
import type { GetProfileResultStatus } from '@/shared/api/generated/identity/model';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

type CurrentAccountData = {
    claims: BffMeResult;
    status: GetProfileResultStatus;
    canLookUpUsers: boolean;
};

export function CurrentAccount({
    children,
}: {
    children: (account: CurrentAccountData) => ReactNode;
}) {
    const me = useBffMe();
    const profile = useGetMyProfile();

    function retry() {
        void me.refetch();
        void profile.refetch();
    }

    if (me.error || profile.error) {
        return <ErrorState error={me.error ?? profile.error} onRetry={retry} />;
    }
    // При переходе в поиск дождёмся актуальных прав, даже если в кеше остался профиль.
    if (me.isPending || profile.isPending || me.isFetching || profile.isFetching) {
        return <LoadingState label="Загружаем профиль…" />;
    }
    if (
        me.data?.status !== 200 ||
        !me.data.data ||
        profile.data?.status !== 200 ||
        !profile.data.data
    ) {
        return <ErrorState error={undefined} onRetry={retry} />;
    }

    const claims = me.data.data;
    const status = profile.data.data.status;
    return children({
        claims,
        status,
        canLookUpUsers: claims.role === 'Admin' && status === 'Active',
    });
}
