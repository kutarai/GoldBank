import { useEffect, useState, useRef, createContext, useContext } from 'react';
import {
  Box, Dialog, DialogTitle, DialogContent, DialogActions, TextField, Button,
  Alert, CircularProgress, Typography, Paper,
} from '@mui/material';
import LockIcon from '@mui/icons-material/Lock';
import CloudOffIcon from '@mui/icons-material/CloudOff';
import { Outlet } from 'react-router-dom';
import { login, getUser, clearSession } from '../services/api';

// 10 minutes of inactivity → lock
const IDLE_LIMIT_MS = 10 * 60 * 1000;
// Health check every 30 seconds
const HEALTH_INTERVAL_MS = 30 * 1000;
const HEALTH_TIMEOUT_MS  = 5 * 1000;
const HEALTH_URL = 'http://localhost:5001/health';

// ── Context so screens can disable mutating buttons when offline ──
const SecurityContext = createContext({ online: true, locked: false });
export function useSecurityState() {
  return useContext(SecurityContext);
}

export default function SecurityShell() {
  const [locked, setLocked] = useState(false);
  const [online, setOnline] = useState(true);
  const [unlockUser, setUnlockUser] = useState('');
  const [unlockPin, setUnlockPin] = useState('');
  const [unlockErr, setUnlockErr] = useState(null);
  const [unlockLoading, setUnlockLoading] = useState(false);
  const lastActivityRef = useRef(Date.now());

  // Pre-fill the unlock username from the current session
  useEffect(() => {
    const u = getUser();
    if (u?.username) setUnlockUser(u.username);
  }, [locked]);

  // ── Idle detection ────────────────────────────────────────────────
  useEffect(() => {
    const bump = () => { lastActivityRef.current = Date.now(); };
    window.addEventListener('mousemove', bump);
    window.addEventListener('keydown', bump);
    window.addEventListener('click', bump);
    return () => {
      window.removeEventListener('mousemove', bump);
      window.removeEventListener('keydown', bump);
      window.removeEventListener('click', bump);
    };
  }, []);

  useEffect(() => {
    const id = setInterval(() => {
      if (Date.now() - lastActivityRef.current > IDLE_LIMIT_MS) {
        setLocked(true);
      }
    }, 5000);
    return () => clearInterval(id);
  }, []);

  // ── Offline detection (gateway health ping) ───────────────────────
  useEffect(() => {
    let cancelled = false;
    const ping = async () => {
      try {
        const ctrl = new AbortController();
        const timeout = setTimeout(() => ctrl.abort(), HEALTH_TIMEOUT_MS);
        const res = await fetch(HEALTH_URL, { signal: ctrl.signal, cache: 'no-store' });
        clearTimeout(timeout);
        if (!cancelled) setOnline(res.ok);
      } catch {
        if (!cancelled) setOnline(false);
      }
    };
    ping();
    const id = setInterval(ping, HEALTH_INTERVAL_MS);
    return () => { cancelled = true; clearInterval(id); };
  }, []);

  const handleUnlock = async () => {
    setUnlockLoading(true);
    setUnlockErr(null);
    try {
      // Re-authenticate using the existing login endpoint
      await login(unlockUser, unlockPin);
      lastActivityRef.current = Date.now();
      setLocked(false);
      setUnlockPin('');
    } catch {
      setUnlockErr('Invalid credentials.');
    } finally {
      setUnlockLoading(false);
    }
  };

  const handleSignOut = () => {
    clearSession();
    window.location.href = '/login';
  };

  return (
    <SecurityContext.Provider value={{ online, locked }}>
      {/* Offline banner */}
      {!online && (
        <Box sx={{
          position: 'sticky', top: 0, zIndex: 1300,
          bgcolor: 'error.dark', color: 'white',
          px: 2, py: 1,
          display: 'flex', alignItems: 'center', gap: 1,
        }}>
          <CloudOffIcon fontSize="small" />
          <Typography variant="body2">
            Gateway unreachable — cash transactions are blocked until the connection is restored.
          </Typography>
        </Box>
      )}

      <Outlet />

      {/* Lock screen */}
      <Dialog
        open={locked}
        disableEscapeKeyDown
        slotProps={{ backdrop: { sx: { backdropFilter: 'blur(4px)' } } }}
      >
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <LockIcon color="warning" />
          Session Locked
        </DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Your session was locked due to inactivity. Enter your password to resume.
          </Typography>
          <TextField fullWidth margin="normal" label="Username"
            value={unlockUser} onChange={(e) => setUnlockUser(e.target.value)} />
          <TextField fullWidth margin="normal" label="Password" type="password"
            value={unlockPin} onChange={(e) => setUnlockPin(e.target.value)} autoFocus
            onKeyDown={(e) => { if (e.key === 'Enter') handleUnlock(); }} />
          {unlockErr && <Alert severity="error" sx={{ mt: 1 }}>{unlockErr}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={handleSignOut}>Sign Out</Button>
          <Button variant="contained" disabled={unlockLoading || !unlockUser || !unlockPin}
            onClick={handleUnlock}>
            {unlockLoading ? <CircularProgress size={20} /> : 'Unlock'}
          </Button>
        </DialogActions>
      </Dialog>
    </SecurityContext.Provider>
  );
}
