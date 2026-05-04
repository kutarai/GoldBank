import { createTheme } from '@mui/material/styles';

// Gold-on-dark theme
export const theme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#D4AF37',
      light: '#E5C76B',
      dark: '#8B6F1A',
      contrastText: '#1A1308',
    },
    secondary: {
      main: '#FFB300',
      light: '#FFD54F',
      dark: '#C68400',
      contrastText: '#1A1308',
    },
    error: { main: '#EF5350' },
    background: {
      default: '#15120A',
      paper: '#1F1A10',
    },
    text: {
      primary: '#F5E9C7',
      secondary: '#C9A227',
    },
  },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
  },
});
