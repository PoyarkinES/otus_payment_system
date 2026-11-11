import CircularProgress from '@mui/material/CircularProgress';
import Typography from '@mui/material/Typography';

type LoadingStateProps = {
    label?: string;
};

export function LoadingState({ label = 'Загрузка…' }: LoadingStateProps) {
    return (
        <div role="status" className="flex items-center gap-3 py-6">
            <CircularProgress size={24} />
            <Typography color="text.secondary">{label}</Typography>
        </div>
    );
}
