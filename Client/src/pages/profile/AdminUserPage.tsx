import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { useState, type FormEvent } from 'react';
import { z } from 'zod';

import { CurrentAccount } from '@/pages/profile/CurrentAccount';
import { ProfileDetails } from '@/pages/profile/ProfileDetails';
import { useGetUserById } from '@/shared/api/generated/identity/endpoints/users/users';
import { ErrorState } from '@/shared/ui/ErrorState';
import { LoadingState } from '@/shared/ui/LoadingState';

const userIdSchema = z.guid();

function UserLookup() {
    const [input, setInput] = useState('');
    const [submittedId, setSubmittedId] = useState('');
    const [invalid, setInvalid] = useState(false);
    const result = useGetUserById(submittedId, { query: { enabled: Boolean(submittedId) } });

    function search(event: FormEvent) {
        event.preventDefault();
        const id = input.trim();
        if (!userIdSchema.safeParse(id).success) {
            setInvalid(true);
            return;
        }
        if (submittedId === id) {
            void result.refetch();
        } else {
            setSubmittedId(id);
        }
    }

    return (
        <>
            <Paper
                component="form"
                variant="outlined"
                className="flex flex-col items-start gap-4 p-5"
                onSubmit={search}
            >
                <TextField
                    label="UUID пользователя"
                    value={input}
                    onChange={(event) => {
                        setInput(event.target.value);
                        setInvalid(false);
                        setSubmittedId('');
                    }}
                    error={invalid}
                    helperText={
                        invalid && 'Введите UUID в формате xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.'
                    }
                    fullWidth
                />
                <Button type="submit" variant="contained" disabled={result.isFetching}>
                    Найти
                </Button>
            </Paper>

            {result.isFetching && <LoadingState label="Ищем пользователя…" />}
            {result.error && <ErrorState error={result.error} onRetry={() => result.refetch()} />}
            {submittedId && !result.isFetching && !result.error && result.data?.status === 200 && (
                <ProfileDetails
                    email={result.data.data.email}
                    status={result.data.data.status}
                    userId={result.data.data.userId}
                />
            )}
        </>
    );
}

export function AdminUserPage() {
    return (
        <section className="flex flex-col gap-4">
            <Typography variant="h4" component="h1">
                Поиск пользователя
            </Typography>
            <CurrentAccount>
                {({ canLookUpUsers }) =>
                    canLookUpUsers ? (
                        <UserLookup />
                    ) : (
                        <Alert severity="warning">Недостаточно прав</Alert>
                    )
                }
            </CurrentAccount>
        </section>
    );
}
