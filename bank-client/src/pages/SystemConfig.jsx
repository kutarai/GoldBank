import { useState } from 'react';
import {
  Box, Typography, TextField, Button, Card, CardContent, Grid, Chip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  Dialog, DialogTitle, DialogContent, DialogActions, Alert, IconButton, Tooltip,
  InputAdornment, MenuItem, Divider,
} from '@mui/material';
import { Edit, Save, Add, Delete, CreditCard, Settings, Security, Tune } from '@mui/icons-material';
import { useSnackbar } from '../services/snackbar';

const INITIAL_CONFIGS = [
  { id: 1, key: 'card.bin_prefix', value: '6275', tenantId: null, category: 'Card', description: 'Virtual card BIN prefix (4-6 digits). Used for generating card PANs on registration.' },
  { id: 2, key: 'card.pan_length', value: '16', tenantId: null, category: 'Card', description: 'Total PAN length including BIN and Luhn check digit.' },
  { id: 3, key: 'account.daily_limit_zwg', value: '50000', tenantId: null, category: 'Limits', description: 'Default daily transaction limit for ZWG accounts.' },
  { id: 4, key: 'account.daily_limit_usd', value: '10000', tenantId: null, category: 'Limits', description: 'Default daily transaction limit for USD accounts.' },
  { id: 5, key: 'account.monthly_limit_zwg', value: '200000', tenantId: null, category: 'Limits', description: 'Default monthly transaction limit for ZWG accounts.' },
  { id: 6, key: 'account.monthly_limit_usd', value: '50000', tenantId: null, category: 'Limits', description: 'Default monthly transaction limit for USD accounts.' },
  { id: 7, key: 'otp.ttl_seconds', value: '300', tenantId: null, category: 'Security', description: 'OTP validity period in seconds (default: 5 minutes).' },
  { id: 8, key: 'otp.max_attempts', value: '5', tenantId: null, category: 'Security', description: 'Maximum OTP verification attempts before lockout.' },
  { id: 9, key: 'pin.max_failed_attempts', value: '5', tenantId: null, category: 'Security', description: 'Maximum failed PIN attempts before account lockout.' },
  { id: 10, key: 'pin.lockout_minutes', value: '30', tenantId: null, category: 'Security', description: 'Account lockout duration after max failed PIN attempts.' },
  { id: 11, key: 'kyc.face_match_auto_approve', value: '0.80', tenantId: null, category: 'KYC', description: 'Minimum face match score for auto-approval (0.0-1.0).' },
  { id: 12, key: 'kyc.face_match_reject', value: '0.40', tenantId: null, category: 'KYC', description: 'Face match score below which KYC is auto-rejected.' },
  { id: 13, key: 'fraud.velocity_limit_count', value: '10', tenantId: null, category: 'Fraud', description: 'Max transactions in velocity window before flagging.' },
  { id: 14, key: 'fraud.velocity_window_minutes', value: '60', tenantId: null, category: 'Fraud', description: 'Velocity check time window in minutes.' },
  { id: 15, key: 'fraud.high_value_threshold_zwg', value: '100000', tenantId: null, category: 'Fraud', description: 'ZWG amount above which transactions trigger high-value alert.' },
  { id: 16, key: 'fraud.high_value_threshold_usd', value: '5000', tenantId: null, category: 'Fraud', description: 'USD amount above which transactions trigger high-value alert.' },
  { id: 17, key: 'loan.max_tenure_months', value: '48', tenantId: null, category: 'Loans', description: 'Maximum loan tenure in months.' },
  { id: 18, key: 'loan.min_credit_score', value: '200', tenantId: null, category: 'Loans', description: 'Minimum credit score required to apply for a loan.' },
  { id: 19, key: 'loan.income_variance_threshold', value: '10', tenantId: null, category: 'Loans', description: 'Income variance % above which a warning is triggered on loan review.' },
  { id: 20, key: 'switch.gateway_url', value: 'http://synergy-switch:5002', tenantId: null, category: 'Switch', description: 'SynergySwitch gRPC endpoint URL for card transaction routing.' },
  { id: 21, key: 'ai.ollama_url', value: 'http://goldbank-ollama:11434', tenantId: null, category: 'AI', description: 'Ollama inference endpoint URL for on-premise AI services.' },
  { id: 22, key: 'ai.model_name', value: 'qwen3-vl', tenantId: null, category: 'AI', description: 'Default AI model used for vision and text inference.' },
];

const CATEGORIES = ['All', 'Card', 'Limits', 'Security', 'KYC', 'Fraud', 'Loans', 'Switch', 'AI'];
const CATEGORY_ICONS = { Card: <CreditCard />, Limits: <Tune />, Security: <Security />, KYC: <Security />, Fraud: <Security />, Loans: <Tune />, Switch: <Settings />, AI: <Settings /> };

