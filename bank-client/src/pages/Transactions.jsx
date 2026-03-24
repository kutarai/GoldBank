import { useState, useMemo } from 'react';
import {
  Box, Typography, TextField, MenuItem, Button, Chip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Grid,
} from '@mui/material';
import { Search, Clear } from '@mui/icons-material';
import { generateTransactions } from '../services/api';

const STATUS_COLORS = { Completed: 'success', Pending: 'warning', Failed: 'error', Reversed: 'default' };

export default function Transactions() {
  const [filters, setFilters] = useState({ accountId: '', type: '', status: '', search: '' });
  const allTxns = useMemo(() => generateTransactions(), []);

  const filtered = allTxns.filter((t) => {
    if (filters.accountId && !t.accountId.includes(filters.accountId)) return false;
    if (filters.type && t.type !== filters.type) return false;
    if (filters.status && t.status !== filters.status) return false;
    if (filters.search && !t.reference.includes(filters.search) && !t.accountId.includes(filters.search)) return false;
    return true;
  });

  const update = (key, val) => setFilters((f) => ({ ...f, [key]: val }));

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Transactions</Typography>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, sm: 3 }}>
          <TextField size="small" fullWidth label="Account / Reference" value={filters.search} onChange={(e) => update('search', e.target.value)}
            slotProps={{ input: { startAdornment: <Search sx={{ mr: 1 }} /> } }} />
        </Grid>
        <Grid size={{ xs: 6, sm: 2 }}>
          <TextField select size="small" fullWidth label="Type" value={filters.type} onChange={(e) => update('type', e.target.value)}>
            <MenuItem value="">All</MenuItem>
            {['Purchase', 'Transfer', 'CashIn', 'CashOut', 'BillPay', 'P2P'].map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
          </TextField>
        </Grid>
        <Grid size={{ xs: 6, sm: 2 }}>
          <TextField select size="small" fullWidth label="Status" value={filters.status} onChange={(e) => update('status', e.target.value)}>
            <MenuItem value="">All</MenuItem>
            {['Completed', 'Pending', 'Failed', 'Reversed'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </TextField>
        </Grid>
        <Grid size={{ xs: 12, sm: 2 }}>
          <Button startIcon={<Clear />} onClick={() => setFilters({ accountId: '', type: '', status: '', search: '' })}>Clear</Button>
        </Grid>
      </Grid>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell><TableCell>Account</TableCell><TableCell>Type</TableCell>
              <TableCell align="right">Amount</TableCell><TableCell align="right">Fee</TableCell>
              <TableCell>Currency</TableCell><TableCell>Status</TableCell><TableCell>Reference</TableCell>
              <TableCell>Counterparty</TableCell><TableCell>Date</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((t) => (
              <TableRow key={t.id} hover>
                <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{t.id}</TableCell>
                <TableCell>{t.accountId}</TableCell>
                <TableCell>{t.type}</TableCell>
                <TableCell align="right">{t.amount.toLocaleString()}</TableCell>
                <TableCell align="right">{t.fee.toFixed(2)}</TableCell>
                <TableCell>{t.currency}</TableCell>
                <TableCell><Chip label={t.status} color={STATUS_COLORS[t.status]} size="small" /></TableCell>
                <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{t.reference}</TableCell>
                <TableCell>{t.counterparty}</TableCell>
                <TableCell>{t.date}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>{filtered.length} transactions</Typography>
    </Box>
  );
}
