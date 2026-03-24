import { createContext, useContext, useState, useCallback } from 'react';
import { Snackbar, Alert } from '@mui/material';

const Ctx = createContext(null);

export function SnackbarProvider({ children }) {
  const [state, setState] = useState({ open: false, message: '', severity: 'info' });

  const notify = useCallback((message, severity = 'success') => {
    setState({ open: true, message, severity });
  }, []);

  const handleClose = () => setState((s) => ({ ...s, open: false }));

  return (
    <Ctx.Provider value={notify}>
      {children}
      <Snackbar open={state.open} autoHideDuration={4000} onClose={handleClose} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
        <Alert onClose={handleClose} severity={state.severity} variant="filled">{state.message}</Alert>
      </Snackbar>
    </Ctx.Provider>
  );
}

export function useSnackbar() {
  return useContext(Ctx);
}
