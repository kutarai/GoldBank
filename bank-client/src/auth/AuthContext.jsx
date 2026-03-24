import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { SEED_ACCOUNTS } from './roles';

const AuthContext = createContext(null);

const STORAGE_KEY = 'unibank_admin_user';

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    try {
      const stored = sessionStorage.getItem(STORAGE_KEY);
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  });

  useEffect(() => {
    if (user) {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(user));
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
  }, [user]);

  const login = useCallback((username, password) => {
    const account = SEED_ACCOUNTS.find(
      (a) => a.username === username && a.password === password
    );
    if (!account) return false;
    setUser({ username: account.username, name: account.name, role: account.role });
    return true;
  }, []);

  const logout = useCallback(() => setUser(null), []);

  const hasRole = useCallback(
    (roles) => {
      if (!user) return false;
      if (typeof roles === 'string') return user.role === roles;
      return roles.includes(user.role);
    },
    [user]
  );

  return (
    <AuthContext.Provider value={{ user, login, logout, hasRole }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
