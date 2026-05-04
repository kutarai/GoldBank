import { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import {
  Box, Card, CardContent, Grid, Typography, TextField, Button, Alert, MenuItem, FormControlLabel, Checkbox,
  CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import DenominationGrid, { toBreakdown, getDenominationsFor } from '../components/DenominationGrid';
import { getCustomerCard, createWithdrawal, approveWithdrawal, openReceipt } from '../services/api';
import PrintIcon from '@mui/icons-material/Print';
import { useTellerSession } from '../auth/TellerSessionContext';
import { useSecurityState } from '../components/SecurityShell';

export default function Withdrawal() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { drawer, refreshDrawer } = useTellerSession();
  const { online } = useSecurityState();

  const accountIdParam = searchParams.get('account');
  const [card, setCard] = useState(null);
  const [selectedAccountId, setSelectedAccountId] = useState('');
  const [currency, setCurrency] = useState('');
  const [amount, setAmount] = useState('');
  const [denominations, setDenominations] = useState({});
  const [identityVerified, setIdentityVerified] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [success, setSuccess] = useState(null);
  const [error, setError] = useState(null);

  // Approval modal state
  const [pendingId, setPendingId] = useState(null);
  const [supUser, setSupUser] = useState('');
  const [supPin, setSupPin] = useState('');
  const [approvalError, setApprovalError] = useState(null);
  const [approving, setApproving] = useState(false);

  useEffect(() => {
    if (accountIdParam) {
      getCustomerCard(accountIdParam).then(c => {
        setCard(c);
        const first = c.balances?.[0];
        if (first) {
          setSelectedAccountId(first.accountIdRaw);
          setCurrency(first.currency);
        }
      }).catch(() => setError('Customer not found'));
    }
  }, [accountIdParam]);

  const grandTotal = getDenominationsFor(currency).reduce(
    (sum, d) => sum + d.face * (Number(denominations[d.face]) || 0), 0);

  const selectedBalance = card?.balances?.find(b => b.currency === currency)?.balance ?? 0;

  const canSubmit =
    identityVerified &&
    selectedAccountId &&
    currency &&
    Number(amount) > 0 &&
    Number(amount) <= selectedBalance &&
    Math.abs(grandTotal - Number(amount)) < 0.001 &&
    drawer &&
    online;

  const handleSubmit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const res = await createWithdrawal({
        accountId: selectedAccountId,
        currency,
        amount: Number(amount),
        denominations: toBreakdown(currency, denominations),
        identityVerified: true,
      });
      if (res.requiresApproval) {
        setPendingId(res.pendingTransactionId);
      } else {
        setSuccess(res);
        await refreshDrawer();
      }
    } catch (err) {
      setError(err.body?.message || err.body?.error || err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleApprove = async () => {
    setApproving(true);
    setApprovalError(null);
    try {
      const res = await approveWithdrawal(pendingId, supUser, supPin);
      setPendingId(null);
      setSuccess(res);
      await refreshDrawer();
    } catch (err) {
      setApprovalError('Approval failed.');
    } finally {
      setApproving(false);
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
      <Typography variant="h5" sx={{ mb: 3 }}>New Withdrawal</Typography>

      {/* Mini customer card at top */}
      {card && (
        <Card sx={{ mb: 2 }}>
          <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            {card.selfieImageUrl && (
              <Box component="img" src={card.selfieImageUrl} alt="Photo"
                sx={{ width: 80, height: 80, objectFit: 'cover', borderRadius: 1, border: '1px solid', borderColor: 'divider' }} />
            )}
            <Box sx={{ flexGrow: 1 }}>
              <Typography variant="h6">{card.fullName}</Typography>
              <Typography variant="body2" color="text.secondary">{card.accountId} · {card.phone}</Typography>
              <Typography variant="caption">Available: {currency} {Number(selectedBalance).toLocaleString()}</Typography>
            </Box>
            {card.signatureImageUrl && (
              <Box component="img" src={card.signatureImageUrl} alt="Signature"
                sx={{ width: 120, height: 60, objectFit: 'contain', borderRadius: 1, border: '1px solid', borderColor: 'divider' }} />
            )}
          </CardContent>
        </Card>
      )}

      <Alert severity="warning" icon={false} sx={{ mb: 2 }}>
        <FormControlLabel
          control={
            <Checkbox
              checked={identityVerified}
              onChange={(e) => setIdentityVerified(e.target.checked)}
              color="warning"
            />
          }
          label={<strong>I have verified the customer's identity by photo, signature, and physical ID document</strong>}
        />
      </Alert>

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 2 }}>Transaction Details</Typography>

              <TextField
                select fullWidth margin="normal" label="Currency"
                value={currency}
                disabled={!identityVerified}
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
                fullWidth margin="normal" label="Amount" type="number"
                value={amount}
                disabled={!identityVerified}
                onChange={(e) => setAmount(e.target.value)}
                helperText={`Available: ${Number(selectedBalance).toLocaleString()}`}
              />

              {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}

              <Button
                fullWidth variant="contained" color="warning"
                sx={{ mt: 3, py: 1.2 }}
                disabled={!canSubmit || submitting}
                onClick={handleSubmit}
              >
                {submitting ? <CircularProgress size={22} /> : 'Confirm Withdrawal'}
              </Button>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 7 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 2 }}>Denominations</Typography>
              {currency && identityVerified ? (
                <DenominationGrid
                  currency={currency}
                  value={denominations}
                  onChange={setDenominations}
                  targetAmount={amount}
                />
              ) : (
                <Typography color="text.secondary">
                  {identityVerified ? 'Select a currency.' : 'Tick the identity verification box to unlock.'}
                </Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Supervisor approval modal */}
      <Dialog open={!!pendingId} onClose={() => setPendingId(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Supervisor Approval Required</DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            This withdrawal exceeds the high-value threshold. A supervisor must enter their PIN to proceed.
          </Alert>
          <TextField
            fullWidth margin="normal" label="Supervisor Username"
            value={supUser} onChange={(e) => setSupUser(e.target.value)}
          />
          <TextField
            fullWidth margin="normal" label="Supervisor PIN" type="password"
            value={supPin} onChange={(e) => setSupPin(e.target.value)}
          />
          {approvalError && <Alert severity="error" sx={{ mt: 1 }}>{approvalError}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingId(null)}>Cancel</Button>
          <Button
            variant="contained"
            disabled={approving || !supUser || !supPin}
            onClick={handleApprove}
          >
            {approving ? <CircularProgress size={20} /> : 'Approve'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={!!success} disableEscapeKeyDown>
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CheckCircleIcon color="success" />
          Transaction Successfully Completed
        </DialogTitle>
        <DialogContent>
          <Typography>
            Withdrawal recorded.
          </Typography>
          {(success?.reference || success?.transactionId) && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              Reference: <strong>{success.reference || success.transactionId}</strong>
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
