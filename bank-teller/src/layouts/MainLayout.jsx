import { AppBar, Toolbar, Typography, Box, Chip, Button, Container } from '@mui/material';
import { Outlet, useNavigate, Link as RouterLink } from 'react-router-dom';
import LogoutIcon from '@mui/icons-material/Logout';
import { useTellerSession } from '../auth/TellerSessionContext';

export default function MainLayout() {
  const { user, drawer, logout } = useTellerSession();
  const navigate = useNavigate();

  // Parse running balances from drawer.openingFloatJson + (later) running adjustments
  let balances = {};
  try {
    const opening = drawer?.openingFloatJson ? JSON.parse(drawer.openingFloatJson) : {};
    Object.entries(opening).forEach(([cur, info]) => {
      balances[cur] = (info && info.total) ?? 0;
    });
  } catch { /* ignore */ }

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography
            variant="h6"
            sx={{ flexGrow: 0, mr: 4, cursor: 'pointer' }}
            onClick={() => navigate('/')}
          >
            GoldBank Teller
          </Typography>

          <Box sx={{ flexGrow: 1, display: 'flex', gap: 1, alignItems: 'center' }}>
            <Button color="inherit" component={RouterLink} to="/customers">Customers</Button>
            <Button color="inherit" component={RouterLink} to="/drawer">Drawer</Button>
            {(user?.role === 'BranchManager' || user?.role === 'VaultManager' || user?.role === 'Admin') && (
              <Button color="inherit" component={RouterLink} to="/vault">Vault</Button>
            )}

            {drawer ? (
              <Chip
                size="small"
                color="success"
                label="Drawer Open"
                sx={{ ml: 2 }}
              />
            ) : (
              <Chip
                size="small"
                color="warning"
                label="No drawer"
                onClick={() => navigate('/drawer')}
                sx={{ ml: 2, cursor: 'pointer' }}
              />
            )}
          </Box>

          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Box sx={{ textAlign: 'right', mr: 1 }}>
              <Typography variant="body2">{user?.fullName ?? user?.username}</Typography>
              <Typography variant="caption" sx={{ opacity: 0.7 }}>
                {user?.branchName ?? (user?.branchId ? 'Unknown branch' : 'No branch assigned')}
              </Typography>
            </Box>
            <Button color="inherit" startIcon={<LogoutIcon />} onClick={logout}>Logout</Button>
          </Box>
        </Toolbar>
      </AppBar>

      <Container maxWidth="lg" sx={{ py: 3, flexGrow: 1 }}>
        <Outlet />
      </Container>
    </Box>
  );
}
