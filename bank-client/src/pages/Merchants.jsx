import { useState } from 'react';
import {
  Box, Typography, Button, Chip, Tab, Tabs, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, IconButton, Tooltip,
  Card, CardContent, Grid, Switch, FormControlLabel, InputAdornment,
  Alert, List, ListItem, ListItemText, ListItemIcon, Divider,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import {
  Add, Edit, Block, CheckCircle, DeviceHub, Sync, Search,
  History, PowerSettingsNew,
} from '@mui/icons-material';
import { useSnackbar } from '../services/snackbar';

// ── Stub Data ────────────────────────────────────────────────────────────────
const STUB_MERCHANTS = [
  { id: 'M001', name: 'OK Supermarket - Borrowdale', category: 'Retail', contactName: 'John Moyo', phone: '+263772100001', email: 'ok.borrowdale@okzim.co.zw', commission: 1.5, status: 'active', devices: 3, lastRecon: '2026-03-23', createdAt: '2025-06-15' },
  { id: 'M002', name: 'TM Pick n Pay - Eastgate', category: 'Retail', contactName: 'Mary Ndlovu', phone: '+263772100002', email: 'tm.eastgate@tm.co.zw', commission: 1.5, status: 'active', devices: 5, lastRecon: '2026-03-23', createdAt: '2025-07-20' },
  { id: 'M003', name: 'Chicken Inn - Samora', category: 'Food & Beverage', contactName: 'Peter Chigumba', phone: '+263772100003', email: 'ci.samora@innscor.co.zw', commission: 2.0, status: 'active', devices: 2, lastRecon: '2026-03-22', createdAt: '2025-08-10' },
  { id: 'M004', name: 'N. Richards Pharmacy', category: 'Health', contactName: 'Nurse Richards', phone: '+263772100004', email: 'info@nrichards.co.zw', commission: 1.0, status: 'suspended', devices: 1, lastRecon: '2026-03-15', createdAt: '2025-09-01' },
  { id: 'M005', name: 'Zuva Fuel - Msasa', category: 'Fuel', contactName: 'Taurai Makoni', phone: '+263772100005', email: 'msasa@zuva.co.zw', commission: 0.5, status: 'active', devices: 2, lastRecon: '2026-03-23', createdAt: '2025-10-12' },
  { id: 'M006', name: 'Edgars - Joina City', category: 'Clothing', contactName: 'Grace Mupfumi', phone: '+263772100006', email: 'joina@edgars.co.zw', commission: 1.5, status: 'pending', devices: 0, lastRecon: null, createdAt: '2026-03-20' },
  { id: 'M007', name: 'Bon Marche - Avondale', category: 'Retail', contactName: 'David Chirwa', phone: '+263772100007', email: 'avondale@bonmarche.co.zw', commission: 1.5, status: 'active', devices: 4, lastRecon: '2026-03-23', createdAt: '2025-05-08' },
  { id: 'M008', name: 'Delta Beverages - Depot', category: 'Wholesale', contactName: 'Tinashe Dube', phone: '+263772100008', email: 'depot@delta.co.zw', commission: 0.8, status: 'closed', devices: 0, lastRecon: '2026-01-30', createdAt: '2024-11-15' },
];

const INITIAL_DEVICES = [
  { id: 'T001', merchantId: 'M001', terminalId: 'TID-20001', model: 'PAX A920', serialNumber: 'SN-A920-001', status: 'active', lastTxn: '2026-03-23 14:22' },
  { id: 'T002', merchantId: 'M001', terminalId: 'TID-20002', model: 'PAX A920', serialNumber: 'SN-A920-002', status: 'active', lastTxn: '2026-03-23 13:45' },
  { id: 'T003', merchantId: 'M001', terminalId: 'TID-20003', model: 'Ingenico Move 5000', serialNumber: 'SN-M5K-001', status: 'faulty', lastTxn: '2026-03-20 09:10' },
  { id: 'T004', merchantId: 'M002', terminalId: 'TID-30001', model: 'PAX A920', serialNumber: 'SN-A920-003', status: 'active', lastTxn: '2026-03-23 15:00' },
  { id: 'T005', merchantId: 'M002', terminalId: 'TID-30002', model: 'Verifone V240m', serialNumber: 'SN-V240-001', status: 'active', lastTxn: '2026-03-23 12:30' },
];

const STUB_RECON = [
  { id: 'R001', merchantId: 'M001', date: '2026-03-23', totalTxns: 142, totalAmount: 45230.50, settled: 44800.00, commission: 678.45, discrepancies: 0, status: 'matched' },
  { id: 'R002', merchantId: 'M002', date: '2026-03-23', totalTxns: 218, totalAmount: 67450.00, settled: 67450.00, commission: 1011.75, discrepancies: 0, status: 'matched' },
  { id: 'R003', merchantId: 'M003', date: '2026-03-22', totalTxns: 89, totalAmount: 12340.00, settled: 12100.00, commission: 246.80, discrepancies: 1, status: 'discrepancy' },
  { id: 'R004', merchantId: 'M005', date: '2026-03-23', totalTxns: 310, totalAmount: 156000.00, settled: 156000.00, commission: 780.00, discrepancies: 0, status: 'matched' },
];

const statusColor = { active: 'success', suspended: 'warning', pending: 'info', closed: 'default' };

export default function Merchants() {
  const notify = useSnackbar();
  const [tab, setTab] = useState(0);
  const [search, setSearch] = useState('');
  const [merchants, setMerchants] = useState(STUB_MERCHANTS);
  const [devices, setDevices] = useState(INITIAL_DEVICES);

  // Audit log — persists across all actions in the session
  const [auditLog, setAuditLog] = useState([]);
  const [auditOpen, setAuditOpen] = useState(false);

  // Edit dialog
  const [editOpen, setEditOpen] = useState(false);
  const [form, setForm] = useState({});

  // Merchant status toggle dialog
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [statusTarget, setStatusTarget] = useState(null); // merchant to toggle
  const [statusReason, setStatusReason] = useState('');

  // Device dialog
  const [deviceOpen, setDeviceOpen] = useState(false);
  const [selectedMerchantId, setSelectedMerchantId] = useState(null);

  // Device status toggle dialog
  const [deviceStatusOpen, setDeviceStatusOpen] = useState(false);
  const [deviceTarget, setDeviceTarget] = useState(null);
  const [deviceReason, setDeviceReason] = useState('');

  // Recon dialog
  const [reconOpen, setReconOpen] = useState(false);

  // ── Helpers ──────────────────────────────────────────────────────────────
  const logEvent = (action, entityType, entityId, entityName, reason) => {
    setAuditLog((prev) => [{
      id: prev.length + 1,
      timestamp: new Date().toLocaleString(),
      action,
      entityType,
      entityId,
      entityName,
      reason,
      user: 'admin',
    }, ...prev]);
  };

  const filtered = merchants.filter((m) => {
    const q = search.toLowerCase();
    const matchesSearch = !q || m.name.toLowerCase().includes(q) || m.id.toLowerCase().includes(q) || m.category.toLowerCase().includes(q);
    if (tab === 0) return matchesSearch;
    if (tab === 1) return matchesSearch && m.status === 'active';
    if (tab === 2) return matchesSearch && m.status === 'suspended';
    if (tab === 3) return matchesSearch && m.status === 'pending';
    return matchesSearch;
  });

  // ── Merchant Edit ───────────────────────────────────────────────────────
  const handleEdit = (merchant) => { setForm({ ...merchant }); setEditOpen(true); };

  const handleSave = () => {
    setMerchants((prev) => prev.map((m) => m.id === form.id ? { ...m, ...form } : m));
    logEvent('Edit', 'Merchant', form.id, form.name, 'Profile updated');
    notify(`Merchant ${form.name} updated`);
    setEditOpen(false);
  };

  // ── Merchant Suspend / Activate (with reason) ──────────────────────────
  const openStatusDialog = (merchant) => {
    setStatusTarget(merchant);
    setStatusReason('');
    setStatusDialogOpen(true);
  };

  const confirmStatusToggle = () => {
    const newStatus = statusTarget.status === 'active' ? 'suspended' : 'active';
    const actionLabel = newStatus === 'active' ? 'Activated' : 'Suspended';
    setMerchants((prev) => prev.map((m) => m.id === statusTarget.id ? { ...m, status: newStatus } : m));
    logEvent(actionLabel, 'Merchant', statusTarget.id, statusTarget.name, statusReason);
    notify(`${statusTarget.name} ${actionLabel.toLowerCase()}`);
    setStatusDialogOpen(false);
  };

  // ── Devices ─────────────────────────────────────────────────────────────
  const handleDevices = (merchantId) => { setSelectedMerchantId(merchantId); setDeviceOpen(true); };

  const merchantDevices = devices.filter((d) => d.merchantId === selectedMerchantId);

  const openDeviceStatusDialog = (device) => {
    setDeviceTarget(device);
    setDeviceReason('');
    setDeviceStatusOpen(true);
  };

  const confirmDeviceToggle = () => {
    const newStatus = deviceTarget.status === 'active' ? 'deactivated' : 'active';
    const actionLabel = newStatus === 'active' ? 'Activated' : 'Deactivated';
    setDevices((prev) => prev.map((d) => d.id === deviceTarget.id ? { ...d, status: newStatus } : d));
    const merchantName = merchants.find((m) => m.id === deviceTarget.merchantId)?.name || deviceTarget.merchantId;
    logEvent(`Device ${actionLabel}`, 'Device', deviceTarget.terminalId, `${deviceTarget.model} (${merchantName})`, deviceReason);
    notify(`${deviceTarget.terminalId} ${actionLabel.toLowerCase()}`);
    setDeviceStatusOpen(false);
  };

  // ── Recon ───────────────────────────────────────────────────────────────
  const handleRecon = (merchantId) => { setSelectedMerchantId(merchantId); setReconOpen(true); };
  const merchantRecon = STUB_RECON.filter((r) => r.merchantId === selectedMerchantId);

  // ── Columns ─────────────────────────────────────────────────────────────
  const columns = [
    { field: 'id', headerName: 'ID', width: 80 },
    { field: 'name', headerName: 'Merchant Name', flex: 1, minWidth: 200 },
    { field: 'category', headerName: 'Category', width: 140 },
    { field: 'contactName', headerName: 'Contact', width: 140 },
    { field: 'phone', headerName: 'Phone', width: 140 },
    { field: 'commission', headerName: 'Commission %', width: 120, renderCell: (p) => `${p.value}%` },
    { field: 'devices', headerName: 'Devices', width: 80, align: 'center' },
    { field: 'status', headerName: 'Status', width: 110, renderCell: (p) => <Chip label={p.value} color={statusColor[p.value] || 'default'} size="small" /> },
    { field: 'actions', headerName: 'Actions', width: 200, sortable: false, renderCell: (p) => (
      <Box>
        <Tooltip title="Edit"><IconButton size="small" onClick={() => handleEdit(p.row)}><Edit fontSize="small" /></IconButton></Tooltip>
        <Tooltip title={p.row.status === 'active' ? 'Suspend' : 'Activate'}>
          <IconButton size="small" onClick={() => openStatusDialog(p.row)} color={p.row.status === 'active' ? 'warning' : 'success'}>
            {p.row.status === 'active' ? <Block fontSize="small" /> : <CheckCircle fontSize="small" />}
          </IconButton>
        </Tooltip>
        <Tooltip title="Devices"><IconButton size="small" onClick={() => handleDevices(p.row.id)}><DeviceHub fontSize="small" /></IconButton></Tooltip>
        <Tooltip title="Reconciliation"><IconButton size="small" onClick={() => handleRecon(p.row.id)}><Sync fontSize="small" /></IconButton></Tooltip>
      </Box>
    )},
  ];

  const deviceColumns = [
    { field: 'terminalId', headerName: 'Terminal ID', width: 130 },
    { field: 'model', headerName: 'Model', width: 160 },
    { field: 'serialNumber', headerName: 'Serial Number', width: 160 },
    { field: 'status', headerName: 'Status', width: 110, renderCell: (p) => (
      <Chip label={p.value} color={p.value === 'active' ? 'success' : p.value === 'deactivated' ? 'default' : 'error'} size="small" />
    )},
    { field: 'lastTxn', headerName: 'Last Transaction', width: 160 },
    { field: 'actions', headerName: 'Actions', width: 100, sortable: false, renderCell: (p) => (
      <Tooltip title={p.row.status === 'active' ? 'Deactivate' : 'Activate'}>
        <IconButton size="small" onClick={() => openDeviceStatusDialog(p.row)} color={p.row.status === 'active' ? 'warning' : 'success'}>
          <PowerSettingsNew fontSize="small" />
        </IconButton>
      </Tooltip>
    )},
  ];

  const reconColumns = [
    { field: 'date', headerName: 'Date', width: 110 },
    { field: 'totalTxns', headerName: 'Transactions', width: 110, align: 'right' },
    { field: 'totalAmount', headerName: 'Total Amount', width: 130, align: 'right', renderCell: (p) => `$${p.value.toLocaleString()}` },
    { field: 'settled', headerName: 'Settled', width: 130, align: 'right', renderCell: (p) => `$${p.value.toLocaleString()}` },
    { field: 'commission', headerName: 'Commission', width: 120, align: 'right', renderCell: (p) => `$${p.value.toFixed(2)}` },
    { field: 'discrepancies', headerName: 'Discrepancies', width: 120, align: 'center' },
    { field: 'status', headerName: 'Status', width: 110, renderCell: (p) => <Chip label={p.value} color={p.value === 'matched' ? 'success' : 'error'} size="small" /> },
  ];

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">Merchant Management</Typography>
        <Box>
          <Button variant="outlined" startIcon={<History />} onClick={() => setAuditOpen(true)} sx={{ mr: 1 }}
            disabled={auditLog.length === 0}>
            Event Log ({auditLog.length})
          </Button>
          <Button variant="contained" startIcon={<Add />}>Register Merchant</Button>
        </Box>
      </Box>

      {/* Stats */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[
          { label: 'Total Merchants', value: merchants.length, color: 'primary.main' },
          { label: 'Active', value: merchants.filter((m) => m.status === 'active').length, color: 'success.main' },
          { label: 'Suspended', value: merchants.filter((m) => m.status === 'suspended').length, color: 'warning.main' },
          { label: 'Pending Approval', value: merchants.filter((m) => m.status === 'pending').length, color: 'info.main' },
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

      {/* Tabs + Search */}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Tabs value={tab} onChange={(_, v) => setTab(v)}>
          <Tab label="All" />
          <Tab label="Active" />
          <Tab label="Suspended" />
          <Tab label="Pending" />
        </Tabs>
        <TextField size="small" placeholder="Search merchants..." value={search} onChange={(e) => setSearch(e.target.value)}
          InputProps={{ startAdornment: <InputAdornment position="start"><Search /></InputAdornment> }} />
      </Box>

      {/* Data Grid */}
      <DataGrid rows={filtered} columns={columns} autoHeight pageSize={10} rowsPerPageOptions={[10, 25]} disableRowSelectionOnClick
        sx={{ '& .MuiDataGrid-cell': { py: 1 } }} />

      {/* ── Merchant Edit Dialog ──────────────────────────────────────────── */}
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Edit Merchant</DialogTitle>
        <DialogContent>
          <TextField fullWidth label="Merchant Name" margin="normal" value={form.name || ''} onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <TextField select fullWidth label="Category" margin="normal" value={form.category || ''} onChange={(e) => setForm({ ...form, category: e.target.value })}>
            {['Retail', 'Food & Beverage', 'Health', 'Fuel', 'Clothing', 'Wholesale', 'Services'].map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Contact Name" margin="normal" value={form.contactName || ''} onChange={(e) => setForm({ ...form, contactName: e.target.value })} />
          <TextField fullWidth label="Phone" margin="normal" value={form.phone || ''} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
          <TextField fullWidth label="Email" margin="normal" value={form.email || ''} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          <TextField fullWidth label="Commission %" margin="normal" type="number" value={form.commission || ''} onChange={(e) => setForm({ ...form, commission: parseFloat(e.target.value) })}
            InputProps={{ endAdornment: <InputAdornment position="end">%</InputAdornment> }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave}>Save</Button>
        </DialogActions>
      </Dialog>

      {/* ── Merchant Suspend / Activate Dialog ────────────────────────────── */}
      <Dialog open={statusDialogOpen} onClose={() => setStatusDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>
          {statusTarget?.status === 'active' ? 'Suspend Merchant' : 'Activate Merchant'}
        </DialogTitle>
        <DialogContent>
          <Alert severity={statusTarget?.status === 'active' ? 'warning' : 'success'} sx={{ mb: 2 }}>
            {statusTarget?.status === 'active'
              ? `Suspending "${statusTarget?.name}" will prevent all transactions from their terminals.`
              : `Activating "${statusTarget?.name}" will re-enable transactions from their terminals.`
            }
          </Alert>
          <TextField
            fullWidth label="Reason (required)" margin="normal" multiline rows={3}
            value={statusReason} onChange={(e) => setStatusReason(e.target.value)}
            placeholder={statusTarget?.status === 'active' ? 'e.g. Non-compliance with merchant agreement' : 'e.g. Compliance issues resolved'}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setStatusDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            color={statusTarget?.status === 'active' ? 'warning' : 'success'}
            disabled={!statusReason.trim()}
            onClick={confirmStatusToggle}
          >
            {statusTarget?.status === 'active' ? 'Suspend' : 'Activate'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── Device Dialog ─────────────────────────────────────────────────── */}
      <Dialog open={deviceOpen} onClose={() => setDeviceOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>
          Devices — {merchants.find((m) => m.id === selectedMerchantId)?.name}
          <Button variant="outlined" size="small" startIcon={<Add />} sx={{ ml: 2 }}>Issue Device</Button>
        </DialogTitle>
        <DialogContent>
          <DataGrid rows={merchantDevices} columns={deviceColumns} autoHeight pageSize={5} disableRowSelectionOnClick />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeviceOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* ── Device Activate / Deactivate Dialog ───────────────────────────── */}
      <Dialog open={deviceStatusOpen} onClose={() => setDeviceStatusOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>
          {deviceTarget?.status === 'active' ? 'Deactivate Device' : 'Activate Device'}
        </DialogTitle>
        <DialogContent>
          <Alert severity={deviceTarget?.status === 'active' ? 'warning' : 'success'} sx={{ mb: 2 }}>
            {deviceTarget?.status === 'active'
              ? `Deactivating terminal ${deviceTarget?.terminalId} (${deviceTarget?.model}) will stop it from processing transactions.`
              : `Activating terminal ${deviceTarget?.terminalId} (${deviceTarget?.model}) will allow it to process transactions.`
            }
          </Alert>
          <TextField
            fullWidth label="Reason (required)" margin="normal" multiline rows={3}
            value={deviceReason} onChange={(e) => setDeviceReason(e.target.value)}
            placeholder={deviceTarget?.status === 'active' ? 'e.g. Device reported stolen' : 'e.g. Device recovered and verified'}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeviceStatusOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            color={deviceTarget?.status === 'active' ? 'warning' : 'success'}
            disabled={!deviceReason.trim()}
            onClick={confirmDeviceToggle}
          >
            {deviceTarget?.status === 'active' ? 'Deactivate' : 'Activate'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── Reconciliation Dialog ─────────────────────────────────────────── */}
      <Dialog open={reconOpen} onClose={() => setReconOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>
          Reconciliation — {merchants.find((m) => m.id === selectedMerchantId)?.name}
        </DialogTitle>
        <DialogContent>
          {merchantRecon.length > 0 ? (
            <DataGrid rows={merchantRecon} columns={reconColumns} autoHeight pageSize={5} disableRowSelectionOnClick />
          ) : (
            <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>No reconciliation records found.</Typography>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReconOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* ── Event Log Dialog ──────────────────────────────────────────────── */}
      <Dialog open={auditOpen} onClose={() => setAuditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Event Log</DialogTitle>
        <DialogContent>
          {auditLog.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 3, textAlign: 'center' }}>No events recorded yet.</Typography>
          ) : (
            <List dense>
              {auditLog.map((entry, i) => (
                <Box key={entry.id}>
                  {i > 0 && <Divider />}
                  <ListItem alignItems="flex-start">
                    <ListItemIcon sx={{ mt: 1.5, minWidth: 36 }}>
                      {entry.action.includes('Suspend') || entry.action.includes('Deactivat') ? <Block color="warning" fontSize="small" /> : <CheckCircle color="success" fontSize="small" />}
                    </ListItemIcon>
                    <ListItemText
                      primary={<Typography variant="body2"><strong>{entry.action}</strong> — {entry.entityType} {entry.entityId}</Typography>}
                      secondary={
                        <>
                          <Typography variant="caption" component="span" color="text.primary">{entry.entityName}</Typography>
                          <br />
                          <Typography variant="caption" component="span">Reason: {entry.reason}</Typography>
                          <br />
                          <Typography variant="caption" component="span" color="text.secondary">By {entry.user} at {entry.timestamp}</Typography>
                        </>
                      }
                    />
                  </ListItem>
                </Box>
              ))}
            </List>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAuditOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
