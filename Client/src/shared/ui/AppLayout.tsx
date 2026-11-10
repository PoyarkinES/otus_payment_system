import AppBar from '@mui/material/AppBar';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { Link, Outlet } from 'react-router';

/** Общий каркас всех страниц: шапка и область контента. */
export function AppLayout() {
    return (
        <div className="flex min-h-screen flex-col">
            <AppBar position="static" elevation={0}>
                <Toolbar>
                    <Typography
                        variant="h6"
                        component={Link}
                        to="/"
                        className="text-inherit no-underline"
                    >
                        Otus Payment System
                    </Typography>
                </Toolbar>
            </AppBar>
            <Container component="main" maxWidth="md" className="flex-1 py-8">
                <Outlet />
            </Container>
        </div>
    );
}