export default function SystemConfig() {
  const notify = useSnackbar();
  const [configs, setConfigs] = useState(INITIAL_CONFIGS);
  const [category, setCategory] = useState('All');
  const [editId, setEditId] = useState(null);
  const [editValue, setEditValue] = useState('');
  const [addOpen, setAddOpen] = useState(false);
  const [newConfig, setNewConfig] = useState({ key: '', value: '', tenantId: '', category: 'Card', description: '' });

  const filtered = category === 'All' ? configs : configs.filter((c) => c.category === category);

  const startEdit = (config) => { setEditId(config.id); setEditValue(config.value); };
  const saveEdit = (id) => {
    setConfigs((prev) => prev.map((c) => c.id === id ? { ...c, value: editValue } : c));
    notify(`Config updated: ${configs.find((c) => c.id === id)?.key}`);
    setEditId(null);
  };
  const cancelEdit = () => setEditId(null);

  const deleteConfig = (id) => {
    const key = configs.find((c) => c.id === id)?.key;
    setConfigs((prev) => prev.filter((c) => c.id !== id));
    notify(`Config deleted: ${key}`);
  };

  const addConfig = () => {
    setConfigs((prev) => [...prev, { ...newConfig, id: Math.max(...prev.map((c) => c.id)) + 1, tenantId: newConfig.tenantId || null }]);
    notify(`Config added: ${newConfig.key}`);
    setAddOpen(false);
    setNewConfig({ key: '', value: '', tenantId: '', category: 'Card', description: '' });
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">System Configuration</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={() => setAddOpen(true)}>Add Config</Button>
      </Box>

      <Alert severity="info" sx={{ mb: 2 }}>
        Changes take effect immediately for new operations. Existing sessions use cached values until refresh.
        Tenant-specific overrides take priority over global defaults.
      </Alert>

      {/* Category chips */}
      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
        {CATEGORIES.map((cat) => (
          <Chip key={cat} label={cat} variant={category === cat ? 'filled' : 'outlined'}
            color={category === cat ? 'primary' : 'default'} onClick={() => setCategory(cat)}
            icon={cat !== 'All' ? CATEGORY_ICONS[cat] : undefined}
            sx={{ cursor: 'pointer' }} />
        ))}
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Key</TableCell>
              <TableCell>Value</TableCell>
              <TableCell>Category</TableCell>
              <TableCell>Tenant</TableCell>
              <TableCell>Description</TableCell>
              <TableCell align="center">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((config) => (
              <TableRow key={config.id} hover>
                <TableCell>
                  <Typography variant="body2" fontFamily="monospace" fontWeight="medium">{config.key}</Typography>
                </TableCell>
                <TableCell>
                  {editId === config.id ? (
                    <TextField size="small" value={editValue} onChange={(e) => setEditValue(e.target.value)}
                      autoFocus sx={{ minWidth: 120 }}
                      onKeyDown={(e) => { if (e.key === 'Enter') saveEdit(config.id); if (e.key === 'Escape') cancelEdit(); }} />
                  ) : (
                    <Chip label={config.value} size="small" variant="outlined" sx={{ fontFamily: 'monospace' }} />
                  )}
                </TableCell>
                <TableCell><Chip label={config.category} size="small" color="primary" variant="outlined" /></TableCell>
                <TableCell>{config.tenantId || <Typography variant="caption" color="text.secondary">Global</Typography>}</TableCell>
                <TableCell><Typography variant="caption" color="text.secondary">{config.description}</Typography></TableCell>
                <TableCell align="center">
                  {editId === config.id ? (
                    <>
                      <Tooltip title="Save"><IconButton size="small" color="success" onClick={() => saveEdit(config.id)}><Save fontSize="small" /></IconButton></Tooltip>
                      <Button size="small" onClick={cancelEdit}>Cancel</Button>
                    </>
                  ) : (
                    <>
                      <Tooltip title="Edit"><IconButton size="small" onClick={() => startEdit(config)}><Edit fontSize="small" /></IconButton></Tooltip>
                      <Tooltip title="Delete"><IconButton size="small" color="error" onClick={() => deleteConfig(config.id)}><Delete fontSize="small" /></IconButton></Tooltip>
                    </>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Add Config Dialog */}
      <Dialog open={addOpen} onClose={() => setAddOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Configuration</DialogTitle>
        <DialogContent>
          <TextField fullWidth label="Key" margin="normal" value={newConfig.key} onChange={(e) => setNewConfig({ ...newConfig, key: e.target.value })}
            placeholder="e.g. card.bin_prefix" helperText="Use dot notation for namespacing" />
          <TextField fullWidth label="Value" margin="normal" value={newConfig.value} onChange={(e) => setNewConfig({ ...newConfig, value: e.target.value })} />
          <TextField select fullWidth label="Category" margin="normal" value={newConfig.category} onChange={(e) => setNewConfig({ ...newConfig, category: e.target.value })}>
            {CATEGORIES.filter((c) => c !== 'All').map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Tenant ID (leave blank for global)" margin="normal" value={newConfig.tenantId} onChange={(e) => setNewConfig({ ...newConfig, tenantId: e.target.value })} />
          <TextField fullWidth label="Description" margin="normal" multiline rows={2} value={newConfig.description} onChange={(e) => setNewConfig({ ...newConfig, description: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={addConfig} disabled={!newConfig.key || !newConfig.value}>Add</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
