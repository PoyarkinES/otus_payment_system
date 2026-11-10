import Alert from '@mui/material/Alert';
import AlertTitle from '@mui/material/AlertTitle';
import Button from '@mui/material/Button';

import { describeError } from '@/shared/api/describeError';

type ErrorStateProps = {
    error: unknown;
    onRetry?: () => void;
};

/** Ошибка запроса глазами пользователя: что случилось, что делать и код для поддержки. */
export function ErrorState({ error, onRetry }: ErrorStateProps) {
    const { title, detail, correlationId } = describeError(error);

    return (
        <Alert
            severity="error"
            action={
                onRetry && (
                    <Button color="inherit" size="small" onClick={onRetry}>
                        Повторить
                    </Button>
                )
            }
        >
            <AlertTitle>{title}</AlertTitle>
            {detail && <div>{detail}</div>}
            {correlationId && (
                <div className="mt-2 text-sm opacity-80">Код для поддержки: {correlationId}</div>
            )}
        </Alert>
    );
}
