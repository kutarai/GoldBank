import { useState, useMemo } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, LinearProgress, Alert,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { generateLoans } from '../services/api';
import { useSnackbar } from '../services/snackbar';

const VERIFY_COLORS = { Verified: 'success', Partial: 'warning', Failed: 'error', 'Not Available': 'default' };

export default function LoanReview() {
  const notify = useSnackbar();
  const loans = useMemo(() => generateLoans(), []);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [decision, setDecision] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [notes, setNotes] = useState('');

  const openReview = (loan) => { setSelected(loan); setDecision(''); setRejectReason(''); setNotes(''); setReviewOpen(true); };
  const handleSubmit = () => { notify(`Loan ${selected.id}: ${decision}`); setReviewOpen(false); };

  const incomeVariance = selected ? Math.abs((selected.extractedIncome - selected.statedIncome) / selected.statedIncome * 100) : 0;

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Loan Review</Typography>
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
              </Grid>
              <Grid size={6}>
                <Typography variant="subtitle2" color="text.secondary">Amount</Typography>
                <Typography variant="h6">${selected.amount.toLocaleString()}</Typography>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Tenure</Typography>
                <Typography>{selected.tenure} months</Typography>
                <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Purpose</Typography>
                <Typography>{selected.purpose}</Typography>
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
