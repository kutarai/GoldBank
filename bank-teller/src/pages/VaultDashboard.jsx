import { useEffect, useState, useCallback } from 'react';
import {
  Box, Card, CardContent, Grid, Typography, Button, Chip, Alert, MenuItem, TextField,
  Dialog, DialogTitle, DialogContent, DialogActions, CircularProgress, Table, TableBody,
  TableCell, TableHead, TableRow, Tooltip,
} from '@mui/material';
import RefreshIcon from '@mui/icons-material/Refresh';
import { useNavigate } from 'react-router-dom';
import { useTellerSession } from '../auth/TellerSessionContext';
import {
  listVaults, getVault, postVaultMovement, postVaultSpotCheck, listVaultMovements, openVaultEodReport,
} from '../services/api';
import DenominationGrid, { toBreakdown, getDenominationsFor } from '../components/DenominationGrid';

const MOVEMENT_TYPES = [
  { value: 'CashInjection',    direction: 'In',  label: 'Cash Injection (CIT in)'  },
  { value: 'CashWithdrawal',   direction: 'Out', label: 'Withdrawal to HQ (CIT out)' },
  { value: 'DrawerIssue',      direction: 'Out', label: 'Issue Float to Teller'    },
  { value: 'DrawerSurrender',  direction: 'In',  label: 'Receive Surrender'        },
];

