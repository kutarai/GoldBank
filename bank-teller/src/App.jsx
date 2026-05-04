import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, CssBaseline } from '@mui/material';
import { theme } from './theme';
import { TellerSessionProvider } from './auth/TellerSessionContext';
import ProtectedRoute from './auth/ProtectedRoute';
import ErrorBoundary from './components/ErrorBoundary';
import SecurityShell from './components/SecurityShell';
import MainLayout from './layouts/MainLayout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import SupervisorDashboard from './pages/SupervisorDashboard';
import VaultDashboard from './pages/VaultDashboard';
import CustomerSearch from './pages/CustomerSearch';
import CustomerCard from './pages/CustomerCard';
import Deposit from './pages/Deposit';
import Withdrawal from './pages/Withdrawal';
import Drawer from './pages/Drawer';
import { getUser } from './services/api';

// Branch managers see the supervisor dashboard at /; tellers see their own dashboard.
function HomeRouter() {
  const u = getUser();
  if (u?.role === 'BranchManager' || u?.role === 'Admin') {
    return <SupervisorDashboard />;
  }
  return <Dashboard />;
}

export default function App() {
  return (
    <ErrorBoundary>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <TellerSessionProvider>
          <BrowserRouter>
            <Routes>
              <Route path="/login" element={<Login />} />
              <Route element={<ProtectedRoute />}>
                <Route element={<SecurityShell />}>
                  <Route element={<MainLayout />}>
                    <Route path="/" element={<HomeRouter />} />
                    <Route path="/customers" element={<CustomerSearch />} />
                    <Route path="/customers/:accountId" element={<CustomerCard />} />
                    <Route path="/deposit" element={<Deposit />} />
                    <Route path="/withdrawal" element={<Withdrawal />} />
                    <Route path="/drawer" element={<Drawer />} />
                    <Route path="/vault" element={<VaultDashboard />} />
                    <Route path="*" element={<Navigate to="/" replace />} />
                  </Route>
                </Route>
              </Route>
            </Routes>
          </BrowserRouter>
        </TellerSessionProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}
