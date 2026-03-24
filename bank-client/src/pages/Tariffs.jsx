import { useState } from 'react';
import {
  Box, Typography, Button, Chip, Tab, Tabs, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, IconButton, Tooltip,
  Card, CardContent, Grid, Switch, FormControlLabel, InputAdornment,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import { Add, Edit, ContentCopy } from '@mui/icons-material';
import { useSnackbar } from '../services/snackbar';

const TRANSACTION_TYPES = [
  'P2P Transfer', 'Domestic Transfer', 'Cross-Border Transfer', 'Bill Payment',
  'NFC Payment', 'QR Payment', 'Cash In', 'Cash Out', 'Cheque Deposit',
  'Balance Enquiry', 'Statement Enquiry', 'Loan Disbursement', 'Loan Repayment',
];

const STUB_TARIFFS = [
  { id: 1, name: 'P2P Transfer', transactionType: 'P2P Transfer', currency: 'ZWG', feeType: 'percentage', feeValue: 1.0, minFee: 0.50, maxFee: 50.00, minAmount: 1.00, maxAmount: 50000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 2, name: 'P2P Transfer', transactionType: 'P2P Transfer', currency: 'USD', feeType: 'percentage', feeValue: 0.5, minFee: 0.10, maxFee: 25.00, minAmount: 0.50, maxAmount: 10000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 3, name: 'Domestic Transfer', transactionType: 'Domestic Transfer', currency: 'ZWG', feeType: 'percentage', feeValue: 1.0, minFee: 1.00, maxFee: 100.00, minAmount: 10.00, maxAmount: 100000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 4, name: 'Domestic Transfer', transactionType: 'Domestic Transfer', currency: 'USD', feeType: 'percentage', feeValue: 0.75, minFee: 0.50, maxFee: 50.00, minAmount: 1.00, maxAmount: 25000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 5, name: 'Cross-Border Transfer', transactionType: 'Cross-Border Transfer', currency: 'USD', feeType: 'percentage', feeValue: 2.5, minFee: 2.00, maxFee: 100.00, minAmount: 5.00, maxAmount: 10000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 6, name: 'Bill Payment', transactionType: 'Bill Payment', currency: 'ZWG', feeType: 'flat', feeValue: 2.00, minFee: null, maxFee: null, minAmount: 5.00, maxAmount: 50000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 7, name: 'Bill Payment', transactionType: 'Bill Payment', currency: 'USD', feeType: 'flat', feeValue: 0.50, minFee: null, maxFee: null, minAmount: 1.00, maxAmount: 10000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 8, name: 'NFC Payment', transactionType: 'NFC Payment', currency: 'ZWG', feeType: 'percentage', feeValue: 0.5, minFee: 0.10, maxFee: 25.00, minAmount: 0.50, maxAmount: 50000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 9, name: 'QR Payment', transactionType: 'QR Payment', currency: 'ZWG', feeType: 'percentage', feeValue: 0.5, minFee: 0.10, maxFee: 25.00, minAmount: 0.50, maxAmount: 50000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 10, name: 'Cash In (Agent)', transactionType: 'Cash In', currency: 'ZWG', feeType: 'flat', feeValue: 0.00, minFee: null, maxFee: null, minAmount: 5.00, maxAmount: 100000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 11, name: 'Cash Out (Agent)', transactionType: 'Cash Out', currency: 'ZWG', feeType: 'percentage', feeValue: 2.0, minFee: 1.00, maxFee: 100.00, minAmount: 5.00, maxAmount: 50000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 12, name: 'Cheque Deposit', transactionType: 'Cheque Deposit', currency: 'ZWG', feeType: 'flat', feeValue: 5.00, minFee: null, maxFee: null, minAmount: 50.00, maxAmount: 500000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 13, name: 'Balance Enquiry', transactionType: 'Balance Enquiry', currency: 'ZWG', feeType: 'flat', feeValue: 0.00, minFee: null, maxFee: null, minAmount: null, maxAmount: null, status: 'active', effectiveFrom: '2026-01-01', tier: 'Standard' },
  { id: 14, name: 'P2P Transfer - Premium', transactionType: 'P2P Transfer', currency: 'ZWG', feeType: 'percentage', feeValue: 0.5, minFee: 0.25, maxFee: 25.00, minAmount: 1.00, maxAmount: 100000.00, status: 'active', effectiveFrom: '2026-01-01', tier: 'Premium' },
];

export default function Tariffs() {
  const notify = useSnackbar();
  const [tab, setTab] = useState(0);
  const [tariffs, setTariffs] = useState(STUB_TARIFFS);
  const [editOpen, setEditOpen] = useState(false);
  const [form, setForm] = useState({});
  const [isNew, setIsNew] = useState(false);

  const currencies = ['All', 'ZWG', 'USD'];
  const filtered = tab === 0 ? tariffs : tariffs.filter((t) => t.currency === currencies[tab]);

  const handleEdit = (tariff) => {
    setForm({ ...tariff });
    setIsNew(false);
    setEditOpen(true);
  };

  const handleNew = () => {
    setForm({ id: tariffs.length + 1, name: '', transactionType: '', currency: 'ZWG', feeType: 'percentage', feeValue: 0, minFee: null, maxFee: null, minAmount: 0, maxAmount: 0, status: 'active', effectiveFrom: new Date().toISOString().slice(0, 10), tier: 'Standard' });
    setIsNew(true);
    setEditOpen(true);
  };

  const handleDuplicate = (tariff) => {
    const dup = { ...tariff, id: tariffs.length + 1, name: `${tariff.name} (copy)`, status: 'draft' };
    setTariffs((prev) => [...prev, dup]);
    notify(`Tariff duplicated: ${dup.name}`);
  };

  const handleSave = () => {
    if (isNew) {
      setTariffs((prev) => [...prev, form]);
      notify(`Tariff created: ${form.name}`);
    } else {
      setTariffs((prev) => prev.map((t) => t.id === form.id ? form : t));
      notify(`Tariff updated: ${form.name}`);
    }
    setEditOpen(false);
  };

  const columns = [
    { field: 'name', headerName: 'Tariff Name', flex: 1, minWidth: 180 },
    { field: 'transactionType', headerName: 'Transaction Type', width: 160 },
    { field: 'currency', headerName: 'Currency', width: 90, renderCell: (p) => <Chip label={p.value} size="small" variant="outlined" /> },
    { field: 'tier', headerName: 'Tier', width: 100, renderCell: (p) => <Chip label={p.value} size="small" color={p.value === 'Premium' ? 'secondary' : 'default'} /> },
    { field: 'feeType', headerName: 'Fee Type', width: 100, renderCell: (p) => p.value === 'percentage' ? '%' : 'Flat' },
    { field: 'feeValue', headerName: 'Fee', width: 90, align: 'right', renderCell: (p) => p.row.feeType === 'percentage' ? `${p.value}%` : `$${p.value.toFixed(2)}` },
    { field: 'minFee', headerName: 'Min Fee', width: 90, align: 'right', renderCell: (p) => p.value != null ? `$${p.value.toFixed(2)}` : '-' },
    { field: 'maxFee', headerName: 'Max Fee', width: 90, align: 'right', renderCell: (p) => p.value != null ? `$${p.value.toFixed(2)}` : '-' },
    { field: 'minAmount', headerName: 'Min Txn', width: 100, align: 'right', renderCell: (p) => p.value != null ? `$${p.value.toLocaleString()}` : '-' },
    { field: 'maxAmount', headerName: 'Max Txn', width: 110, align: 'right', renderCell: (p) => p.value != null ? `$${p.value.toLocaleString()}` : '-' },
    { field: 'status', headerName: 'Status', width: 90, renderCell: (p) => <Chip label={p.value} color={p.value === 'active' ? 'success' : 'default'} size="small" /> },
    { field: 'effectiveFrom', headerName: 'Effective', width: 110 },
    { field: 'actions', headerName: 'Actions', width: 120, sortable: false, renderCell: (p) => (
      <Box>
        <Tooltip title="Edit"><IconButton size="small" onClick={() => handleEdit(p.row)}><Edit fontSize="small" /></IconButton></Tooltip>
        <Tooltip title="Duplicate"><IconButton size="small" onClick={() => handleDuplicate(p.row)}><ContentCopy fontSize="small" /></IconButton></Tooltip>
      </Box>
    )},
  ];

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">Tariff Management</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleNew}>New Tariff</Button>
      </Box>

      {/* Summary */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[
          { label: 'Total Tariffs', value: tariffs.length, color: 'primary.main' },
          { label: 'ZWG Tariffs', value: tariffs.filter((t) => t.currency === 'ZWG').length, color: 'success.main' },
          { label: 'USD Tariffs', value: tariffs.filter((t) => t.currency === 'USD').length, color: 'info.main' },
          { label: 'Premium Tier', value: tariffs.filter((t) => t.tier === 'Premium').length, color: 'secondary.main' },
        ].map((s) => (
          <Grid key={s.label} size={{ xs: 6, md: 3 }}>
            <Card>
              <CardContent sx={{ textAlign: 'center', py: 1.5 }}>
                <Typography variant="h4" sx={{ color: s.color }}>{s.value}</Typography>
                <Typography variant="body2" color="text.secondary">{s.label}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="All Currencies" />
        <Tab label="ZWG" />
        <Tab label="USD" />
      </Tabs>

      <DataGrid rows={filtered} columns={columns} autoHeight pageSize={25} rowsPerPageOptions={[10, 25, 50]} disableRowSelectionOnClick
        sx={{ '& .MuiDataGrid-cell': { py: 0.5 } }} />

      {/* Edit / Create Dialog */}
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{isNew ? 'Create Tariff' : 'Edit Tariff'}</DialogTitle>
        <DialogContent>
          <TextField fullWidth label="Tariff Name" margin="normal" value={form.name || ''} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <TextField select fullWidth label="Transaction Type" margin="normal" value={form.transactionType || ''} onChange={(e) => setForm({ ...form, transactionType: e.target.value })}>
            {TRANSACTION_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
          </TextField>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}>
              <TextField select fullWidth label="Currency" margin="normal" value={form.currency || 'ZWG'} onChange={(e) => setForm({ ...form, currency: e.target.value })}>
                <MenuItem value="ZWG">ZWG</MenuItem>
                <MenuItem value="USD">USD</MenuItem>
              </TextField>
            </Grid>
            <Grid size={{ xs: 6 }}>
              <TextField select fullWidth label="Tier" margin="normal" value={form.tier || 'Standard'} onChange={(e) => setForm({ ...form, tier: e.target.value })}>
                <MenuItem value="Standard">Standard</MenuItem>
                <MenuItem value="Premium">Premium</MenuItem>
                <MenuItem value="Corporate">Corporate</MenuItem>
              </TextField>
            </Grid>
          </Grid>
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}>
              <TextField select fullWidth label="Fee Type" margin="normal" value={form.feeType || 'percentage'} onChange={(e) => setForm({ ...form, feeType: e.target.value })}>
                <MenuItem value="percentage">Percentage</MenuItem>
                <MenuItem value="flat">Flat Fee</MenuItem>
              </TextField>
            </Grid>
            <Grid size={{ xs: 6 }}>
              <TextField fullWidth label="Fee Value" margin="normal" type="number" value={form.feeValue ?? ''} onChange={(e) => setForm({ ...form, feeValue: parseFloat(e.target.value) || 0 })}
                InputProps={{ endAdornment: <InputAdornment position="end">{form.feeType === 'percentage' ? '%' : '$'}</InputAdornment> }} />
            </Grid>
          </Grid>
          {form.feeType === 'percentage' && (
            <Grid container spacing={2}>
              <Grid size={{ xs: 6 }}>
                <TextField fullWidth label="Min Fee ($)" margin="normal" type="number" value={form.minFee ?? ''} onChange={(e) => setForm({ ...form, minFee: parseFloat(e.target.value) || null })} />
              </Grid>
              <Grid size={{ xs: 6 }}>
                <TextField fullWidth label="Max Fee ($)" margin="normal" type="number" value={form.maxFee ?? ''} onChange={(e) => setForm({ ...form, maxFee: parseFloat(e.target.value) || null })} />
              </Grid>
            </Grid>
          )}
          <Grid container spacing={2}>
            <Grid size={{ xs: 6 }}>
              <TextField fullWidth label="Min Transaction Amount" margin="normal" type="number" value={form.minAmount ?? ''} onChange={(e) => setForm({ ...form, minAmount: parseFloat(e.target.value) || null })} />
            </Grid>
            <Grid size={{ xs: 6 }}>
              <TextField fullWidth label="Max Transaction Amount" margin="normal" type="number" value={form.maxAmount ?? ''} onChange={(e) => setForm({ ...form, maxAmount: parseFloat(e.target.value) || null })} />
            </Grid>
          </Grid>
          <TextField fullWidth label="Effective From" margin="normal" type="date" value={form.effectiveFrom || ''} onChange={(e) => setForm({ ...form, effectiveFrom: e.target.value })} InputLabelProps={{ shrink: true }} />
          <FormControlLabel control={<Switch checked={form.status === 'active'} onChange={(e) => setForm({ ...form, status: e.target.checked ? 'active' : 'draft' })} />}
            label={form.status === 'active' ? 'Active' : 'Draft'} sx={{ mt: 1 }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave}>{isNew ? 'Create' : 'Save'}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
