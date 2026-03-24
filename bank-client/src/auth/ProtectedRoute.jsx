import { Navigate, useLocation } from 'react-router-dom';
import { Alert, Box } from '@mui/material';
import { useAuth } from './AuthContext';

export default function ProtectedRoute({ roles, children }) {
  const { user, hasRole } = useAuth();
  const location = useLocation();

  if (!user) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (roles && !hasRole(roles)) {
    return (
      <Box sx={{ p: 4 }}>
        <Alert severity="warning">You do not have permission to view this page.</Alert>
      </Box>
    );
  }

  return children;
}
