import { useState } from 'react';
import {
  Box, Typography, Button, Chip, TextField, Grid, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, FormControlLabel, Switch,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Card, CardContent,
} from '@mui/material';
import { Add } from '@mui/icons-material';
import { useSnackbar } from '../services/snackbar';

const TRUST_STATUS_COLORS = { Verified: 'success', Probationary: 'warning', Suspended: 'error' };

const INITIAL_HOUSES = [
  {
    id: 'DH001',
    name: 'Fidelity Gold Refinery',
    address: '12 Kelvin Road, Msasa Industrial',
    city: 'Harare',
    phone: '+263 242 487 100',
    email: 'vault@fidelitygold.co.zw',
    licenseNumber: 'RBZ-DH-2019-001',
    apiEndpoint: 'https://api.fidelitygold.co.zw/vault/v1',
    trustStatus: 'Verified',
    active: true,
  },
  {
    id: 'DH002',
    name: 'ZB Bank Vault Services',
    address: '46 Speke Avenue, CBD',
    city: 'Harare',
    phone: '+263 242 751 631',
    email: 'custody@zbbank.co.zw',
    licenseNumber: 'RBZ-DH-2020-014',
    apiEndpoint: 'https://custody.zbbank.co.zw/api',
    trustStatus: 'Verified',
    active: true,
  },
  {
    id: 'DH003',
    name: 'Old Mutual Safe Deposit',
    address: '3 Jason Moyo Avenue',
    city: 'Bulawayo',
    phone: '+263 292 888 100',
    email: 'safedeposit@oldmutual.co.zw',
    licenseNumber: 'RBZ-DH-2021-007',
    apiEndpoint: '',
    trustStatus: 'Probationary',
    active: true,
  },
  {
    id: 'DH004',
    name: 'Stanbic Precious Metals',
    address: '59 Samora Machel Avenue',
    city: 'Harare',
    phone: '+263 242 759 471',
    email: 'preciousmetals@stanbic.co.zw',
    licenseNumber: 'RBZ-DH-2018-003',
    apiEndpoint: 'https://metals.stanbiczw.com/custody/v2',
    trustStatus: 'Verified',
    active: true,
  },
  {
    id: 'DH005',
    name: 'CBZ Custody Services',
    address: '1 Jason Moyo Avenue',
    city: 'Harare',
    phone: '+263 242 748 050',
    email: 'custody@cbz.co.zw',
    licenseNumber: 'RBZ-DH-2022-011',
    apiEndpoint: '',
    trustStatus: 'Suspended',
    active: false,
  },
];

const EMPTY_FORM = {
  name: '',
  address: '',
  city: '',
  phone: '',
  email: '',
  licenseNumber: '',
  apiEndpoint: '',
  trustStatus: 'Probationary',
  active: true,
};

