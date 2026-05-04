import { createTheme } from '@mui/material/styles';

export function buildTheme(mode) {
  return createTheme({
    palette: {
      mode,
      primary: {
        main: '#C9A227',
        dark: '#8B6F1A',
        light: '#E5C76B',
        contrastText: '#1A1308',
      },
      secondary: {
        main: '#3D2E12',
        light: '#6B4F1D',
        dark: '#1F1A10',
        contrastText: '#F5E9C7',
      },
      warning: { main: '#FFB300' },
    },
    typography: {
      fontFamily: 'Roboto, Helvetica Neue, Arial, sans-serif',
    },
  });
}
