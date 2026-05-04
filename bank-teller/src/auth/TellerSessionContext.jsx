import { createContext, useContext, useEffect, useState, useCallback } from 'react';
import { getCurrentDrawer, getUser, getToken, clearSession } from '../services/api';

const TellerSessionContext = createContext(null);

export function TellerSessionProvider({ children }) {
  const [user, setUser] = useState(() => getUser());
  const [drawer, setDrawer] = useState(null);
  const [loading, setLoading] = useState(true);

  const refreshDrawer = useCallback(async () => {
    if (!getToken()) { setDrawer(null); return; }
    try {
      const d = await getCurrentDrawer();
      setDrawer(d);
    } catch {
      setDrawer(null);
    }
  }, []);

  useEffect(() => {
    (async () => {
      if (getToken()) {
        await refreshDrawer();
      }
      setLoading(false);
    })();
  }, [refreshDrawer]);

  const logout = useCallback(() => {
    clearSession();
    setUser(null);
    setDrawer(null);
    window.location.href = '/login';
  }, []);

  const value = {
    user,
    setUser,
    drawer,
    setDrawer,
    refreshDrawer,
    logout,
    loading,
  };

  return (
    <TellerSessionContext.Provider value={value}>
      {children}
    </TellerSessionContext.Provider>
  );
}

export function useTellerSession() {
  const ctx = useContext(TellerSessionContext);
  if (!ctx) throw new Error('useTellerSession must be used within TellerSessionProvider');
  return ctx;
}
