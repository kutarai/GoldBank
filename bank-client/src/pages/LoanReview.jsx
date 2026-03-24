import { useState, useEffect } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, LinearProgress, Alert,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  Tab, Tabs, IconButton, Tooltip, InputAdornment,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import { Edit, Save, Add, Delete } from '@mui/icons-material';
import { generateLoans } from '../services/api';
import { useSnackbar } from '../services/snackbar';

const VERIFY_COLORS = { Verified: 'success', Partial: 'warning', Failed: 'error', 'Not Available': 'default' };

// ── Default Rate Configuration ───────────────────────────────────────────────
const DEFAULT_RATES = [
  { id: 1, scoreMin: 800, scoreMax: 1000, label: 'Excellent', tenure6: 16.0, tenure12: 17.0, tenure24: 18.0, tenure36: 19.0, tenure48: 20.0 },
  { id: 2, scoreMin: 650, scoreMax: 799, label: 'Good', tenure6: 19.0, tenure12: 20.0, tenure24: 22.0, tenure36: 23.0, tenure48: 24.0 },
  { id: 3, scoreMin: 500, scoreMax: 649, label: 'Fair', tenure6: 23.0, tenure12: 24.0, tenure24: 26.0, tenure36: 27.0, tenure48: 28.0 },
  { id: 4, scoreMin: 350, scoreMax: 499, label: 'Poor', tenure6: 27.0, tenure12: 28.0, tenure24: 30.0, tenure36: 31.0, tenure48: 32.0 },
  { id: 5, scoreMin: 0, scoreMax: 349, label: 'Very Poor', tenure6: 32.0, tenure12: 33.0, tenure24: 35.0, tenure36: 36.0, tenure48: 36.0 },
];

const TENURE_COLS = [
  { key: 'tenure6', label: '6 months' },
  { key: 'tenure12', label: '12 months' },
  { key: 'tenure24', label: '24 months' },
  { key: 'tenure36', label: '36 months' },
  { key: 'tenure48', label: '48 months' },
];

