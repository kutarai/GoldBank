import { useLocation, useNavigate } from 'react-router-dom';
import {
  List, ListItemButton, ListItemIcon, ListItemText, Divider, Collapse,
} from '@mui/material';
import {
  Dashboard, People, VerifiedUser, Gavel, Shield, Receipt, ManageAccounts,
  AccountBalance, AccountBalanceWallet, Settings, BarChart, TrendingUp,
  Storefront, AttachMoney, CompareArrows, ManageSearch, Logout, ExpandLess, ExpandMore,
} from '@mui/icons-material';
import { useState } from 'react';
import { useAuth } from '../auth/AuthContext';
import { Access } from '../auth/roles';

const navItems = [
  { label: 'Dashboard', icon: <Dashboard />, path: '/' },
  { label: 'Customers', icon: <People />, path: '/customers', access: Access.CustomerAccess },
  { label: 'KYC Review', icon: <VerifiedUser />, path: '/kyc', access: Access.KycAccess },
  { label: 'Disputes', icon: <Gavel />, path: '/disputes', access: Access.DisputeAccess },
  { label: 'Fraud Alerts', icon: <Shield />, path: '/fraud', access: Access.FraudAccess },
  { label: 'Transactions', icon: <Receipt />, path: '/transactions' },
  { label: 'Users', icon: <ManageAccounts />, path: '/users', access: Access.UserManagement },
  { label: 'Branches', icon: <AccountBalance />, path: '/branches', access: Access.UserManagement },
  { label: 'Loan Review', icon: <AccountBalanceWallet />, path: '/loans', access: Access.LoanAccess },
  { label: 'System Config', icon: <Settings />, path: '/config', access: Access.ConfigAccess },
];

const reportItems = [
  { label: 'User Growth', icon: <TrendingUp />, path: '/reports/users' },
  { label: 'Merchant Performance', icon: <Storefront />, path: '/reports/merchants' },
  { label: 'Revenue', icon: <AttachMoney />, path: '/reports/revenue' },
  { label: 'Reconciliation', icon: <CompareArrows />, path: '/reports/recon' },
];

export default function NavMenu() {
  const { user, hasRole, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [reportsOpen, setReportsOpen] = useState(false);

  if (!user) return null;

  return (
    <List component="nav" sx={{ pt: 0 }}>
      {navItems.map((item) => {
        if (item.access && !hasRole(item.access)) return null;
        return (
          <ListItemButton
            key={item.path}
            selected={location.pathname === item.path}
            onClick={() => navigate(item.path)}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        );
      })}

      {hasRole(Access.ReportAccess) && (
        <>
          <ListItemButton onClick={() => setReportsOpen(!reportsOpen)}>
            <ListItemIcon><BarChart /></ListItemIcon>
            <ListItemText primary="Reports" />
            {reportsOpen ? <ExpandLess /> : <ExpandMore />}
          </ListItemButton>
          <Collapse in={reportsOpen} timeout="auto" unmountOnExit>
            <List component="div" disablePadding>
              {reportItems.map((item) => (
                <ListItemButton
                  key={item.path}
                  sx={{ pl: 4 }}
                  selected={location.pathname === item.path}
                  onClick={() => navigate(item.path)}
                >
                  <ListItemIcon>{item.icon}</ListItemIcon>
                  <ListItemText primary={item.label} />
                </ListItemButton>
              ))}
            </List>
          </Collapse>
          <ListItemButton
            selected={location.pathname === '/audit'}
            onClick={() => navigate('/audit')}
          >
            <ListItemIcon><ManageSearch /></ListItemIcon>
            <ListItemText primary="Audit Trail" />
          </ListItemButton>
        </>
      )}

      <Divider sx={{ my: 1 }} />
      <ListItemButton onClick={() => { logout(); navigate('/login'); }}>
        <ListItemIcon><Logout color="error" /></ListItemIcon>
        <ListItemText primary="Logout" sx={{ color: 'error.main' }} />
      </ListItemButton>
    </List>
  );
}