export default function DepositHouses() {
  const notify = useSnackbar();
  const [houses, setHouses] = useState(INITIAL_HOUSES);
  const [editOpen, setEditOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);

  const stats = {
    total: houses.length,
    verified: houses.filter((h) => h.trustStatus === 'Verified').length,
    probationary: houses.filter((h) => h.trustStatus === 'Probationary').length,
    suspended: houses.filter((h) => h.trustStatus === 'Suspended').length,
  };

  const openCreate = () => {
    setSelected(null);
    setForm(EMPTY_FORM);
    setEditOpen(true);
  };

  const openEdit = (house) => {
    setSelected(house);
    setForm({
      name: house.name,
      address: house.address,
      city: house.city,
      phone: house.phone,
      email: house.email,
      licenseNumber: house.licenseNumber,
      apiEndpoint: house.apiEndpoint,
      trustStatus: house.trustStatus,
      active: house.active,
    });
    setEditOpen(true);
  };

  const handleSave = () => {
    if (selected) {
      setHouses((prev) =>
        prev.map((h) => (h.id === selected.id ? { ...h, ...form } : h))
      );
      notify(`Deposit house "${form.name}" updated`);
    } else {
      const newId = `DH${String(houses.length + 1).padStart(3, '0')}`;
      setHouses((prev) => [...prev, { id: newId, ...form }]);
      notify(`Deposit house "${form.name}" created`);
    }
    setEditOpen(false);
  };

  const setField = (key) => (e) => setForm((f) => ({ ...f, [key]: e.target.value }));

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5">Deposit House Management</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={openCreate}>
          Add Deposit House
        </Button>
      </Box>

      {/* Stats Cards */}
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[
          { label: 'Total', value: stats.total, color: 'text.primary' },
          { label: 'Verified', value: stats.verified, color: 'success.main' },
          { label: 'Probationary', value: stats.probationary, color: 'warning.main' },
          { label: 'Suspended', value: stats.suspended, color: 'error.main' },
        ].map((stat) => (
          <Grid size={{ xs: 6, sm: 3 }} key={stat.label}>
            <Card variant="outlined">
              <CardContent sx={{ textAlign: 'center', py: 2 }}>
                <Typography variant="h4" color={stat.color} fontWeight="bold">
                  {stat.value}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {stat.label}
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {/* Data Table */}
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Address</TableCell>
              <TableCell>City</TableCell>
              <TableCell>License Number</TableCell>
              <TableCell>Trust Status</TableCell>
              <TableCell>Active</TableCell>
              <TableCell>API Endpoint</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {houses.map((h) => (
              <TableRow key={h.id} hover>
                <TableCell>
                  <Typography variant="body2" fontWeight="medium">{h.name}</Typography>
                  <Typography variant="caption" color="text.secondary">{h.id}</Typography>
                </TableCell>
                <TableCell>{h.address}</TableCell>
                <TableCell>{h.city}</TableCell>
                <TableCell>
                  <Chip label={h.licenseNumber} size="small" variant="outlined" />
                </TableCell>
                <TableCell>
                  <Chip
                    label={h.trustStatus}
                    color={TRUST_STATUS_COLORS[h.trustStatus]}
                    size="small"
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    label={h.active ? 'Active' : 'Inactive'}
                    color={h.active ? 'success' : 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell>
                  {h.apiEndpoint ? (
                    <Typography
                      variant="caption"
                      sx={{
                        fontFamily: 'monospace',
                        maxWidth: 180,
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        whiteSpace: 'nowrap',
                        display: 'block',
                      }}
                      title={h.apiEndpoint}
                    >
                      {h.apiEndpoint}
                    </Typography>
                  ) : (
                    <Chip label="Manual" size="small" variant="outlined" color="default" />
                  )}
                </TableCell>
                <TableCell>
                  <Button size="small" onClick={() => openEdit(h)}>Edit</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create / Edit Dialog */}
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{selected ? 'Edit Deposit House' : 'Create Deposit House'}</DialogTitle>
        <DialogContent>
          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid size={12}>
              <TextField fullWidth label="Name" value={form.name} onChange={setField('name')} required />
            </Grid>
            <Grid size={12}>
              <TextField fullWidth label="Address" value={form.address} onChange={setField('address')} />
            </Grid>
            <Grid size={6}>
              <TextField fullWidth label="City" value={form.city} onChange={setField('city')} />
            </Grid>
            <Grid size={6}>
              <TextField fullWidth label="Contact Phone" value={form.phone} onChange={setField('phone')} />
            </Grid>
            <Grid size={12}>
              <TextField fullWidth label="Contact Email" type="email" value={form.email} onChange={setField('email')} />
            </Grid>
            <Grid size={6}>
              <TextField
                fullWidth
                label="License Number"
                value={form.licenseNumber}
                onChange={setField('licenseNumber')}
                disabled={!!selected}
                required
              />
            </Grid>
            <Grid size={6}>
              <TextField
                select
                fullWidth
                label="Trust Status"
                value={form.trustStatus}
                onChange={setField('trustStatus')}
              >
                {['Verified', 'Probationary', 'Suspended'].map((s) => (
                  <MenuItem key={s} value={s}>{s}</MenuItem>
                ))}
              </TextField>
            </Grid>
            <Grid size={12}>
              <TextField
                fullWidth
                label="API Endpoint (optional)"
                placeholder="https://..."
                value={form.apiEndpoint}
                onChange={setField('apiEndpoint')}
              />
            </Grid>
            <Grid size={12}>
              <FormControlLabel
                control={
                  <Switch
                    checked={form.active}
                    onChange={(e) => setForm((f) => ({ ...f, active: e.target.checked }))}
                  />
                }
                label="Active"
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave} disabled={!form.name || !form.licenseNumber}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