export default function LoanReview() {
  const notify = useSnackbar();
  const [loans, setLoans] = useState([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    generateLoans().then(setLoans).finally(() => setLoading(false));
  }, []);
  const [tab, setTab] = useState(0);

  // Review dialog
  const [reviewOpen, setReviewOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [decision, setDecision] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [notes, setNotes] = useState('');

  // Rate configuration
  const [rates, setRates] = useState(DEFAULT_RATES);
  const [editingRateId, setEditingRateId] = useState(null);
  const [rateForm, setRateForm] = useState({});
  const [addRateOpen, setAddRateOpen] = useState(false);
  const [newRate, setNewRate] = useState({ scoreMin: 0, scoreMax: 0, label: '', tenure6: 0, tenure12: 0, tenure24: 0, tenure36: 0, tenure48: 0 });

  const openReview = (loan) => { setSelected(loan); setDecision(''); setRejectReason(''); setNotes(''); setReviewOpen(true); };
  const handleSubmit = () => { notify(`Loan ${selected.id}: ${decision}`); setReviewOpen(false); };
  const incomeVariance = selected ? Math.abs((selected.extractedIncome - selected.statedIncome) / selected.statedIncome * 100) : 0;

  // Rate editing
  const startEditRate = (rate) => { setEditingRateId(rate.id); setRateForm({ ...rate }); };
  const cancelEditRate = () => { setEditingRateId(null); setRateForm({}); };
  const saveEditRate = () => {
    setRates((prev) => prev.map((r) => r.id === rateForm.id ? { ...rateForm } : r));
    notify(`Rate tier "${rateForm.label}" updated`);
    setEditingRateId(null);
  };
  const deleteRate = (id) => {
    setRates((prev) => prev.filter((r) => r.id !== id));
    notify('Rate tier removed');
  };
  const addRate = () => {
    setRates((prev) => [...prev, { ...newRate, id: Math.max(...prev.map((r) => r.id)) + 1 }].sort((a, b) => b.scoreMin - a.scoreMin));
    notify(`Rate tier "${newRate.label}" added`);
    setAddRateOpen(false);
    setNewRate({ scoreMin: 0, scoreMax: 0, label: '', tenure6: 0, tenure12: 0, tenure24: 0, tenure36: 0, tenure48: 0 });
  };

  // Calculate monthly repayment preview
  const calcMonthly = (principal, annualRate, months) => {
    const r = annualRate / 100 / 12;
    if (r === 0) return (principal / months).toFixed(2);
    const factor = Math.pow(1 + r, months);
    return (principal * (r * factor) / (factor - 1)).toFixed(2);
  };

  return (
    <Box>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      <Typography variant="h5" gutterBottom>Loan Review</Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Pending Applications" />
        <Tab label="Rate Configuration" />
        <Tab label="Repayment Calculator" />
      </Tabs>

      {/* ── Tab 0: Pending Applications ───────────────────────────────────── */}
      {tab === 0 && (
        <>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            {[['Pending Applications', loans.length, 'warning'], ['Approved This Month', 28, 'success'], ['Rejected This Month', 5, 'error']].map(([label, val, color]) => (
              <Grid size={{ xs: 12, sm: 4 }} key={label}>
                <Card><CardContent>
                  <Typography variant="body2" color="text.secondary">{label}</Typography>
                  <Typography variant="h4" color={`${color}.main`}>{val}</Typography>
                </CardContent></Card>
              </Grid>
            ))}
          </Grid>

          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Applicant</TableCell><TableCell align="right">Amount</TableCell>
                  <TableCell>Tenure</TableCell><TableCell>Purpose</TableCell>
                  <TableCell align="right">Credit Score</TableCell><TableCell>AI Verification</TableCell>
                  <TableCell>Applied</TableCell><TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {loans.map((l) => (
                  <TableRow key={l.id} hover>
                    <TableCell>{l.name}</TableCell>
                    <TableCell align="right">${l.amount.toLocaleString()}</TableCell>
                    <TableCell>{l.tenure} months</TableCell>
                    <TableCell>{l.purpose}</TableCell>
                    <TableCell align="right">{l.creditScore}</TableCell>
                    <TableCell><Chip label={l.verificationStatus} color={VERIFY_COLORS[l.verificationStatus]} size="small" /></TableCell>
                    <TableCell>{l.appliedDate}</TableCell>
                    <TableCell><Button size="small" variant="outlined" onClick={() => openReview(l)}>Review</Button></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}

      {/* ── Tab 1: Rate Configuration ─────────────────────────────────────── */}
      {tab === 1 && (
        <>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
            <Typography variant="h6">Interest Rates by Credit Score & Tenure</Typography>
            <Button variant="contained" startIcon={<Add />} onClick={() => setAddRateOpen(true)}>Add Tier</Button>
          </Box>

          <Alert severity="info" sx={{ mb: 2 }}>
            Rates are annual percentages. Changes take effect for new loan applications immediately.
          </Alert>

          <TableContainer component={Paper}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Credit Score Range</TableCell>
                  <TableCell>Rating</TableCell>
                  {TENURE_COLS.map((t) => <TableCell key={t.key} align="right">{t.label}</TableCell>)}
                  <TableCell align="center">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {rates.map((rate) => (
                  <TableRow key={rate.id} hover>
                    {editingRateId === rate.id ? (
                      <>
                        <TableCell>
                          <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                            <TextField size="small" type="number" value={rateForm.scoreMin} onChange={(e) => setRateForm({ ...rateForm, scoreMin: parseInt(e.target.value) || 0 })} sx={{ width: 70 }} />
                            <Typography>—</Typography>
                            <TextField size="small" type="number" value={rateForm.scoreMax} onChange={(e) => setRateForm({ ...rateForm, scoreMax: parseInt(e.target.value) || 0 })} sx={{ width: 70 }} />
                          </Box>
                        </TableCell>
                        <TableCell>
                          <TextField size="small" value={rateForm.label} onChange={(e) => setRateForm({ ...rateForm, label: e.target.value })} sx={{ width: 100 }} />
                        </TableCell>
                        {TENURE_COLS.map((t) => (
                          <TableCell key={t.key} align="right">
                            <TextField size="small" type="number" value={rateForm[t.key]} onChange={(e) => setRateForm({ ...rateForm, [t.key]: parseFloat(e.target.value) || 0 })}
                              sx={{ width: 70 }} InputProps={{ endAdornment: <InputAdornment position="end">%</InputAdornment> }} />
                          </TableCell>
                        ))}
                        <TableCell align="center">
                          <Tooltip title="Save"><IconButton size="small" color="success" onClick={saveEditRate}><Save fontSize="small" /></IconButton></Tooltip>
                          <Button size="small" onClick={cancelEditRate}>Cancel</Button>
                        </TableCell>
                      </>
                    ) : (
                      <>
                        <TableCell>
                          <Chip label={`${rate.scoreMin} — ${rate.scoreMax}`} size="small" variant="outlined" />
                        </TableCell>
                        <TableCell>
                          <Chip label={rate.label} size="small"
                            color={rate.label === 'Excellent' ? 'success' : rate.label === 'Good' ? 'primary' : rate.label === 'Fair' ? 'info' : rate.label === 'Poor' ? 'warning' : 'error'} />
                        </TableCell>
                        {TENURE_COLS.map((t) => (
                          <TableCell key={t.key} align="right">
                            <Typography variant="body2" fontWeight="medium">{rate[t.key].toFixed(1)}%</Typography>
                          </TableCell>
                        ))}
                        <TableCell align="center">
                          <Tooltip title="Edit"><IconButton size="small" onClick={() => startEditRate(rate)}><Edit fontSize="small" /></IconButton></Tooltip>
                          <Tooltip title="Delete"><IconButton size="small" color="error" onClick={() => deleteRate(rate.id)}><Delete fontSize="small" /></IconButton></Tooltip>
                        </TableCell>
                      </>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </>
      )}

      {/* ── Tab 2: Repayment Calculator ───────────────────────────────────── */}
      {tab === 2 && <RepaymentCalculator rates={rates} calcMonthly={calcMonthly} />}

      {/* ── Add Rate Tier Dialog ──────────────────────────────────────────── */}
      <Dialog open={addRateOpen} onClose={() => setAddRateOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Add Rate Tier</DialogTitle>
        <DialogContent>
          <Grid container spacing={2} sx={{ mt: 0.5 }}>
            <Grid size={4}>
              <TextField fullWidth label="Score Min" type="number" value={newRate.scoreMin} onChange={(e) => setNewRate({ ...newRate, scoreMin: parseInt(e.target.value) || 0 })} />
            </Grid>
            <Grid size={4}>
              <TextField fullWidth label="Score Max" type="number" value={newRate.scoreMax} onChange={(e) => setNewRate({ ...newRate, scoreMax: parseInt(e.target.value) || 0 })} />
            </Grid>
            <Grid size={4}>
              <TextField fullWidth label="Rating Label" value={newRate.label} onChange={(e) => setNewRate({ ...newRate, label: e.target.value })} />
            </Grid>
            {TENURE_COLS.map((t) => (
              <Grid size={{ xs: 6, sm: 4 }} key={t.key}>
                <TextField fullWidth label={t.label} type="number" value={newRate[t.key]}
                  onChange={(e) => setNewRate({ ...newRate, [t.key]: parseFloat(e.target.value) || 0 })}
                  InputProps={{ endAdornment: <InputAdornment position="end">% p.a.</InputAdornment> }} />
              </Grid>
            ))}
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAddRateOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={addRate} disabled={!newRate.label || newRate.scoreMax <= newRate.scoreMin}>Add Tier</Button>
        </DialogActions>
      </Dialog>

      {/* ── Loan Review Dialog ────────────────────────────────────────────── */}
      <Dialog open={reviewOpen} onClose={() => setReviewOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Loan Review: {selected?.name}</DialogTitle>
        <DialogContent>
          {selected && (
            <Grid container spacing={2} sx={{ mt: 1 }}>
              <Grid size={6}>
                <Typography variant="subtitle2" color="text.secondary">Account</Typography>
                <Typography>{selected.accountId}</Typography>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Phone</Typography>
                <Typography>{selected.phone}</Typography>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Credit Score</Typography>
                <Typography variant="h6">{selected.creditScore}</Typography>
                {(() => {
                  const tier = rates.find((r) => selected.creditScore >= r.scoreMin && selected.creditScore <= r.scoreMax);
                  return tier ? (
                    <Chip label={`${tier.label} — ${tier[`tenure${selected.tenure}`] || tier.tenure12}% p.a.`} size="small"
                      color={tier.label === 'Excellent' ? 'success' : tier.label === 'Good' ? 'primary' : 'warning'} sx={{ mt: 0.5 }} />
                  ) : null;
                })()}
              </Grid>
              <Grid size={6}>
                <Typography variant="subtitle2" color="text.secondary">Amount</Typography>
                <Typography variant="h6">${selected.amount.toLocaleString()}</Typography>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Tenure</Typography>
                <Typography>{selected.tenure} months</Typography>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Purpose</Typography>
                <Typography>{selected.purpose}</Typography>
                {(() => {
                  const tier = rates.find((r) => selected.creditScore >= r.scoreMin && selected.creditScore <= r.scoreMax);
                  const rate = tier ? (tier[`tenure${selected.tenure}`] || tier.tenure12) : 0;
                  const monthly = calcMonthly(selected.amount, rate, selected.tenure);
                  return rate ? (
                    <Box sx={{ mt: 1 }}>
                      <Typography variant="subtitle2" color="text.secondary">Est. Monthly Repayment</Typography>
                      <Typography variant="h6" color="primary.main">${monthly}</Typography>
                    </Box>
                  ) : null;
                })()}
              </Grid>

              {selected.faceMatchScore != null && (
                <Grid size={12}>
                  <Typography variant="subtitle1" gutterBottom>AI Verification</Typography>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 1 }}>
                    <Typography variant="body2">Face Match:</Typography>
                    <LinearProgress variant="determinate" value={selected.faceMatchScore * 100} sx={{ flex: 1, height: 10, borderRadius: 5 }}
                      color={selected.faceMatchScore > 0.9 ? 'success' : 'warning'} />
                    <Typography>{(selected.faceMatchScore * 100).toFixed(1)}%</Typography>
                  </Box>
                  <Table size="small">
                    <TableBody>
                      <TableRow><TableCell>Employer</TableCell><TableCell>{selected.extractedEmployer}</TableCell></TableRow>
                      <TableRow><TableCell>Extracted Income</TableCell><TableCell>${selected.extractedIncome.toLocaleString()}</TableCell></TableRow>
                      <TableRow><TableCell>Stated Income</TableCell><TableCell>${selected.statedIncome.toLocaleString()}</TableCell></TableRow>
                    </TableBody>
                  </Table>
                  {incomeVariance > 10 && <Alert severity="warning" sx={{ mt: 1 }}>Income variance {incomeVariance.toFixed(1)}% exceeds 10% threshold</Alert>}
                </Grid>
              )}

              <Grid size={12}>
                <TextField select fullWidth label="Decision" value={decision} onChange={(e) => setDecision(e.target.value)} required>
                  <MenuItem value="Approve">Approve</MenuItem>
                  <MenuItem value="Reject">Reject</MenuItem>
                  <MenuItem value="RequestDocuments">Request More Documents</MenuItem>
                </TextField>
              </Grid>
              {decision === 'Reject' && (
                <Grid size={12}><TextField fullWidth label="Reject Reason" value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} required /></Grid>
              )}
              <Grid size={12}><TextField fullWidth label="Review Notes" multiline rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} /></Grid>
            </Grid>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setReviewOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSubmit} disabled={!decision || (decision === 'Reject' && !rejectReason)}>Submit</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}

// ── Repayment Calculator Component ───────────────────────────────────────────
function RepaymentCalculator({ rates, calcMonthly }) {
  const [amount, setAmount] = useState(5000);
  const [tenure, setTenure] = useState(12);
  const [score, setScore] = useState(700);

  const tier = rates.find((r) => score >= r.scoreMin && score <= r.scoreMax);
  const tenureKey = `tenure${tenure}`;
  const rate = tier ? (tier[tenureKey] || tier.tenure12) : 0;
  const monthly = calcMonthly(amount, rate, tenure);
  const totalRepayment = (parseFloat(monthly) * tenure).toFixed(2);
  const totalInterest = (totalRepayment - amount).toFixed(2);

  return (
    <Box>
      <Typography variant="h6" gutterBottom>Repayment Calculator</Typography>
      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 4 }}>
          <Card>
            <CardContent>
              <Typography variant="subtitle2" color="text.secondary" gutterBottom>Loan Parameters</Typography>
              <TextField fullWidth label="Loan Amount" type="number" margin="normal" value={amount}
                onChange={(e) => setAmount(parseFloat(e.target.value) || 0)}
                InputProps={{ startAdornment: <InputAdornment position="start">$</InputAdornment> }} />
              <TextField select fullWidth label="Tenure" margin="normal" value={tenure} onChange={(e) => setTenure(parseInt(e.target.value))}>
                {[6, 12, 24, 36, 48].map((m) => <MenuItem key={m} value={m}>{m} months</MenuItem>)}
              </TextField>
              <TextField fullWidth label="Credit Score" type="number" margin="normal" value={score}
                onChange={(e) => setScore(parseInt(e.target.value) || 0)}
                helperText={tier ? `Rating: ${tier.label}` : 'No matching tier'} />
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 8 }}>
          {tier ? (
            <Card>
              <CardContent>
                <Typography variant="subtitle2" color="text.secondary" gutterBottom>Repayment Summary</Typography>
                <Grid container spacing={2} sx={{ mb: 2 }}>
                  {[
                    ['Interest Rate', `${rate.toFixed(1)}% p.a.`, 'info.main'],
                    ['Monthly Repayment', `$${monthly}`, 'primary.main'],
                    ['Total Repayment', `$${parseFloat(totalRepayment).toLocaleString()}`, 'text.primary'],
                    ['Total Interest', `$${parseFloat(totalInterest).toLocaleString()}`, 'warning.main'],
                  ].map(([label, value, color]) => (
                    <Grid size={3} key={label}>
                      <Typography variant="caption" color="text.secondary">{label}</Typography>
                      <Typography variant="h6" sx={{ color }}>{value}</Typography>
                    </Grid>
                  ))}
                </Grid>

                <Typography variant="subtitle2" color="text.secondary" gutterBottom>Rate Comparison for Score {score} ({tier.label})</Typography>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Tenure</TableCell>
                      <TableCell align="right">Rate</TableCell>
                      <TableCell align="right">Monthly</TableCell>
                      <TableCell align="right">Total Interest</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {TENURE_COLS.map((t) => {
                      const r = tier[t.key];
                      const m = parseInt(t.key.replace('tenure', ''));
                      const mo = calcMonthly(amount, r, m);
                      const ti = (parseFloat(mo) * m - amount).toFixed(2);
                      return (
                        <TableRow key={t.key} selected={m === tenure}>
                          <TableCell>{t.label}</TableCell>
                          <TableCell align="right">{r.toFixed(1)}%</TableCell>
                          <TableCell align="right">${mo}</TableCell>
                          <TableCell align="right">${parseFloat(ti).toLocaleString()}</TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          ) : (
            <Alert severity="warning">No rate tier matches credit score {score}. Add a tier covering this range.</Alert>
          )}
        </Grid>
      </Grid>
    </Box>
  );
}
