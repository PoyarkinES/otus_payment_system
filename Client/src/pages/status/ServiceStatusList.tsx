import Chip from '@mui/material/Chip';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Paper from '@mui/material/Paper';

import { serviceTitle, statusLabel } from '@/pages/status/statusLabels';
import type { BffServiceStatus } from '@/shared/api/generated/model';

type ServiceStatusListProps = {
    services: BffServiceStatus | undefined;
};

/** Сервисы берутся из ответа как есть: новый сервис в спеке появится здесь без правок кода. */
export function ServiceStatusList({ services }: ServiceStatusListProps) {
    const entries: [string, unknown][] = Object.entries(services ?? {});

    return (
        <Paper variant="outlined">
            <List disablePadding>
                {entries.map(([key, value], index) => {
                    const title = serviceTitle(key);
                    const label = statusLabel(value);
                    return (
                        <ListItem
                            key={key}
                            aria-label={title}
                            divider={index < entries.length - 1}
                            className="gap-4"
                        >
                            <ListItemText primary={title} />
                            <Chip label={label.text} color={label.color} size="small" />
                        </ListItem>
                    );
                })}
            </List>
        </Paper>
    );
}
