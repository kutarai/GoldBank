import { useState, useEffect } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button, TextField, MenuItem, LinearProgress,
  Dialog, DialogTitle, DialogContent, DialogActions, Stepper, Step, StepLabel,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { generateFraudAlerts } from '../services/api';
import { useSnackbar } from '../services/snackbar';

const SEV_COLORS = { High: 'error', Medium: 'warning', Low: 'info' };
const STATUS_COLORS = { New: 'warning', Reviewed: 'info', Escalated: 'error', Dismissed: 'default' };

export default function FraudAlerts() {
  const notify = useSnackbar();
  const [alerts, setAlerts] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    generateFraudAlerts().then(setAlerts).finally(() => setLoading(false));
  }, []);
  const [statusFilter, setStatusFilter] = useState('');
  const [sevFilter, setSevFilter] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [decision, setDecision] = useState('');
  const [investigationNotes, setInvestigationNotes] = useState('');

  const filtered = alerts.filter((a) => {
    if (statusFilter && a.status !== statusFilter) return false;
    if (sevFilter && a.severity !== sevFilter) return false;
    return true;
  });

  const stats = {
    highActive: alerts.filter((a) => a.severity === 'High' && a.status === 'New').length,
    investigating: alerts.filter((a) => a.status === 'Reviewed').length,
    resolved: alerts.filter((a) => a.status === 'Dismissed').length,
  };

  const openDetail = (alert) => { setSelected(alert); setDetailOpen(true); };
  const openReview = () => { setDecision(''); setInvestigationNotes(''); setDetailOpen(false); setReviewOpen(true); };
  const handleReview = () => { notify(`Alert ${selected.id}: ${decision}`); setReviewOpen(false); };

  const timelineSteps = ['Created', 'Under Investigation', 'Resolved'];
  const activeStep = selected?.status === 'New' ? 0 : selected?.status === 'Reviewed' || selected?.status === 'Escalated' ? 1 : 2;

  return (
    <Box>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      <Typography variant="h5" gutterBottom>Fraud Alerts</Typography>
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[['Active High Alerts', stats.highActive, 'error'], ['Under Investigation', stats.investigating, 'warning'], ['Resolved Today', stats.resolved, 'success']].map(([label, val, color]) => (
          <Grid size={{ xs: 12, sm: 4 }} key={label}>
            <Card><CardContent>
              <Typography variant="body2" color="text.secondary">{label}</Typography>
              <Typography variant="h4" color={`${color}.main`}>{val}</Typography>
            </CardContent></Card>
          </Grid>
        ))}
      </Grid>

      <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
        <TextField select size="small" label="Status" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} sx={{ minWidth: 140 }}>
          <MenuItem value="">All</MenuItem>
          {['New', 'Reviewed', 'Escalated', 'Dismissed'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>
        <TextField select size="small" label="Severity" value={sevFilter} onChange={(e) => setSevFilter(e.target.value)} sx={{ minWidth: 140 }}>
          <MenuItem value="">All</MenuItem>
          {['High', 'Medium', 'Low'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Alert ID</TableCell><TableCell>Account</TableCell><TableCell>Transaction</TableCell>
              <TableCell>Type</TableCell><TableCell>Severity</TableCell><TableCell>Status</TableCell>
              <TableCell>Created</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((a) => (
              <TableRow key={a.id} hover>
                <TableCell>{a.id}</TableCell>
                <TableCell>{a.accountId}</TableCell>
                <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{a.transactionId}</TableCell>
                <TableCell>{a.type}</TableCell>
                <TableCell><Chip label={a.severity} color={SEV_COLORS[a.severity]} size="small" /></TableCell>
                <TableCell><Chip label={a.status} color={STATUS_COLORS[a.status]} size="small" /></TableCell>
                <TableCell>{a.created}</TableCell>
                <TableCell><Button size="small" onClick={() => openDetail(a)}>Details</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Detail Dialog */}
      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Fraud Alert: {selected?.id}</DialogTitle>
        <DialogContent>
          {selected && (
            <Box sx={{ mt: 1 }}>
              <Grid container spacing={2} sx={{ mb: 2 }}>
                <Grid size={6}><Typography variant="body2" color="text.secondary">Account</Typography><Typography>{selected.accountId}</Typography></Grid>
                <Grid size={6}><Typography variant="body2" color="text.secondary">Transaction</Typography><Typography>{selected.transactionId}</Typography></Grid>
                <Grid size={6}><Typography variant="body2" color="text.secondary">Alert Type</Typography><Chip label={selected.type} size="small" /></Grid>
                <Grid size={6}><Typography variant="body2" color="text.secondary">Severity</Typography><Chip label={selected.severity} color={SEV_COLORS[selected.severity]} size="small" /></Grid>
              </Grid>
              <Typography variant="subtitle1" gutterBottom>Investigation Timeline</Typography>
              <Stepper activeStep={activeStep} sx={{ mb: 2 }}>
                {timelineSteps.map((label) => <Step key={label}><StepLabel>{label}</StepLabel></Step>)}
              </Stepper>
              <Typography variant="body2">{selected.description}</Typography>
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDetailOpen(false)}>Close</Button>
          <Button variant="contained" onClick={openReview}>Review / Resolve</Button>
        </DialogActions>
      </Dialog>

      {/* Review Dialog */}
      <Dialog open={reviewOpen} onClose={() => setReviewOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Review Alert {selected?.id}</DialogTitle>
        <DialogContent>
          <TextField select fullWidth label="Decision" margin="normal" value={decision} onChange={(e) => setDecision(e.target.value)} required>
            {['Approve', 'Dismiss', 'Escalate', 'Block Account', 'Create Investigation'].map((d) => <MenuItem key={d} value={d}>{d}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Investigation Notes" margin="normal" multiline rows={3} value={investigationNotes} onChange={(e) => setInvestigationNotes(e.target.value)} required />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReviewOpen(false)}>Cancel</Button>
          <Button variant="contained" color={decision === 'Block Account' ? 'error' : 'primary'} onClick={handleReview} disabled={!decision || !investigationNotes}>Submit</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
