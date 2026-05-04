import { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import {
  Box, Card, CardContent, Grid, Typography, TextField, Button, Alert, MenuItem, CircularProgress,
  Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import DenominationGrid, { toBreakdown, getDenominationsFor } from '../components/DenominationGrid';
import { getCustomerCard, createDeposit, openReceipt } from '../services/api';
import PrintIcon from '@mui/icons-material/Print';
import { useTellerSession } from '../auth/TellerSessionContext';
import { useSecurityState } from '../components/SecurityShell';

export default function Deposit() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { drawer, refreshDrawer } = useTellerSession();
  const { online } = useSecurityState();

  const accountIdParam = searchParams.get('account');
  const [card, setCard] = useState(null);
  const [selectedAccountId, setSelectedAccountId] = useState('');
  const [currency, setCurrency] = useState('');
  const [amount, setAmount] = useState('');
  const [depositorName, setDepositorName] = useState('');
  const [denominations, setDenominations] = useState({});
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (accountIdParam) {
      getCustomerCard(accountIdParam).then(c => {
        setCard(c);
        const first = c.balances?.[0];
        if (first) {
          setSelectedAccountId(first.accountIdRaw);
          setCurrency(first.currency);
          setDepositorName(c.fullName || '');
        }
      }).catch(() => setError('Customer not found'));
    }
  }, [accountIdParam]);

  const grandTotal = getDenominationsFor(currency).reduce(
    (sum, d) => sum + d.face * (Number(denominations[d.face]) || 0), 0);

  const canSubmit =
    selectedAccountId &&
    currency &&
    Number(amount) > 0 &&
    depositorName.trim().length > 0 &&
    Math.abs(grandTotal - Number(amount)) < 0.001 &&
    drawer &&
    online;

  const handleSubmit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await createDeposit({
        accountId: selectedAccountId,
        currency,
        amount: Number(amount),
        depositorName,
        denominations: toBreakdown(currency, denominations),
      });
      setSuccess({ ...res });
      await refreshDrawer();
    } catch (err) {
      setError(err.body?.message || err.body?.error || err.message);
    } finally {
      setSubmitting(false);
    }
  };

  if (!drawer) {
    return (
      <Alert severity="warning">
        You must open your drawer before processing transactions.{' '}
        <Button size="small" onClick={() => navigate('/drawer')}>Open Drawer</Button>
      </Alert>
    );
  }

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3 }}>New Deposit</Typography>

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 2 }}>Transaction Details</Typography>

              {card && (
                <Box sx={{ mb: 2, p: 2, bgcolor: 'background.default', borderRadius: 1 }}>
                  <Typography variant="caption" color="text.secondary">Customer</Typography>
                  <Typography>{card.fullName}</Typography>
                  <Typography variant="caption" color="text.secondary">Account</Typography>
                  <Typography>{card.accountId}</Typography>
                </Box>
              )}

              <TextField
                select fullWidth margin="normal" label="Currency"
                value={currency}
                onChange={(e) => {
                  setCurrency(e.target.value);
                  const acc = card?.balances?.find(b => b.currency === e.target.value);
                  if (acc) setSelectedAccountId(acc.accountIdRaw);
                  setDenominations({});
                }}
              >
                {(card?.balances || []).map(b => (
                  <MenuItem key={b.currency} value={b.currency}>{b.currency}</MenuItem>
                ))}
              </TextField>

              <TextField
                fullWidth margin="normal" label="Depositor Name" required
                value={depositorName}
                onChange={(e) => setDepositorName(e.target.value)}
                helperText="May differ from account holder"
              />

              <TextField
                fullWidth margin="normal" label="Amount" type="number" required
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />

              {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}

              <Button
                fullWidth variant="contained" color="success"
                sx={{ mt: 3, py: 1.2 }}
                disabled={!canSubmit || submitting}
                onClick={handleSubmit}
              >
                {submitting ? <CircularProgress size={22} /> : 'Confirm Deposit'}
              </Button>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 7 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 2 }}>Denominations</Typography>
              {currency ? (
                <DenominationGrid
                  currency={currency}
                  value={denominations}
                  onChange={setDenominations}
                  targetAmount={amount}
                />
              ) : (
                <Typography color="text.secondary">Select a currency to enter denominations.</Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Dialog open={!!success} disableEscapeKeyDown>
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CheckCircleIcon color="success" />
          Transaction Successfully Completed
        </DialogTitle>
        <DialogContent>
          <Typography>
            Deposit recorded.
          </Typography>
          {success?.reference && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              Reference: <strong>{success.reference}</strong>
            </Typography>
          )}
        </DialogContent>
        <DialogActions>
          {success?.cashTransactionId && (
            <Button
              startIcon={<PrintIcon />}
              onClick={() => openReceipt(success.cashTransactionId)}
            >
              Print Receipt
            </Button>
          )}
          <Button
            variant="contained"
            onClick={() => { setSuccess(null); navigate('/customers'); }}
            autoFocus
          >
            OK
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
