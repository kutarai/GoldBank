import { useState, useMemo } from 'react';
import { Routes, Route } from 'react-router-dom';
import { ThemeProvider } from '@mui/material/styles';
import { SnackbarProvider } from './services/snackbar';
import { AuthProvider } from './auth/AuthContext';
import { buildTheme } from './theme';
import ProtectedRoute from './auth/ProtectedRoute';
import MainLayout from './layouts/MainLayout';
import Login from './pages/Login';
import Dashboard from './pages/Dashboard';
import Customers from './pages/Customers';
import KycReview from './pages/KycReview';
import Disputes from './pages/Disputes';
import FraudAlerts from './pages/FraudAlerts';
import Transactions from './pages/Transactions';
import UserManagement from './pages/UserManagement';
import BranchManagement from './pages/BranchManagement';
import DepositHouses from './pages/DepositHouses';
import LoanReview from './pages/LoanReview';
import AssetValuation from './pages/AssetValuation';
import SystemConfig from './pages/SystemConfig';
import Merchants from './pages/Merchants';
import Tariffs from './pages/Tariffs';
import AuditTrail from './pages/AuditTrail';
import UserGrowthReport from './pages/reports/UserGrowthReport';
import MerchantReport from './pages/reports/MerchantReport';
import RevenueReport from './pages/reports/RevenueReport';
import ReconReport from './pages/reports/ReconReport';
import NotFound from './pages/NotFound';
import { Access } from './auth/roles';

export default function App() {
  const [darkMode, setDarkMode] = useState(false);
  const theme = useMemo(() => buildTheme(darkMode ? 'dark' : 'light'), [darkMode]);

  return (
    <ThemeProvider theme={theme}>
      <AuthProvider>
        <SnackbarProvider>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route element={<ProtectedRoute><MainLayout darkMode={darkMode} onToggleDark={() => setDarkMode(!darkMode)} /></ProtectedRoute>}>
              <Route index element={<Dashboard />} />
              <Route path="customers" element={<ProtectedRoute roles={Access.CustomerAccess}><Customers /></ProtectedRoute>} />
              <Route path="kyc" element={<ProtectedRoute roles={Access.KycAccess}><KycReview /></ProtectedRoute>} />
              <Route path="disputes" element={<ProtectedRoute roles={Access.DisputeAccess}><Disputes /></ProtectedRoute>} />
              <Route path="fraud" element={<ProtectedRoute roles={Access.FraudAccess}><FraudAlerts /></ProtectedRoute>} />
              <Route path="transactions" element={<Transactions />} />
              <Route path="users" element={<ProtectedRoute roles={Access.UserManagement}><UserManagement /></ProtectedRoute>} />
              <Route path="branches" element={<ProtectedRoute roles={Access.UserManagement}><BranchManagement /></ProtectedRoute>} />
              <Route path="deposit-houses" element={<ProtectedRoute roles={Access.ConfigAccess}><DepositHouses /></ProtectedRoute>} />
              <Route path="loans" element={<ProtectedRoute roles={Access.LoanAccess}><LoanReview /></ProtectedRoute>} />
              <Route path="assets" element={<ProtectedRoute roles={Access.LoanAccess}><AssetValuation /></ProtectedRoute>} />
              <Route path="config" element={<ProtectedRoute roles={Access.ConfigAccess}><SystemConfig /></ProtectedRoute>} />
              <Route path="merchants" element={<ProtectedRoute roles={Access.CustomerAccess}><Merchants /></ProtectedRoute>} />
              <Route path="tariffs" element={<ProtectedRoute roles={Access.ConfigAccess}><Tariffs /></ProtectedRoute>} />
              <Route path="audit" element={<ProtectedRoute roles={Access.ReportAccess}><AuditTrail /></ProtectedRoute>} />
              <Route path="reports/users" element={<ProtectedRoute roles={Access.ReportAccess}><UserGrowthReport /></ProtectedRoute>} />
              <Route path="reports/merchants" element={<ProtectedRoute roles={Access.ReportAccess}><MerchantReport /></ProtectedRoute>} />
              <Route path="reports/revenue" element={<ProtectedRoute roles={Access.ReportAccess}><RevenueReport /></ProtectedRoute>} />
              <Route path="reports/recon" element={<ProtectedRoute roles={Access.ReportAccess}><ReconReport /></ProtectedRoute>} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </SnackbarProvider>
      </AuthProvider>
    </ThemeProvider>
  );
}
