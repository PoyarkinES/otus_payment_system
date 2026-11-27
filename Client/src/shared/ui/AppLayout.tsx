import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import { Link, Outlet } from 'react-router';

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
                    <Box className="ml-auto flex items-center gap-4">
                        <Button component={Link} to="/profile" color="inherit">
                            Профиль
                        </Button>
                    </Box>
                </Toolbar>
            </AppBar>
            <Container component="main" maxWidth="md" className="flex-1 py-8">
                <Outlet />
            </Container>
        </div>
    );
}
