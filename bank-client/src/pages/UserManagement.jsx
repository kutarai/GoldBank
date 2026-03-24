import { useState, useMemo } from 'react';
import {
  Box, Typography, Button, Chip, TextField, MenuItem, Grid,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { Add } from '@mui/icons-material';
import { generateAdminUsers, generateBranches } from '../services/api';
import { Roles } from '../auth/roles';
import { useSnackbar } from '../services/snackbar';

export default function UserManagement() {
  const notify = useSnackbar();
  const users = useMemo(() => generateAdminUsers(), []);
  const branches = useMemo(() => generateBranches(), []);
  const [editOpen, setEditOpen] = useState(false);
  const [deactivateOpen, setDeactivateOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [form, setForm] = useState({ username: '', fullName: '', email: '', password: '', role: '', branch: '' });
  const [deactivateReason, setDeactivateReason] = useState('');
  const [roleFilter, setRoleFilter] = useState('');

  const filtered = roleFilter ? users.filter((u) => u.role === roleFilter) : users;

  const openCreate = () => {
    setSelected(null);
    setForm({ username: '', fullName: '', email: '', password: '', role: '', branch: '' });
    setEditOpen(true);
  };

  const openEdit = (user) => {
    setSelected(user);
    setForm({ username: user.username, fullName: user.fullName, email: user.email, password: '', role: user.role, branch: user.branch });
    setEditOpen(true);
  };

  const handleSave = () => {
    notify(selected ? `User ${form.username} updated` : `User ${form.username} created`);
    setEditOpen(false);
  };

  const openDeactivate = (user) => { setSelected(user); setDeactivateReason(''); setDeactivateOpen(true); };
  const handleDeactivate = () => { notify(`User ${selected.username} deactivated`); setDeactivateOpen(false); };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">User Management</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={openCreate}>Add User</Button>
      </Box>

      <TextField select size="small" label="Filter by Role" value={roleFilter} onChange={(e) => setRoleFilter(e.target.value)} sx={{ mb: 2, minWidth: 180 }}>
        <MenuItem value="">All Roles</MenuItem>
        {Object.values(Roles).map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
      </TextField>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Username</TableCell><TableCell>Full Name</TableCell><TableCell>Email</TableCell>
              <TableCell>Role</TableCell><TableCell>Branch</TableCell><TableCell>Status</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((u) => (
              <TableRow key={u.id} hover>
                <TableCell>{u.username}</TableCell>
                <TableCell>{u.fullName}</TableCell>
                <TableCell>{u.email}</TableCell>
                <TableCell><Chip label={u.role} size="small" /></TableCell>
                <TableCell>{u.branch}</TableCell>
                <TableCell><Chip label={u.status} color={u.status === 'Active' ? 'success' : 'error'} size="small" /></TableCell>
                <TableCell>
                  <Button size="small" onClick={() => openEdit(u)}>Edit</Button>
                  <Button size="small" color="error" onClick={() => openDeactivate(u)}>Deactivate</Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create/Edit Dialog */}
      <Dialog open={editOpen} onClose={() => setEditOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{selected ? 'Edit User' : 'Create User'}</DialogTitle>
        <DialogContent>
          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid size={6}><TextField fullWidth label="Username" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} disabled={!!selected} required /></Grid>
            <Grid size={6}><TextField fullWidth label="Full Name" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required /></Grid>
            <Grid size={6}><TextField fullWidth label="Email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required /></Grid>
            {!selected && <Grid size={6}><TextField fullWidth label="Password" type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required /></Grid>}
            <Grid size={6}>
              <TextField select fullWidth label="Role" value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })} required>
                {Object.values(Roles).map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
              </TextField>
            </Grid>
            <Grid size={6}>
              <TextField select fullWidth label="Branch" value={form.branch} onChange={(e) => setForm({ ...form, branch: e.target.value })}>
                {branches.map((b) => <MenuItem key={b.id} value={b.name}>{b.name}</MenuItem>)}
              </TextField>
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSave}>Save</Button>
        </DialogActions>
      </Dialog>

      {/* Deactivate Dialog */}
      <Dialog open={deactivateOpen} onClose={() => setDeactivateOpen(false)}>
        <DialogTitle>Deactivate User</DialogTitle>
        <DialogContent>
          <Typography sx={{ mb: 2 }}>Deactivate <strong>{selected?.username}</strong> ({selected?.fullName})?</Typography>
          <TextField fullWidth label="Reason" required multiline rows={2} value={deactivateReason} onChange={(e) => setDeactivateReason(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeactivateOpen(false)}>Cancel</Button>
          <Button variant="contained" color="error" onClick={handleDeactivate} disabled={!deactivateReason}>Deactivate</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
