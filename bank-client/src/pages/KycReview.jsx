import { useState, useMemo } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button,
  Dialog, DialogTitle, DialogContent, DialogActions, TextField, MenuItem,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  LinearProgress,
} from '@mui/material';
import { CheckCircle, Cancel, HelpOutline } from '@mui/icons-material';
import { generateKycQueue } from '../services/api';
import { useSnackbar } from '../services/snackbar';

function MatchIcon({ match }) {
  if (match === true) return <CheckCircle color="success" fontSize="small" />;
  if (match === false) return <Cancel color="error" fontSize="small" />;
  return <HelpOutline color="disabled" fontSize="small" />;
}

export default function KycReview() {
  const notify = useSnackbar();
  const queue = useMemo(() => generateKycQueue(), []);
  const [reviewOpen, setReviewOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [decision, setDecision] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [notes, setNotes] = useState('');

  const openReview = (item) => { setSelected(item); setDecision(''); setRejectReason(''); setNotes(''); setReviewOpen(true); };

  const handleSubmit = () => {
    notify(`KYC ${decision} for ${selected.name}`);
    setReviewOpen(false);
  };

  return (
    <Box>
      <Typography variant="h5" gutterBottom>KYC Review</Typography>
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[['Auto-Approved Today', 12, 'success'], ['Pending Review', queue.length, 'warning'], ['Rejected Today', 2, 'error']].map(([label, val, color]) => (
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
              <TableCell>ID</TableCell><TableCell>Name</TableCell><TableCell>Level</TableCell>
              <TableCell>Submitted</TableCell><TableCell>Face Match</TableCell>
              <TableCell>AI Decision</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {queue.map((item) => (
              <TableRow key={item.id} hover>
                <TableCell>{item.id}</TableCell>
                <TableCell>{item.name}</TableCell>
                <TableCell>Level {item.level}</TableCell>
                <TableCell>{item.submittedDate}</TableCell>
                <TableCell>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                    <LinearProgress variant="determinate" value={item.faceMatchScore * 100} sx={{ flex: 1, height: 8, borderRadius: 4 }}
                      color={item.faceMatchScore > 0.9 ? 'success' : item.faceMatchScore > 0.8 ? 'warning' : 'error'} />
                    <Typography variant="body2">{(item.faceMatchScore * 100).toFixed(0)}%</Typography>
                  </Box>
                </TableCell>
                <TableCell><Chip label={item.aiDecision} color={item.aiDecision === 'AutoApproved' ? 'success' : 'error'} size="small" /></TableCell>
                <TableCell><Button size="small" variant="outlined" onClick={() => openReview(item)}>Review</Button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={reviewOpen} onClose={() => setReviewOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>KYC Review: {selected?.name}</DialogTitle>
        <DialogContent>
          {selected && (
            <Grid container spacing={2} sx={{ mt: 1 }}>
              <Grid size={12}>
                <Typography variant="subtitle1" gutterBottom>Face Match Score</Typography>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                  <LinearProgress variant="determinate" value={selected.faceMatchScore * 100} sx={{ flex: 1, height: 12, borderRadius: 6 }}
                    color={selected.faceMatchScore > 0.9 ? 'success' : selected.faceMatchScore > 0.8 ? 'warning' : 'error'} />
                  <Typography variant="h6">{(selected.faceMatchScore * 100).toFixed(1)}%</Typography>
                </Box>
              </Grid>
              <Grid size={12}>
                <Typography variant="subtitle1" gutterBottom>Extracted Field Comparison</Typography>
                <Table size="small">
                  <TableHead><TableRow><TableCell>Field</TableCell><TableCell>Extracted</TableCell><TableCell>Match</TableCell></TableRow></TableHead>
                  <TableBody>
                    <TableRow><TableCell>Name</TableCell><TableCell>{selected.extractedName}</TableCell><TableCell><MatchIcon match={selected.nameMatch} /></TableCell></TableRow>
                    <TableRow><TableCell>ID Number</TableCell><TableCell>{selected.extractedIdNumber}</TableCell><TableCell><MatchIcon match={selected.idMatch} /></TableCell></TableRow>
                    <TableRow><TableCell>Date of Birth</TableCell><TableCell>{selected.extractedDob}</TableCell><TableCell><MatchIcon match={selected.dobMatch} /></TableCell></TableRow>
                  </TableBody>
                </Table>
              </Grid>
              <Grid size={12}>
                <Typography variant="subtitle1" gutterBottom>AI Decision</Typography>
                <Chip label={selected.aiDecision} color={selected.aiDecision === 'AutoApproved' ? 'success' : 'error'} />
                {selected.aiReason && <Typography variant="body2" color="error" sx={{ mt: 0.5 }}>{selected.aiReason}</Typography>}
              </Grid>
              <Grid size={12}>
                <TextField select fullWidth label="Decision" value={decision} onChange={(e) => setDecision(e.target.value)}>
                  <MenuItem value="Approve">Approve</MenuItem>
                  <MenuItem value="Reject">Reject</MenuItem>
                  <MenuItem value="Escalate">Escalate to Compliance</MenuItem>
                </TextField>
              </Grid>
              {decision === 'Reject' && (
                <Grid size={12}>
                  <TextField fullWidth label="Reject Reason" required value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} />
                </Grid>
              )}
              <Grid size={12}>
                <TextField fullWidth label="Notes (optional)" multiline rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
              </Grid>
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
