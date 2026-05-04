import { Component } from 'react';
import { Box, Typography, Button, Paper } from '@mui/material';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';

/**
 * Top-level React error boundary for the bank-teller app (STORY-162).
 * Catches render-time exceptions and shows a friendly recovery screen.
 * Console output is sanitised to avoid leaking PII.
 */
export default class ErrorBoundary extends Component {
  state = { hasError: false };

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error, info) {
    // Log only the error message + stack — no props, no state, no PII
    // eslint-disable-next-line no-console
    console.error('[bank-teller] render error:', error?.message, info?.componentStack?.split('\n').slice(0, 5).join('\n'));
  }

  handleReload = () => {
    this.setState({ hasError: false });
    window.location.href = '/';
  };

  render() {
    if (!this.state.hasError) return this.props.children;

    return (
      <Box sx={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default' }}>
        <Paper sx={{ p: 4, maxWidth: 500, textAlign: 'center' }}>
          <ErrorOutlineIcon sx={{ fontSize: 64, color: 'error.main', mb: 2 }} />
          <Typography variant="h5" gutterBottom>Something went wrong</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            The application hit an unexpected error. No transaction has been lost,
            but you'll need to return to the dashboard and try again. Contact support
            if this keeps happening.
          </Typography>
          <Button variant="contained" onClick={this.handleReload}>
            Return to Dashboard
          </Button>
        </Paper>
      </Box>
    );
  }
}