export default function VaultDashboard() {
  const { user } = useTellerSession();
  const navigate = useNavigate();
  const [vault, setVault] = useState(null);
  const [details, setDetails] = useState(null);
  const [movements, setMovements] = useState([]);
  const [denomRegistry, setDenomRegistry] = useState({}); // currency → [{face,type,id}]
  const [error, setError] = useState(null);

  // Movement dialog
  const [movement, setMovement] = useState(null); // { type, direction, label }
  const [mvCurrency, setMvCurrency] = useState('USD');
  const [mvDenoms, setMvDenoms] = useState({});
  const [mvNotes, setMvNotes] = useState('');
  const [mvSubmitting, setMvSubmitting] = useState(false);
  const [mvErr, setMvErr] = useState(null);

  // Spot-check dialog
  const [spotOpen, setSpotOpen] = useState(false);
  const [actualCounts, setActualCounts] = useState({}); // denomId → count
  const [witnessId, setWitnessId] = useState('');
  const [acceptVar, setAcceptVar] = useState(false);
  const [spotSubmitting, setSpotSubmitting] = useState(false);
  const [spotErr, setSpotErr] = useState(null);
  const [spotResult, setSpotResult] = useState(null);

  // Bootstrap: list vaults for branch, pick first, load details
  const refresh = useCallback(async () => {
    try {
      const vs = await listVaults(user?.branchId);
      if (!vs || vs.length === 0) { setError('No vault assigned to this branch.'); return; }
      const v = vs[0];
      setVault(v);
      const [d, m] = await Promise.all([
        getVault(v.id),
        listVaultMovements(v.id, 30),
      ]);
      setDetails(d);
      setMovements(m);
      // Load denomination registry per currency from /denominations
      const ccys = Array.from(new Set((d.stock || []).map(s => s.currency).concat(['USD', 'ZWG'])));
      const reg = {};
      for (const ccy of ccys) {
        const r = await fetch(`${import.meta.env.VITE_API_BASE || 'http://localhost:5001/api/teller'}/denominations?currency=${ccy}`);
        const j = await r.json();
        reg[ccy] = j.denominations || [];
      }
      setDenomRegistry(reg);
    } catch (e) {
      setError(e.message || 'Failed to load vault');
    }
  }, [user?.branchId]);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, 60000);
    return () => clearInterval(id);
  }, [refresh]);

  // ---- Movement helpers ----
  const denomLookup = (currency) => denomRegistry[currency] || [];
  const denomIdByFace = (currency, face) => {
    const row = denomLookup(currency).find(d => Number(d.face) === Number(face));
    return row?.denominationId || row?.id || null;
  };

  // Derive { denominationId, face, count } lines from the DenominationGrid value
  const buildLines = (currency, value) => {
    return Object.entries(value || {})
      .filter(([, c]) => Number(c) > 0)
      .map(([face, c]) => {
        // The /denominations API returns { face, denominationId }; older shape uses { id }
        const id = denomIdByFace(currency, face);
        return { denominationId: id, face: Number(face), count: Number(c) };
      });
  };

  const openMovement = (mv) => {
    setMovement(mv);
    setMvCurrency('USD');
    setMvDenoms({});
    setMvNotes('');
    setMvErr(null);
  };

  const submitMovement = async () => {
    setMvSubmitting(true);
    setMvErr(null);
    try {
      const lines = buildLines(mvCurrency, mvDenoms);
      if (lines.length === 0) throw new Error('Enter at least one denomination');
      if (lines.some(l => !l.denominationId)) throw new Error('Some denominations are not in the registry');
      const total = lines.reduce((s, l) => s + l.face * l.count, 0);
      await postVaultMovement(vault.id, {
        type: movement.value,
        direction: movement.direction,
        currency: mvCurrency,
        totalAmount: total,
        denominations: lines,
        notes: mvNotes || null,
      });
      setMovement(null);
      await refresh();
    } catch (err) {
      setMvErr(err.body?.message || err.body?.error || err.message);
    } finally {
      setMvSubmitting(false);
    }
  };

  // ---- Spot check helpers ----
  const openSpot = () => {
    // Pre-fill actualCounts with current expected stock
    const initial = {};
    (details?.stock || []).forEach(s => { initial[s.denominationId] = s.count; });
    setActualCounts(initial);
    setWitnessId('');
    setAcceptVar(false);
    setSpotErr(null);
    setSpotResult(null);
    setSpotOpen(true);
  };

  const submitSpot = async () => {
    setSpotSubmitting(true);
    setSpotErr(null);
    try {
      const res = await postVaultSpotCheck(vault.id, {
        witnessId,
        actualCounts,
        acceptVariance: acceptVar,
      });
      setSpotResult(res);
      await refresh();
    } catch (err) {
      // Variance preflight: 409 returned
      if (err.status === 409 && err.body?.error === 'Vault.VarianceFound') {
        setSpotErr(`Variance found — toggle "Confirm variance" and re-submit to post adjustment.`);
      } else {
        setSpotErr(err.body?.message || err.body?.error || err.message);
      }
    } finally {
      setSpotSubmitting(false);
    }
  };

  if (error) return <Alert severity="error">{error}</Alert>;
  if (!details) return <CircularProgress />;

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 2 }}>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>{details.name}</Typography>
        <Chip
          label={`Last spot check: ${details.lastSpotCheckResult}`}
          color={details.lastSpotCheckResult === 'Balanced' ? 'success' : details.lastSpotCheckResult === 'Variance' ? 'warning' : 'default'}
          size="small"
        />
        <Button startIcon={<RefreshIcon />} onClick={refresh} size="small">Refresh</Button>
      </Box>

      <Grid container spacing={2} sx={{ mb: 2 }}>
        {Object.entries(details.totalsByCurrency || {}).map(([ccy, total]) => (
          <Grid key={ccy} size={{ xs: 12, sm: 4 }}>
            <Card>
              <CardContent>
                <Typography variant="caption" color="text.secondary">Vault total {ccy}</Typography>
                <Typography variant="h4">{Number(total).toLocaleString()}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 2 }}>
        {MOVEMENT_TYPES.map(mt => (
          <Button key={mt.value} variant="outlined" onClick={() => openMovement(mt)}>
            {mt.label}
          </Button>
        ))}
        <Button variant="contained" color="warning" onClick={openSpot}>Run Spot Check</Button>
        <Button variant="text" onClick={() => openVaultEodReport(vault.id)}>Print Vault EOD Report</Button>
      </Box>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 1 }}>Denomination Stock</Typography>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Currency</TableCell>
                    <TableCell>Face</TableCell>
                    <TableCell>Type</TableCell>
                    <TableCell align="right">Count</TableCell>
                    <TableCell align="right">Value</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {(details.stock || []).map(s => (
                    <TableRow key={s.denominationId}>
                      <TableCell>{s.currency}</TableCell>
                      <TableCell>{Number(s.face).toLocaleString()}</TableCell>
                      <TableCell>{s.type}</TableCell>
                      <TableCell align="right">{s.count}</TableCell>
                      <TableCell align="right">{Number(s.value).toLocaleString()}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ mb: 1 }}>Recent Movements</Typography>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Time</TableCell>
                    <TableCell>Type</TableCell>
                    <TableCell>Dir</TableCell>
                    <TableCell>Ccy</TableCell>
                    <TableCell align="right">Amount</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {movements.map(m => (
                    <TableRow key={m.id}>
                      <TableCell>{new Date(m.createdAt).toLocaleTimeString()}</TableCell>
                      <TableCell>{m.type}</TableCell>
                      <TableCell>
                        <Chip size="small" label={m.direction} color={m.direction === 'In' ? 'success' : 'warning'} />
                      </TableCell>
                      <TableCell>{m.currency}</TableCell>
                      <TableCell align="right">{Number(m.totalAmount).toLocaleString()}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Movement dialog */}
      <Dialog open={!!movement} onClose={() => setMovement(null)} maxWidth="md" fullWidth>
        <DialogTitle>{movement?.label}</DialogTitle>
        <DialogContent>
          <TextField
            select fullWidth margin="normal" label="Currency"
            value={mvCurrency}
            onChange={(e) => { setMvCurrency(e.target.value); setMvDenoms({}); }}
          >
            <MenuItem value="USD">USD</MenuItem>
            <MenuItem value="ZWG">ZWG</MenuItem>
          </TextField>
          <DenominationGrid
            currency={mvCurrency}
            value={mvDenoms}
            onChange={setMvDenoms}
            targetAmount={null}
          />
          <TextField
            fullWidth margin="normal" label="Notes" multiline rows={2}
            value={mvNotes}
            onChange={(e) => setMvNotes(e.target.value)}
          />
          {mvErr && <Alert severity="error" sx={{ mt: 1 }}>{mvErr}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMovement(null)}>Cancel</Button>
          <Button variant="contained" onClick={submitMovement} disabled={mvSubmitting}>
            {mvSubmitting ? <CircularProgress size={20} /> : 'Confirm Movement'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Spot-check dialog */}
      <Dialog open={spotOpen} onClose={() => setSpotOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Vault Spot Check</DialogTitle>
        <DialogContent>
          <Alert severity="info" sx={{ mb: 2 }}>
            Physically count each denomination in the vault and enter the actual count below.
            A distinct witness must approve.
          </Alert>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Currency</TableCell>
                <TableCell>Face</TableCell>
                <TableCell align="right">Expected</TableCell>
                <TableCell align="right">Counted</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(details.stock || []).map(s => (
                <TableRow key={s.denominationId}>
                  <TableCell>{s.currency}</TableCell>
                  <TableCell>{Number(s.face).toLocaleString()}</TableCell>
                  <TableCell align="right">{s.count}</TableCell>
                  <TableCell align="right" sx={{ width: 110 }}>
                    <TextField
                      type="number" size="small"
                      value={actualCounts[s.denominationId] ?? 0}
                      onChange={(e) => setActualCounts(prev => ({ ...prev, [s.denominationId]: Number(e.target.value) }))}
                      inputProps={{ min: 0, style: { textAlign: 'right' } }}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          <TextField
            fullWidth margin="normal" label="Witness User ID (admin_users.Id)"
            value={witnessId}
            onChange={(e) => setWitnessId(e.target.value)}
            helperText="In the next iteration this becomes a witness picker + PIN"
          />
          <Box sx={{ mt: 1 }}>
            <Button
              variant={acceptVar ? 'contained' : 'outlined'}
              color="warning"
              size="small"
              onClick={() => setAcceptVar(v => !v)}
            >
              {acceptVar ? '☒' : '☐'} Confirm variance and post adjustment
            </Button>
          </Box>
          {spotErr && <Alert severity="warning" sx={{ mt: 2 }}>{spotErr}</Alert>}
          {spotResult && (
            <Alert severity={spotResult.hasVariance ? 'warning' : 'success'} sx={{ mt: 2 }}>
              Spot check {spotResult.hasVariance ? 'completed with variance posted' : 'balanced'}.
            </Alert>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSpotOpen(false)}>Close</Button>
          <Button variant="contained" onClick={submitSpot} disabled={spotSubmitting || !witnessId}>
            {spotSubmitting ? <CircularProgress size={20} /> : 'Submit'}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
