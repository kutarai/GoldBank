import { useEffect, useState } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Button, Chip, IconButton, Tooltip,
  Table, TableBody, TableCell, TableHead, TableRow,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField, Alert, CircularProgress,
} from '@mui/material';
import PrintIcon from '@mui/icons-material/Print';
import UndoIcon from '@mui/icons-material/Undo';
import { useNavigate } from 'react-router-dom';
import { useTellerSession } from '../auth/TellerSessionContext';
import { listTransactions, openReceipt, reverseTransaction } from '../services/api';

export default function Dashboard() {
  const { user, drawer, refreshDrawer } = useTellerSession();
  const navigate = useNavigate();
  const [todayTxns, setTodayTxns] = useState([]);

  // Reversal modal state
  const [reversing, setReversing] = useState(null);
  const [reason, setReason] = useState('');
  const [supUser, setSupUser] = useState('');
  const [supPin, setSupPin] = useState('');
  const [reverseErr, setReverseErr] = useState(null);
  const [reverseLoading, setReverseLoading] = useState(false);

  const refreshTxns = () => {
    if (drawer) listTransactions().then(setTodayTxns).catch(() => setTodayTxns([]));
  };

  useEffect(() => {
    refreshTxns();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [drawer]);

  const handleReverse = async () => {
    if (!reversing) return;
    setReverseLoading(true);
    setReverseErr(null);
    try {
      await reverseTransaction(reversing.id, {
        reason,
        supervisorUsername: supUser,
        supervisorPin:      supPin,
      });
      setReversing(null);
      setReason('');
      setSupUser('');
      setSupPin('');
      refreshTxns();
      await refreshDrawer();
    } catch {
      setReverseErr('Reversal failed. Check supervisor credentials and that the original transaction is still in this shift.');
    } finally {
      setReverseLoading(false);
    }
  };

  const totalDeposits = todayTxns.filter(t => t.direction === 'Deposit' && t.status === 'completed').reduce((s, t) => s + Number(t.amount), 0);
  const totalWithdrawals = todayTxns.filter(t => t.direction === 'Withdrawal' && t.status === 'completed').reduce((s, t) => s + Number(t.amount), 0);

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3 }}>Welcome, {user?.fullName ?? user?.username}</Typography>

      {!drawer ? (
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>No drawer open</Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              You must open your cash drawer before processing transactions.
            </Typography>
            <Button variant="contained" onClick={() => navigate('/drawer')}>Open Drawer</Button>
          </CardContent>
        </Card>
      ) : (
        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Card>
              <CardContent>
                <Typography variant="body2" color="text.secondary">Today's Deposits</Typography>
                <Typography variant="h4" color="success.main">{totalDeposits.toLocaleString()}</Typography>
                <Typography variant="caption">{todayTxns.filter(t => t.direction === 'Deposit').length} transactions</Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Card>
              <CardContent>
                <Typography variant="body2" color="text.secondary">Today's Withdrawals</Typography>
                <Typography variant="h4" color="warning.main">{totalWithdrawals.toLocaleString()}</Typography>
                <Typography variant="caption">{todayTxns.filter(t => t.direction === 'Withdrawal').length} transactions</Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid size={{ xs: 12, sm: 4 }}>
            <Card>
              <CardContent>
                <Typography variant="body2" color="text.secondary">Net</Typography>
                <Typography variant="h4">{(totalDeposits - totalWithdrawals).toLocaleString()}</Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      )}

      <Box sx={{ display: 'flex', gap: 2, mb: 3 }}>
        <Button variant="contained" color="primary" onClick={() => navigate('/customers')}>Find Customer</Button>
        <Button variant="outlined" onClick={() => navigate('/deposit')} disabled={!drawer}>New Deposit</Button>
        <Button variant="outlined" onClick={() => navigate('/customers')} disabled={!drawer}>New Withdrawal</Button>
      </Box>

      {/* STORY-161: Reversal modal */}
      <Dialog open={!!reversing} onClose={() => setReversing(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Reverse Transaction</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            You are about to reverse a <strong>{reversing?.direction}</strong> of{' '}
            <strong>{reversing?.currency} {Number(reversing?.amount || 0).toLocaleString()}</strong>.
            This will create a compensating transaction and requires supervisor approval.
          </Alert>
          <TextField
            fullWidth required margin="normal"
            label="Reason"
            multiline rows={2}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Why is this transaction being reversed?"
          />
          <TextField
            fullWidth required margin="normal"
            label="Supervisor Username"
            value={supUser}
            onChange={(e) => setSupUser(e.target.value)}
          />
          <TextField
            fullWidth required margin="normal"
            label="Supervisor PIN" type="password"
            value={supPin}
            onChange={(e) => setSupPin(e.target.value)}
          />
          {reverseErr && <Alert severity="error" sx={{ mt: 1 }}>{reverseErr}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReversing(null)}>Cancel</Button>
          <Button
            variant="contained"
            color="warning"
            disabled={reverseLoading || !reason || !supUser || !supPin}
            onClick={handleReverse}
          >
            {reverseLoading ? <CircularProgress size={20} /> : 'Reverse'}
          </Button>
        </DialogActions>
      </Dialog>

      {todayTxns.length > 0 && (
        <Card>
          <CardContent>
            <Typography variant="h6" gutterBottom>Today's Transactions</Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Time</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell>Currency</TableCell>
                  <TableCell align="right">Amount</TableCell>
                  <TableCell>Depositor</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell align="center">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {todayTxns.map(t => {
                  const isReversed  = t.status === 'reversed' || !!t.reversedAt;
                  const isReversal  = t.direction === 'Reversal';
                  const canReverse  = !isReversed && !isReversal && t.status === 'completed';
                  const rowSx = isReversed
                    ? { '& td': { textDecoration: 'line-through', color: 'text.disabled' } }
                    : {};
                  return (
                    <TableRow key={t.id} hover sx={rowSx}>
                      <TableCell>{new Date(t.createdAt).toLocaleTimeString()}</TableCell>
                      <TableCell>{t.direction}</TableCell>
                      <TableCell>{t.currency}</TableCell>
                      <TableCell align="right">{Number(t.amount).toLocaleString()}</TableCell>
                      <TableCell>{t.depositorName || '—'}</TableCell>
                      <TableCell>
                        <Chip size="small" label={t.status}
                          color={
                            t.status === 'completed' ? 'success'
                            : t.status === 'reversed' ? 'default'
                            : 'warning'
                          } />
                      </TableCell>
                      <TableCell align="center">
                        <Tooltip title="Reprint receipt">
                          <IconButton size="small" onClick={() => openReceipt(t.id)}>
                            <PrintIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                        {canReverse && (
                          <Tooltip title="Reverse transaction">
                            <IconButton
                              size="small"
                              color="warning"
                              onClick={() => { setReversing(t); setReverseErr(null); setReason(''); setSupUser(''); setSupPin(''); }}
                            >
                              <UndoIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
