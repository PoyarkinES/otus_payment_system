import Button from '@mui/material/Button';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getBffWalletsQueryKey } from '@/shared/api/generated/endpoints/bff/bff';
import { apiFetch } from '@/shared/api/http';
import { isApiProblem } from '@/shared/api/problem';
import { ErrorState } from '@/shared/ui/ErrorState';

async function createRubWallet() {
    try {
        await apiFetch('/api/wallets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ currency: 'RUB' }),
        });
    } catch (error) {
        // Другой запрос мог создать единственный рублёвый кошелёк раньше нас.
        if (!isApiProblem(error) || error.status !== 409) throw error;
    }
}

export function CreateWalletButton() {
    const client = useQueryClient();
    const create = useMutation({
        mutationFn: createRubWallet,
        onSuccess: () => client.invalidateQueries({ queryKey: getBffWalletsQueryKey() }),
    });

    return (
        <div className="flex flex-col items-start gap-3">
            <Button
                type="button"
                variant="contained"
                disableElevation
                disabled={create.isPending}
                onClick={() => create.mutate()}
                sx={{ minHeight: 44 }}
            >
                {create.isPending ? 'Создаём кошелёк…' : 'Создать кошелёк'}
            </Button>
            {create.error && <ErrorState error={create.error} />}
        </div>
    );
}
