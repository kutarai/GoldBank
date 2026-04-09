import { useState, useEffect } from 'react';
import {
  Box, Typography, Tabs, Tab, Chip, Button, TextField, MenuItem, LinearProgress,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, FormControlLabel, Checkbox,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { Add } from '@mui/icons-material';
import { generateDisputes, getDisputeActivities, addDisputeActivity } from '../services/api';
import { useSnackbar } from '../services/snackbar';

function SlaChip({ hours }) {
  if (hours < 24) return <Chip label="On Time" color="success" size="small" />;
  if (hours < 72) return <Chip label="At Risk" color="warning" size="small" />;
  return <Chip label="Overdue" color="error" size="small" />;
}

export default function Disputes() {
  const notify = useSnackbar();
  const [disputes, setDisputes] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    generateDisputes().then(setDisputes).finally(() => setLoading(false));
  }, []);
  const [tab, setTab] = useState(0);
  const [createOpen, setCreateOpen] = useState(false);
  const [resolveOpen, setResolveOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [resolution, setResolution] = useState('');
  const [resNotes, setResNotes] = useState('');
  const [refundAmount, setRefundAmount] = useState('');
  const [notifyCustomer, setNotifyCustomer] = useState(true);

  // Details dialog state
  const [detailsOpen, setDetailsOpen] = useState(false);
  const [detailsTab, setDetailsTab] = useState(0);
  const [activities, setActivities] = useState([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [newActionType, setNewActionType] = useState('Called Customer');
  const [newNotes, setNewNotes] = useState('');
  const [savingActivity, setSavingActivity] = useState(false);

  const loadActivities = async (id) => {
    setActivitiesLoading(true);
    const data = await getDisputeActivities(id);
    setActivities(data);
    setActivitiesLoading(false);
  };

  const openDetails = (d) => {
    setSelected(d);
    setDetailsTab(0);
    setActivities([]);
    setNewActionType('Called Customer');
    setNewNotes('');
    setDetailsOpen(true);
    loadActivities(d.id);
  };

  const handleAddActivity = async () => {
    if (!selected || !newActionType.trim()) return;
    setSavingActivity(true);
    const ok = await addDisputeActivity(selected.id, {
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

  const tabs = ['All', 'Open', 'Investigating', 'Resolved'];
  const filtered = tab === 0 ? disputes : disputes.filter((d) => d.status === tabs[tab]);
  const counts = tabs.map((t, i) => i === 0 ? disputes.length : disputes.filter((d) => d.status === t).length);

  const openResolve = (d) => { setSelected(d); setResolution(''); setResNotes(''); setRefundAmount(''); setResolveOpen(true); };
  const handleResolve = () => { notify(`Dispute ${selected.id} resolved`); setResolveOpen(false); };

  return (
    <Box>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
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
                  <Button size="small" variant="outlined" sx={{ ml: 1 }} onClick={() => openDetails(d)}>Details</Button>
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

      {/* Details Dialog */}
      <Dialog open={detailsOpen} onClose={() => setDetailsOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Dispute {selected?.id}</DialogTitle>
        <DialogContent sx={{ px: 0, pb: 0 }}>
          <Tabs
            value={detailsTab}
            onChange={(_, v) => setDetailsTab(v)}
            sx={{ borderBottom: 1, borderColor: 'divider', px: 3 }}
          >
            <Tab label="Details" />
            <Tab label={`Activity Log (${activities.length})`} />
          </Tabs>

          {selected && (
            <Box sx={{ px: 3, pt: 2, pb: 1 }}>
              {/* Tab 0: Details */}
              {detailsTab === 0 && (
                <Table size="small">
                  <TableBody>
                    {[
                      ['Dispute ID', selected.id],
                      ['Transaction', selected.transactionId],
                      ['Account', selected.accountId],
                      ['Type', selected.type],
                      ['Status', <Chip label={selected.status} size="small" color={selected.status === 'Resolved' ? 'success' : selected.status === 'Open' ? 'warning' : 'info'} />],
                      ['Description', selected.description || '—'],
                      ['Resolution', selected.resolution || '—'],
                      ['Refund Amount', selected.refundAmount > 0 ? selected.refundAmount.toLocaleString() : '—'],
                      ['Agent', selected.agent || '—'],
                      ['Filed', selected.filed],
                      ['SLA', <SlaChip hours={selected.slaHours} />],
                      ['Resolved At', selected.resolved || '—'],
                    ].map(([label, value]) => (
                      <TableRow key={label}>
                        <TableCell sx={{ width: '30%', color: 'text.secondary', borderBottom: 'none', py: 1 }}>{label}</TableCell>
                        <TableCell sx={{ borderBottom: 'none', py: 1 }}>{value}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}

              {/* Tab 1: Activity Log */}
              {detailsTab === 1 && (
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
                        'Called Customer',
                        'Called Merchant',
                        'Investigating',
                        'Awaiting Response',
                        'Reviewed Evidence',
                        'Escalated',
                        'Email Sent',
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

                  {/* Activity timeline */}
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
          <Button onClick={() => setDetailsOpen(false)}>Close</Button>
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
