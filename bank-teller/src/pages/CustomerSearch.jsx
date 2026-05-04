import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box, TextField, Typography, Paper, List, ListItemButton, ListItemText, Chip, CircularProgress,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { searchCustomers } from '../services/api';

const STATUS_COLOR = { active: 'success', frozen: 'info', suspended: 'warning', closed: 'default', pending_kyc: 'warning' };

export default function CustomerSearch() {
  const navigate = useNavigate();
  const [q, setQ] = useState('');
  const [results, setResults] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!q.trim()) { setResults([]); return; }
    const id = setTimeout(async () => {
      setLoading(true);
      try {
        const data = await searchCustomers(q);
        setResults(data);
      } catch { setResults([]); }
      finally { setLoading(false); }
    }, 300);
    return () => clearTimeout(id);
  }, [q]);

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3 }}>Find Customer</Typography>

      <TextField
        fullWidth
        autoFocus
        size="medium"
        placeholder="Search by name, phone, account number, or national ID…"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        slotProps={{ input: { startAdornment: <SearchIcon sx={{ mr: 1, color: 'text.secondary' }} /> } }}
        sx={{ mb: 2 }}
      />

      {loading && <CircularProgress size={20} />}

      {!loading && results.length === 0 && q && (
        <Typography color="text.secondary">No matches found.</Typography>
      )}

      {results.length > 0 && (
        <Paper variant="outlined">
          <List>
            {results.map(r => (
              <ListItemButton
                key={`${r.accountId}-${r.currency}`}
                onClick={() => navigate(`/customers/${r.accountId}`)}
              >
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <Typography variant="body1">
                        {r.name || r.shortId}
                        <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 1 }}>
                          {r.shortId}
                        </Typography>
                      </Typography>
                      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                        <Typography variant="body2">{r.currency} {Number(r.balance).toLocaleString()}</Typography>
                        <Chip label={r.status} size="small" color={STATUS_COLOR[r.status] || 'default'} />
                      </Box>
                    </Box>
                  }
                  secondary={`${r.phone} · KYC Level ${r.kycLevel}`}
                />
              </ListItemButton>
            ))}
          </List>
        </Paper>
      )}
    </Box>
  );
}
