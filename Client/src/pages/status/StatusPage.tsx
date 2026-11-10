import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';

import { ServiceStatusList } from '@/pages/status/ServiceStatusList';
import { useBffHealth } from '@/shared/api/generated/endpoints/bff/bff';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

/**
 * «Статус системы» по GET /bff/health. 503 здесь — не ошибка: Gateway отвечает тем же телом
 * и сообщает, какой сервис недоступен, поэтому это состояние «работает с ограничениями».
 */
export function StatusPage() {
    const { data, error, isPending, isFetching, refetch } = useBffHealth();
    // После неудачного обновления Query хранит прошлый ответ; показывать его рядом с ошибкой
    // нельзя — пользователь увидит «Работает» у сервиса, до которого уже не достучаться.
    const health =
        !error && data && (data.status === 200 || data.status === 503) ? data.data : undefined;

    return (
        <section className="flex flex-col gap-4">
            <div className="flex items-center justify-between gap-4">
                <Typography variant="h4" component="h1">
                    Статус системы
                </Typography>
                <Button variant="outlined" onClick={() => refetch()} disabled={isFetching}>
                    Обновить
                </Button>
            </div>

            {isPending && <LoadingState label="Проверяем сервисы…" />}

            {error && <ErrorState error={error} onRetry={() => refetch()} />}

            {health && (
                <>
                    {health.status === 'ok' ? (
                        <Alert severity="success">Все сервисы работают</Alert>
                    ) : (
                        <Alert severity="warning">Система работает с ограничениями</Alert>
                    )}
                    <ServiceStatusList services={health.services} />
                </>
            )}
        </section>
    );
}
