import { createTheme } from '@mui/material/styles';

export function buildTheme(mode) {
  return createTheme({
    palette: {
      mode,
      primary: { main: '#1976d2' },
      secondary: { main: '#9c27b0' },
    },
    typography: {
      fontFamily: 'Roboto, Helvetica Neue, Arial, sans-serif',
    },
  });
}
