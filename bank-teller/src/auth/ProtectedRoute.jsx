import { Navigate, Outlet } from 'react-router-dom';
import { getToken, getUser } from '../services/api';

export default function ProtectedRoute({ requiredRoles }) {
  const token = getToken();
  const user = getUser();

  if (!token || !user) return <Navigate to="/login" replace />;

  if (requiredRoles && requiredRoles.length > 0) {
    if (!requiredRoles.includes(user.role)) {
      return <Navigate to="/" replace />;
    }
  }

  return <Outlet />;
}
