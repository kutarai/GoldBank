import { useState, useMemo } from 'react';
import {
  Box, Typography, Tabs, Tab, Chip, Button, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, FormControlLabel, Checkbox,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { Add } from '@mui/icons-material';
import { generateDisputes } from '../services/api';
import { useSnackbar } from '../services/snackbar';

function SlaChip({ hours }) {
  if (hours < 24) return <Chip label="On Time" color="success" size="small" />;
  if (hours < 72) return <Chip label="At Risk" color="warning" size="small" />;
  return <Chip label="Overdue" color="error" size="small" />;
}

export default function Disputes() {
  const notify = useSnackbar();
  const disputes = useMemo(() => generateDisputes(), []);
  const [tab, setTab] = useState(0);
  const [createOpen, setCreateOpen] = useState(false);
  const [resolveOpen, setResolveOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [resolution, setResolution] = useState('');
  const [resNotes, setResNotes] = useState('');
  const [refundAmount, setRefundAmount] = useState('');
  const [notifyCustomer, setNotifyCustomer] = useState(true);

  const tabs = ['All', 'Open', 'Investigating', 'Resolved'];
  const filtered = tab === 0 ? disputes : disputes.filter((d) => d.status === tabs[tab]);
  const counts = tabs.map((t, i) => i === 0 ? disputes.length : disputes.filter((d) => d.status === t).length);

  const openResolve = (d) => { setSelected(d); setResolution(''); setResNotes(''); setRefundAmount(''); setResolveOpen(true); };
  const handleResolve = () => { notify(`Dispute ${selected.id} resolved`); setResolveOpen(false); };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Disputes</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={() => setCreateOpen(true)}>Create Dispute</Button>
      </Box>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        {tabs.map((t, i) => <Tab key={t} label={`${t} (${counts[i]})`} />)}
      </Tabs>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell><TableCell>Transaction</TableCell><TableCell>Account</TableCell>
              <TableCell>Type</TableCell><TableCell>Status</TableCell><TableCell>Resolution</TableCell>
              <TableCell align="right">Refund</TableCell><TableCell>Agent</TableCell>
              <TableCell>Filed</TableCell><TableCell>SLA</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((d) => (
              <TableRow key={d.id} hover>
                <TableCell>{d.id}</TableCell>
                <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{d.transactionId}</TableCell>
                <TableCell>{d.accountId}</TableCell>
                <TableCell>{d.type}</TableCell>
                <TableCell><Chip label={d.status} size="small" color={d.status === 'Resolved' ? 'success' : d.status === 'Open' ? 'warning' : 'info'} /></TableCell>
                <TableCell>{d.resolution || '-'}</TableCell>
                <TableCell align="right">{d.refundAmount > 0 ? d.refundAmount.toLocaleString() : '-'}</TableCell>
                <TableCell>{d.agent || '-'}</TableCell>
                <TableCell>{d.filed}</TableCell>
                <TableCell><SlaChip hours={d.slaHours} /></TableCell>
                <TableCell>
                  {d.status !== 'Resolved' && <Button size="small" onClick={() => openResolve(d)}>Resolve</Button>}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Create Dialog */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Create Dispute</DialogTitle>
        <DialogContent>
          <TextField fullWidth label="Transaction ID" margin="normal" required />
          <TextField fullWidth label="Account ID" margin="normal" required />
          <TextField select fullWidth label="Type" margin="normal" required>
            {['Unauthorized', 'Duplicate', 'MerchantDispute', 'ServiceNotRendered', 'WrongAmount'].map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Description" margin="normal" multiline rows={3} required />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={() => { notify('Dispute created'); setCreateOpen(false); }}>Create</Button>
        </DialogActions>
      </Dialog>

      {/* Resolve Dialog */}
      <Dialog open={resolveOpen} onClose={() => setResolveOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Resolve Dispute {selected?.id}</DialogTitle>
        <DialogContent>
          <TextField select fullWidth label="Resolution" margin="normal" value={resolution} onChange={(e) => setResolution(e.target.value)} required>
            {['Resolved', 'Rejected', 'Partially Resolved'].map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Resolution Notes" margin="normal" multiline rows={3} value={resNotes} onChange={(e) => setResNotes(e.target.value)} required />
          <TextField fullWidth label="Refund Amount" margin="normal" type="number" value={refundAmount} onChange={(e) => setRefundAmount(e.target.value)} />
          <FormControlLabel control={<Checkbox checked={notifyCustomer} onChange={(e) => setNotifyCustomer(e.target.checked)} />} label="Notify customer" />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResolveOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleResolve} disabled={!resolution || !resNotes}>Resolve</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
