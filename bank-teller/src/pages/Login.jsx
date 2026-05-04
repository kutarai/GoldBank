import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, Paper, TextField, Button, Typography, Alert, CircularProgress,
} from '@mui/material';
import { login } from '../services/api';
import { useTellerSession } from '../auth/TellerSessionContext';

export default function Login() {
  const navigate = useNavigate();
  const { setUser, refreshDrawer } = useTellerSession();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const u = await login(username, password);
      setUser(u);
      await refreshDrawer();
      navigate('/');
    } catch (err) {
      setError(err.message === 'invalid_credentials'
        ? 'Invalid username or password.'
        : err.message === 'role_not_allowed'
        ? 'Your role is not allowed to use the teller app.'
        : 'Login failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default' }}>
      <Paper sx={{ p: 4, width: 380 }}>
        <Typography variant="h5" gutterBottom>GoldBank Teller</Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          Sign in with your teller credentials
        </Typography>
        <form onSubmit={handleSubmit}>
          <TextField
            fullWidth
            label="Username"
            margin="normal"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoFocus
            required
          />
          <TextField
            fullWidth
            label="Password"
            type="password"
            margin="normal"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
          {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
          <Button
            type="submit"
            fullWidth
            variant="contained"
            sx={{ mt: 3, py: 1.2 }}
            disabled={loading || !username || !password}
          >
            {loading ? <CircularProgress size={22} /> : 'Sign In'}
          </Button>
        </form>
      </Paper>
    </Box>
  );
}
