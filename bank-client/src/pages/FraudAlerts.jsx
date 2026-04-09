import { useState, useEffect } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button, TextField, MenuItem, LinearProgress,
  Dialog, DialogTitle, DialogContent, DialogActions, Stepper, Step, StepLabel,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper, Tabs, Tab,
} from '@mui/material';
import { generateFraudAlerts, getFraudAlertActivities, addFraudAlertActivity } from '../services/api';
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
  const [detailTab, setDetailTab] = useState(0);
  const [activities, setActivities] = useState([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [newActionType, setNewActionType] = useState('Investigating');
  const [newNotes, setNewNotes] = useState('');
  const [savingActivity, setSavingActivity] = useState(false);

  const loadActivities = async (id) => {
    setActivitiesLoading(true);
    const data = await getFraudAlertActivities(id);
    setActivities(data);
    setActivitiesLoading(false);
  };

  const handleAddActivity = async () => {
    if (!selected || !newActionType.trim()) return;
    setSavingActivity(true);
    const ok = await addFraudAlertActivity(selected.id, {
      actionType: newActionType,
      notes: newNotes,
      agent: 'admin',
    });
    setSavingActivity(false);
    if (ok) {
      notify('Activity logged');
      setNewNotes('');
      loadActivities(selected.id);
    } else {
      notify('Failed to log activity');
    }
  };

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

  const openDetail = (alert) => {
    setSelected(alert);
    setDetailTab(0);
    setActivities([]);
    setNewActionType('Investigating');
    setNewNotes('');
    setDetailOpen(true);
    loadActivities(alert.id);
  };
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
        <DialogContent sx={{ px: 0, pb: 0 }}>
          <Tabs
            value={detailTab}
            onChange={(_, v) => setDetailTab(v)}
            sx={{ borderBottom: 1, borderColor: 'divider', px: 3 }}
          >
            <Tab label="Details" />
            <Tab label={`Activity Log (${activities.length})`} />
          </Tabs>

          {selected && (
            <Box sx={{ px: 3, pt: 2, pb: 1 }}>
              {/* Tab 0: Details */}
              {detailTab === 0 && (
                <Box>
                  <Table size="small">
                    <TableBody>
                      {[
                        ['Alert ID', selected.id],
                        ['Account', selected.accountId],
                        ['Transaction', selected.transactionId],
                        ['Alert Type', <Chip label={selected.type} size="small" />],
                        ['Severity', <Chip label={selected.severity} color={SEV_COLORS[selected.severity]} size="small" />],
                        ['Status', <Chip label={selected.status} color={STATUS_COLORS[selected.status]} size="small" />],
                        ['Created', selected.created],
                        ['Description', selected.description],
                      ].map(([label, value]) => (
                        <TableRow key={label}>
                          <TableCell sx={{ width: '30%', color: 'text.secondary', borderBottom: 'none', py: 1 }}>{label}</TableCell>
                          <TableCell sx={{ borderBottom: 'none', py: 1 }}>{value}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                  <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 2, mb: 1 }}>Investigation Timeline</Typography>
                  <Stepper activeStep={activeStep}>
                    {timelineSteps.map((label) => <Step key={label}><StepLabel>{label}</StepLabel></Step>)}
                  </Stepper>
                </Box>
              )}

              {/* Tab 1: Activity Log */}
              {detailTab === 1 && (
                <Box>
                  {/* Add new activity form */}
                  <Box sx={{ mb: 3, p: 2, border: 1, borderColor: 'divider', borderRadius: 1 }}>
                    <Typography variant="subtitle2" gutterBottom>Log New Activity</Typography>
                    <TextField
                      select
                      fullWidth
                      size="small"
                      margin="dense"
                      label="Action Type"
                      value={newActionType}
                      onChange={(e) => setNewActionType(e.target.value)}
                    >
                      {[
                        'Investigating',
                        'Called Customer',
                        'Reviewed Transaction',
                        'Contacted Merchant',
                        'Awaiting Response',
                        'Escalated',
                        'Blocked Account',
                        'Cleared',
                        'Note',
                      ].map((a) => <MenuItem key={a} value={a}>{a}</MenuItem>)}
                    </TextField>
                    <TextField
                      fullWidth
                      size="small"
                      margin="dense"
                      label="Notes"
                      multiline
                      rows={2}
                      value={newNotes}
                      onChange={(e) => setNewNotes(e.target.value)}
                    />
                    <Box sx={{ mt: 1, display: 'flex', justifyContent: 'flex-end' }}>
                      <Button
                        variant="contained"
                        size="small"
                        onClick={handleAddActivity}
                        disabled={savingActivity || !newActionType.trim()}
                      >
                        {savingActivity ? 'Saving…' : 'Log Activity'}
                      </Button>
                    </Box>
                  </Box>

                  {/* Timeline */}
                  <Typography variant="subtitle2" gutterBottom>Timeline</Typography>
                  {activitiesLoading && <Typography variant="body2" color="text.secondary">Loading…</Typography>}
                  {!activitiesLoading && activities.length === 0 && (
                    <Typography variant="body2" color="text.secondary">No activity logged yet.</Typography>
                  )}
                  {!activitiesLoading && activities.length > 0 && (
                    <Table size="small">
                      <TableHead>
                        <TableRow>
                          <TableCell>Timestamp</TableCell>
                          <TableCell>Action</TableCell>
                          <TableCell>Notes</TableCell>
                          <TableCell>Agent</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {activities.map((a, i) => (
                          <TableRow key={i} hover>
                            <TableCell>{new Date(a.timestamp).toLocaleString()}</TableCell>
                            <TableCell><Chip label={a.actionType} size="small" /></TableCell>
                            <TableCell>{a.notes || '—'}</TableCell>
                            <TableCell>{a.agent}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </Box>
              )}
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
