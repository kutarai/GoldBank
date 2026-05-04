import { useEffect, useState, useCallback } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Button, Chip, IconButton, Tooltip,
  Table, TableBody, TableCell, TableHead, TableRow, CircularProgress, Alert,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField,
} from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import WarningIcon from '@mui/icons-material/Warning';
import { useTellerSession } from '../auth/TellerSessionContext';
import { getBranchDashboard, approveWithdrawal } from '../services/api';

const REFRESH_MS = 30_000;

function fmtTime(iso) {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

export default function SupervisorDashboard() {
  const { user } = useTellerSession();
  const branchId = user?.branchId;

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [lastRefresh, setLastRefresh] = useState(null);

  // Approval modal state
  const [approving, setApproving] = useState(null); // pending row being approved
  const [supUser, setSupUser] = useState(user?.username || '');
  const [supPin, setSupPin] = useState('');
  const [approvalErr, setApprovalErr] = useState(null);
  const [approvalLoading, setApprovalLoading] = useState(false);

  const refresh = useCallback(async () => {
    if (!branchId) return;
    setError(null);
    try {
      const d = await getBranchDashboard(branchId);
      setData(d);
      setLastRefresh(new Date());
    } catch (e) {
      setError(e.message || 'Failed to load dashboard');
    } finally {
      setLoading(false);
    }
  }, [branchId]);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, REFRESH_MS);
    return () => clearInterval(id);
  }, [refresh]);

  const handleApprove = async () => {
    if (!approving) return;
    setApprovalLoading(true);
    setApprovalErr(null);
    try {
      await approveWithdrawal(approving.cashTransactionId, supUser, supPin);
      setApproving(null);
      setSupPin('');
      await refresh();
    } catch {
      setApprovalErr('Approval failed.');
    } finally {
      setApprovalLoading(false);
    }
  };

  if (!branchId) {
    return (
      <Alert severity="error">
        Your user has no branch assigned. Ask an administrator to set your branch.
      </Alert>
    );
  }

  if (loading) return <CircularProgress />;
  if (error) return <Alert severity="error">{error}</Alert>;
  if (!data) return null;

  const currencies = Object.keys(data.todayVolume || {});

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>Branch Supervisor Dashboard</Typography>
        <Typography variant="caption" color="text.secondary" sx={{ mr: 2 }}>
          {lastRefresh ? `Updated ${fmtTime(lastRefresh)}` : ''}
        </Typography>
        <Tooltip title="Refresh now">
          <IconButton onClick={refresh}><RefreshIcon /></IconButton>
        </Tooltip>
      </Box>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        {/* Active tellers */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card sx={{ height: '100%' }}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Active Tellers ({data.activeTellers?.length || 0})
              </Typography>
              {(data.activeTellers || []).length === 0 ? (
                <Typography color="text.secondary">No tellers currently have an open drawer.</Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Teller</TableCell>
                      <TableCell>Opened</TableCell>
                      <TableCell>Drawer</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.activeTellers.map(t => (
                      <TableRow key={t.drawerId}>
                        <TableCell>{t.tellerName}</TableCell>
                        <TableCell>{fmtTime(t.openedAt)}</TableCell>
                        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.7rem' }}>
                          {String(t.drawerId).slice(0, 8)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Pending approvals */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card sx={{ height: '100%', borderColor: (data.pendingApprovals?.length || 0) > 0 ? 'warning.main' : 'divider', borderWidth: (data.pendingApprovals?.length || 0) > 0 ? 2 : 1, borderStyle: 'solid' }}>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Pending Approvals ({data.pendingApprovals?.length || 0})
              </Typography>
              {(data.pendingApprovals || []).length === 0 ? (
                <Typography color="text.secondary">No high-value withdrawals awaiting approval.</Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Account</TableCell>
                      <TableCell align="right">Amount</TableCell>
                      <TableCell>Age</TableCell>
                      <TableCell />
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.pendingApprovals.map(p => (
                      <TableRow key={p.cashTransactionId} hover>
                        <TableCell>{p.shortAccount}</TableCell>
                        <TableCell align="right">
                          <strong>{p.currency} {Number(p.amount).toLocaleString()}</strong>
                        </TableCell>
                        <TableCell>
                          <Chip size="small" label={`${p.ageMinutes}m`}
                            color={p.ageMinutes > 10 ? 'warning' : 'default'} />
                        </TableCell>
                        <TableCell>
                          <Button size="small" variant="contained"
                            onClick={() => { setApproving(p); setApprovalErr(null); setSupPin(''); }}>
                            Approve
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Today's volume */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>Today's Volume</Typography>
              {currencies.length === 0 ? (
                <Typography color="text.secondary">No transactions yet today.</Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Currency</TableCell>
                      <TableCell align="right">Deposits +</TableCell>
                      <TableCell align="right">Withdrawals −</TableCell>
                      <TableCell align="right">Net</TableCell>
                      <TableCell align="right">Txns</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {currencies.map(cur => {
                      const v = data.todayVolume[cur];
                      return (
                        <TableRow key={cur}>
                          <TableCell><strong>{cur}</strong></TableCell>
                          <TableCell align="right" sx={{ color: 'success.main' }}>
                            {Number(v.deposits).toLocaleString()}
                          </TableCell>
                          <TableCell align="right" sx={{ color: 'warning.main' }}>
                            {Number(v.withdrawals).toLocaleString()}
                          </TableCell>
                          <TableCell align="right">
                            <strong>{Number(v.net).toLocaleString()}</strong>
                          </TableCell>
                          <TableCell align="right">{v.txns}</TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Variance alerts */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Card sx={{
            borderColor: (data.varianceAlerts?.length || 0) > 0 ? 'error.main' : 'divider',
            borderWidth: (data.varianceAlerts?.length || 0) > 0 ? 2 : 1,
            borderStyle: 'solid',
          }}>
            <CardContent>
              <Typography variant="h6" gutterBottom sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                {(data.varianceAlerts?.length || 0) > 0 && <WarningIcon color="error" />}
                Variance Alerts ({data.varianceAlerts?.length || 0})
              </Typography>
              {(data.varianceAlerts || []).length === 0 ? (
                <Typography color="text.secondary">No drawers closed today with variance.</Typography>
              ) : (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Teller</TableCell>
                      <TableCell>Closed</TableCell>
                      <TableCell>Variance</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.varianceAlerts.map(v => (
                      <TableRow key={v.drawerId}>
                        <TableCell>{v.tellerName}</TableCell>
                        <TableCell>{fmtTime(v.closedAt)}</TableCell>
                        <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.7rem' }}>
                          {v.variance}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Approval modal */}
      <Dialog open={!!approving} onClose={() => setApproving(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Approve High-Value Withdrawal</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            You are approving a withdrawal of{' '}
            <strong>{approving?.currency} {Number(approving?.amount || 0).toLocaleString()}</strong>{' '}
            from account <strong>{approving?.shortAccount}</strong>. The teller cannot complete this
            transaction without your PIN. Make sure you have verified the customer in person.
          </Alert>
          <TextField fullWidth margin="normal" label="Your Username"
            value={supUser} onChange={(e) => setSupUser(e.target.value)} />
          <TextField fullWidth margin="normal" label="Your PIN" type="password"
            value={supPin} onChange={(e) => setSupPin(e.target.value)} autoFocus />
          {approvalErr && <Alert severity="error" sx={{ mt: 1 }}>{approvalErr}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setApproving(null)}>Cancel</Button>
          <Button variant="contained" disabled={approvalLoading || !supUser || !supPin}
            onClick={handleApprove}>
            {approvalLoading ? <CircularProgress size={20} /> : 'Approve'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
