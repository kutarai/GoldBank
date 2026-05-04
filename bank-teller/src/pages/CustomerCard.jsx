import { useEffect, useState, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Box, Card, CardContent, Grid, Typography, Chip, Button, Tabs, Tab, TextField,
  Table, TableBody, TableCell, TableHead, TableRow, CircularProgress, Tooltip,
  Dialog, DialogTitle, DialogContent, DialogActions, MenuItem, Alert,
} from '@mui/material';
import {
  getCustomerCard, getCustomerTransactions,
  listCustomerAssets, registerCustomerAsset, withdrawAsset, listDepositHouses,
} from '../services/api';

// Format YYYY-MM-DD
const fmtDate = (d) => d.toISOString().slice(0, 10);
const today30 = () => {
  const t = new Date();
  const f = new Date(t);
  f.setDate(f.getDate() - 30);
  return { from: fmtDate(f), to: fmtDate(t) };
};

export default function CustomerCard() {
  const { accountId } = useParams();
  const navigate = useNavigate();
  const [card, setCard] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);

  // Right-column tab state
  const [tab, setTab] = useState(0);

  // Transactions state
  const initial = today30();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [txns, setTxns] = useState([]);
  const [txnLoading, setTxnLoading] = useState(false);

  const fetchTxns = useCallback(async () => {
    setTxnLoading(true);
    try {
      const data = await getCustomerTransactions(accountId, from, to);
      setTxns(data?.items || []);
    } catch {
      setTxns([]);
    } finally {
      setTxnLoading(false);
    }
  }, [accountId, from, to]);

  // Assets tab state
  const [assets, setAssets] = useState([]);
  const [assetsLoading, setAssetsLoading] = useState(false);
  const [assetsError, setAssetsError] = useState(null);
  const [depositOpen, setDepositOpen] = useState(false);
  const [withdrawTarget, setWithdrawTarget] = useState(null); // asset row

  const fetchAssets = useCallback(async () => {
    setAssetsLoading(true);
    setAssetsError(null);
    try {
      const items = await listCustomerAssets(accountId);
      setAssets(items || []);
    } catch (e) {
      setAssetsError(e.message || 'Failed to load assets');
    } finally {
      setAssetsLoading(false);
    }
  }, [accountId]);

  useEffect(() => {
    setLoading(true);
    getCustomerCard(accountId)
      .then(setCard)
      .catch(() => setError('Customer not found'))
      .finally(() => setLoading(false));
  }, [accountId]);

  // Lazy-load transactions / assets when their tab is opened
  useEffect(() => {
    if (tab === 1 && txns.length === 0) fetchTxns();
    if (tab === 2 && assets.length === 0 && !assetsLoading) fetchAssets();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab]);

  if (loading) return <CircularProgress />;
  if (error || !card) return <Typography color="error">{error || 'Customer not found'}</Typography>;

  const cannotWithdraw = card.status !== 'active' || card.kycLevel < 1;

  return (
    <Box>
      <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
        <Button
          variant="contained"
          color="success"
          onClick={() => navigate(`/deposit?account=${card.accountIdRaw}`)}
        >
          Process Deposit
        </Button>
        <Tooltip title={cannotWithdraw ? 'Account is not active or KYC incomplete' : ''}>
          <span>
            <Button
              variant="contained"
              color="warning"
              disabled={cannotWithdraw}
              onClick={() => navigate(`/withdrawal?account=${card.accountIdRaw}`)}
            >
              Process Withdrawal
            </Button>
          </span>
        </Tooltip>
      </Box>
      <Typography variant="h5" sx={{ mb: 2 }}>Customer Card</Typography>

      <Grid container spacing={3} alignItems="flex-start" sx={{ flexWrap: { sm: 'nowrap' } }}>
        {/* Left: photo + ID + signature */}
        <Grid size={{ xs: 12, sm: 5 }} sx={{ minWidth: 0, flexShrink: 0, width: { sm: 360 } }}>
          <Card>
            <CardContent>
              <Typography variant="subtitle2" color="text.secondary" gutterBottom>Photo</Typography>
              {card.selfieImageUrl ? (
                <Box component="img" src={card.selfieImageUrl} alt="Selfie"
                  sx={{ width: 160, height: 200, objectFit: 'cover', borderRadius: 1, border: '1px solid', borderColor: 'divider', mb: 2, display: 'block' }} />
              ) : (
                <Box sx={{ height: 200, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default', mb: 2, borderRadius: 1 }}>
                  <Typography color="text.secondary">No photo on file</Typography>
                </Box>
              )}

              <Typography variant="subtitle2" color="text.secondary" gutterBottom>ID Document</Typography>
              {card.idImageUrl ? (
                <Box component="img" src={card.idImageUrl} alt="ID Document"
                  sx={{ width: '100%', maxHeight: 200, objectFit: 'contain', borderRadius: 1, border: '1px solid', borderColor: 'divider', mb: 2 }} />
              ) : (
                <Box sx={{ height: 100, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default', mb: 2, borderRadius: 1 }}>
                  <Typography color="text.secondary">No ID on file</Typography>
                </Box>
              )}

              <Typography variant="subtitle2" color="text.secondary" gutterBottom>Signature</Typography>
              {card.signatureImageUrl ? (
                <>
                  <Box component="img" src={card.signatureImageUrl} alt="Signature"
                    sx={{ width: '100%', maxHeight: 100, objectFit: 'contain', borderRadius: 1, border: '1px solid', borderColor: 'divider' }} />
                  {card.signatureVerifiedBy && (
                    <Typography variant="caption" color="success.main">
                      Verified by {card.signatureVerifiedBy} on {new Date(card.signatureVerifiedAt).toLocaleDateString()}
                    </Typography>
                  )}
                </>
              ) : (
                <Box sx={{ height: 60, display: 'flex', alignItems: 'center', justifyContent: 'center', bgcolor: 'background.default', borderRadius: 1 }}>
                  <Typography color="text.secondary">No signature on file</Typography>
                </Box>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Right: tabbed (Profile / Transactions) */}
        <Grid size={{ xs: 12, sm: 7 }} sx={{ minWidth: 0, flexGrow: 1 }}>
          <Card sx={{ mb: 2 }}>
            <Tabs
              value={tab}
              onChange={(_, v) => setTab(v)}
              variant="scrollable"
              scrollButtons="auto"
              sx={{ borderBottom: 1, borderColor: 'divider', px: 2, minHeight: 48 }}
            >
              <Tab label="Profile" />
              <Tab label="Transactions" />
              <Tab label="Assets" />
            </Tabs>

            {tab === 0 && (
              <CardContent>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                  <Typography variant="h6">{card.fullName || 'Unknown'}</Typography>
                  <Box sx={{ display: 'flex', gap: 1 }}>
                    <Chip label={card.status} size="small" color={card.status === 'active' ? 'success' : 'warning'} />
                    {card.flags?.frozen && <Chip label="Frozen" size="small" color="info" />}
                    {card.flags?.suspended && <Chip label="Suspended" size="small" color="warning" />}
                    {card.flags?.signatureVerified && <Chip label="Signature Verified" size="small" color="success" />}
                  </Box>
                </Box>

                <Table size="small">
                  <TableBody>
                    {[
                      ['Account ID', card.accountId],
                      ['Phone', card.phone],
                      ['Email', card.email || '—'],
                      ['Date of Birth', card.dateOfBirth || '—'],
                      ['National ID', card.nationalId || '—'],
                      ['KYC Level', `Level ${card.kycLevel}`],
                    ].map(([k, v]) => (
                      <TableRow key={k}>
                        <TableCell sx={{ width: '35%', color: 'text.secondary', borderBottom: 'none', py: 0.5 }}>{k}</TableCell>
                        <TableCell sx={{ borderBottom: 'none', py: 0.5 }}>{v}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>

                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 2, mb: 1 }}>Balances</Typography>
                {(card.balances || []).map(b => (
                  <Box key={b.accountIdRaw} sx={{ display: 'flex', justifyContent: 'space-between', py: 0.5 }}>
                    <Typography>{b.currency}</Typography>
                    <Typography variant="h6">{Number(b.balance).toLocaleString()}</Typography>
                  </Box>
                ))}
              </CardContent>
            )}

            {tab === 1 && (
              <CardContent>
                <Box sx={{ display: 'flex', gap: 1, mb: 2, alignItems: 'center', flexWrap: 'wrap' }}>
                  <TextField
                    type="date" size="small" label="From"
                    value={from}
                    onChange={(e) => setFrom(e.target.value)}
                    InputLabelProps={{ shrink: true }}
                  />
                  <TextField
                    type="date" size="small" label="To"
                    value={to}
                    onChange={(e) => setTo(e.target.value)}
                    InputLabelProps={{ shrink: true }}
                  />
                  <Button size="small" variant="outlined" onClick={fetchTxns} disabled={txnLoading}>
                    {txnLoading ? <CircularProgress size={16} /> : 'Apply'}
                  </Button>
                  <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
                    {txns.length} transaction{txns.length === 1 ? '' : 's'}
                  </Typography>
                </Box>

                {txnLoading && <CircularProgress size={20} />}

                {!txnLoading && txns.length === 0 && (
                  <Typography color="text.secondary">No transactions in this period.</Typography>
                )}

                {!txnLoading && txns.length > 0 && (
                  <Box sx={{ maxHeight: 480, overflowY: 'auto' }}>
                    <Table size="small" stickyHeader>
                      <TableHead>
                        <TableRow>
                          <TableCell>Date</TableCell>
                          <TableCell>Type</TableCell>
                          <TableCell>Currency</TableCell>
                          <TableCell align="right">Amount</TableCell>
                          <TableCell>Status</TableCell>
                          <TableCell>Reference</TableCell>
                        </TableRow>
                      </TableHead>
                      <TableBody>
                        {txns.map(t => (
                          <TableRow key={t.id} hover>
                            <TableCell>{new Date(t.createdAt).toLocaleDateString()} {new Date(t.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</TableCell>
                            <TableCell>{t.type}</TableCell>
                            <TableCell>{t.currency}</TableCell>
                            <TableCell align="right">{Number(t.amount).toLocaleString()}</TableCell>
                            <TableCell>
                              <Chip
                                label={t.status}
                                size="small"
                                color={t.status === 'completed' ? 'success' : t.status === 'pending' ? 'warning' : 'default'}
                              />
                            </TableCell>
                            <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>{t.reference || '—'}</TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  </Box>
                )}
              </CardContent>
            )}

            {tab === 2 && (
              <CardContent>
                <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 1 }}>
                  <Typography variant="subtitle1" sx={{ flex: 1 }}>Assets in custody</Typography>
                  <Button size="small" variant="outlined" onClick={fetchAssets} disabled={assetsLoading}>Refresh</Button>
                  <Button size="small" variant="contained" onClick={() => setDepositOpen(true)}>Deposit asset</Button>
                </Box>

                {assetsLoading && <CircularProgress size={20} />}
                {assetsError && <Alert severity="error" sx={{ mb: 2 }}>{assetsError}</Alert>}

                {!assetsLoading && assets.length === 0 && !assetsError && (
                  <Typography color="text.secondary">No assets in custody for this customer.</Typography>
                )}

                {!assetsLoading && assets.length > 0 && (
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Asset</TableCell>
                        <TableCell>Type</TableCell>
                        <TableCell>Qty</TableCell>
                        <TableCell>Receipt #</TableCell>
                        <TableCell>Deposit House</TableCell>
                        <TableCell align="right">Last value</TableCell>
                        <TableCell>Status</TableCell>
                        <TableCell align="right">Action</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {assets.map((a) => (
                        <TableRow key={a.assetUuid} hover>
                          <TableCell>
                            <Typography variant="body2">{a.description}</Typography>
                            <Typography variant="caption" color="text.secondary">{a.id}</Typography>
                          </TableCell>
                          <TableCell><Chip label={a.assetType} size="small" /></TableCell>
                          <TableCell>{a.quantity} {a.unit}</TableCell>
                          <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{a.receiptNumber}</TableCell>
                          <TableCell>{a.depositHouse}</TableCell>
                          <TableCell align="right">
                            {a.lastValuation > 0
                              ? `$${Number(a.lastValuation).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
                              : '—'}
                          </TableCell>
                          <TableCell>
                            <Chip label={a.status} size="small" color={a.status === 'Active' ? 'success' : 'default'} />
                            {a.isCollateral && (
                              <Tooltip title={`Collateral on ${a.collateralLoan?.reference || a.collateralLoan?.loanId} — outstanding ${a.collateralLoan?.currency} ${Number(a.collateralLoan?.outstanding ?? 0).toLocaleString()}`}>
                                <Chip label="Collateral" size="small" color="warning" sx={{ ml: 0.5 }} />
                              </Tooltip>
                            )}
                          </TableCell>
                          <TableCell align="right">
                            <Tooltip title={a.isCollateral ? `Cannot withdraw — collateral on ${a.collateralLoan?.reference || a.collateralLoan?.loanId}` : ''}>
                              <span>
                                <Button
                                  size="small"
                                  variant="outlined"
                                  color="warning"
                                  disabled={a.isCollateral || a.status !== 'Active'}
                                  onClick={() => setWithdrawTarget(a)}
                                >
                                  Withdraw
                                </Button>
                              </span>
                            </Tooltip>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                )}
              </CardContent>
            )}
          </Card>
        </Grid>
      </Grid>

      {depositOpen && (
        <DepositAssetDialog
          accountId={accountId}
          customerName={card.fullName}
          onClose={() => setDepositOpen(false)}
          onSaved={() => { setDepositOpen(false); fetchAssets(); }}
        />
      )}
      {withdrawTarget && (
        <WithdrawAssetDialog
          asset={withdrawTarget}
          onClose={() => setWithdrawTarget(null)}
          onWithdrawn={() => { setWithdrawTarget(null); fetchAssets(); }}
        />
      )}
    </Box>
  );
}

// ─── Deposit / Withdraw dialogs ─────────────────────────────────────────────

const ASSET_TYPES = ['GoldCoin', 'GoldBar', 'Silver', 'Platinum', 'PreciousStone', 'Other'];
const UNITS = ['coins', 'bars', 'grams', 'oz', 'carats', 'units'];

function DepositAssetDialog({ accountId, customerName, onClose, onSaved }) {
  const [houses, setHouses] = useState([]);
  const [form, setForm] = useState({
    depositHouseId: '',
    receiptNumber: '',
    assetType: 'GoldCoin',
    description: '',
    quantity: '',
    unit: 'coins',
    weightGrams: '',
    purity: '',
    initialValuation: '',
    currency: 'USD',
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    listDepositHouses().then(setHouses).catch(() => setHouses([]));
  }, []);

  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  const canSubmit = form.depositHouseId && form.receiptNumber && form.description &&
                    Number(form.quantity) > 0 && !busy;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await registerCustomerAsset(accountId, {
        depositHouseId:    form.depositHouseId,
        receiptNumber:     form.receiptNumber.trim(),
        assetType:         form.assetType,
        description:       form.description.trim(),
        quantity:          Number(form.quantity),
        unit:              form.unit,
        weightGrams:       form.weightGrams ? Number(form.weightGrams) : null,
        purity:            form.purity ? Number(form.purity) : null,
        initialValuation:  form.initialValuation ? Number(form.initialValuation) : null,
        currency:          form.currency,
      });
      onSaved();
    } catch (e) {
      setError(e.message || 'Deposit failed');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Deposit asset for {customerName || 'customer'}</DialogTitle>
      <DialogContent>
        <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mt: 1 }}>
          <TextField select label="Deposit house" value={form.depositHouseId} onChange={set('depositHouseId')} fullWidth size="small" sx={{ gridColumn: '1 / -1' }}>
            {houses.map((h) => <MenuItem key={h.id} value={h.id}>{h.name} — {h.city}</MenuItem>)}
          </TextField>
          <TextField label="Receipt #" value={form.receiptNumber} onChange={set('receiptNumber')} size="small" />
          <TextField select label="Asset type" value={form.assetType} onChange={set('assetType')} size="small">
            {ASSET_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
          </TextField>
          <TextField label="Description" value={form.description} onChange={set('description')} size="small" sx={{ gridColumn: '1 / -1' }} />
          <TextField label="Quantity" type="number" inputProps={{ min: 0, step: 0.01 }} value={form.quantity} onChange={set('quantity')} size="small" />
          <TextField select label="Unit" value={form.unit} onChange={set('unit')} size="small">
            {UNITS.map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
          </TextField>
          <TextField label="Weight (g, optional)" type="number" inputProps={{ min: 0, step: 0.001 }} value={form.weightGrams} onChange={set('weightGrams')} size="small" />
          <TextField label="Purity (0–1, optional)" type="number" inputProps={{ min: 0, max: 1, step: 0.0001 }} value={form.purity} onChange={set('purity')} size="small" />
          <TextField label="Initial valuation (optional)" type="number" inputProps={{ min: 0, step: 0.01 }} value={form.initialValuation} onChange={set('initialValuation')} size="small" />
          <TextField select label="Currency" value={form.currency} onChange={set('currency')} size="small">
            <MenuItem value="USD">USD</MenuItem>
            <MenuItem value="ZWG">ZWG</MenuItem>
          </TextField>
        </Box>
        {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={!canSubmit}>{busy ? 'Saving…' : 'Deposit'}</Button>
      </DialogActions>
    </Dialog>
  );
}

function WithdrawAssetDialog({ asset, onClose, onWithdrawn }) {
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [blockingLoan, setBlockingLoan] = useState(null);

  const submit = async () => {
    setBusy(true);
    setError(null);
    setBlockingLoan(null);
    try {
      await withdrawAsset(asset.assetUuid, reason);
      onWithdrawn();
    } catch (e) {
      if (e.status === 409 && e.body?.blockingLoan) {
        setBlockingLoan(e.body.blockingLoan);
        setError(e.body.error || 'Asset is collateral on an open loan.');
      } else {
        setError(e.message || 'Withdrawal failed');
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Withdraw asset {asset.id}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" sx={{ mb: 1 }}>{asset.description}</Typography>
        <Typography variant="caption" color="text.secondary">
          {asset.quantity} {asset.unit} · receipt {asset.receiptNumber} · {asset.depositHouse}
        </Typography>
        <TextField
          label="Reason (optional)" value={reason} onChange={(e) => setReason(e.target.value)}
          size="small" fullWidth sx={{ mt: 2 }}
        />
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
            {blockingLoan && (
              <Box sx={{ mt: 1 }}>
                <Typography variant="caption">
                  Loan {blockingLoan.reference || blockingLoan.loanId} — outstanding {blockingLoan.currency} {Number(blockingLoan.outstanding).toLocaleString()}
                </Typography>
              </Box>
            )}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button variant="contained" color="warning" onClick={submit} disabled={busy}>
          {busy ? 'Releasing…' : 'Confirm withdrawal'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
