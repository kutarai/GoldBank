import { useState, useMemo } from 'react';
import {
  Box, Typography, Button, Chip, TextField, Grid,
  Dialog, DialogTitle, DialogContent, DialogActions, FormControlLabel, Switch,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { Add } from '@mui/icons-material';
import { generateBranches } from '../services/api';
import { useSnackbar } from '../services/snackbar';

export default function BranchManagement() {
  const notify = useSnackbar();
  const branches = useMemo(() => generateBranches(), []);
  const [editOpen, setEditOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [form, setForm] = useState({ name: '', code: '', address: '', city: '', phone: '', active: true });

  const openCreate = () => {
    setSelected(null);
    setForm({ name: '', code: '', address: '', city: '', phone: '', active: true });
    setEditOpen(true);
  };

  const openEdit = (branch) => {
    setSelected(branch);
    setForm({ name: branch.name, code: branch.code, address: branch.address, city: branch.city, phone: branch.phone, active: branch.active });
    setEditOpen(true);
  };

  const handleSave = () => {
    notify(selected ? `Branch ${form.name} updated` : `Branch ${form.name} created`);
    setEditOpen(false);
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Branch Management</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={openCreate}>Add Branch</Button>
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell><TableCell>Code</TableCell><TableCell>Address</TableCell>
              <TableCell>City</TableCell><TableCell>Phone</TableCell><TableCell>Status</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {branches.map((b) => (
              <TableRow key={b.id} hover>
                <TableCell>{b.name}</TableCell>
                <TableCell><Chip label={b.code} size="small" variant="outlined" /></TableCell>
                <TableCell>{b.address}</TableCell>
                <TableCell>{b.city}</TableCell>
                <TableCell>{b.phone}</TableCell>
                <TableCell><Chip label={b.active ? 'Active' : 'Inactive'} color={b.active ? 'success' : 'error'} size="small" /></TableCell>
                <TableCell><Button size="small" onClick={() => openEdit(b)}>Edit</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{selected ? 'Edit Branch' : 'Create Branch'}</DialogTitle>
        <DialogContent>
          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid size={6}><TextField fullWidth label="Branch Name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></Grid>
            <Grid size={6}><TextField fullWidth label="Code" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} disabled={!!selected} required /></Grid>
            <Grid size={12}><TextField fullWidth label="Address" value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} /></Grid>
            <Grid size={6}><TextField fullWidth label="City" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} /></Grid>
            <Grid size={6}><TextField fullWidth label="Phone" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></Grid>
            <Grid size={12}><FormControlLabel control={<Switch checked={form.active} onChange={(e) => setForm({ ...form, active: e.target.checked })} />} label="Active" /></Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave}>Save</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
