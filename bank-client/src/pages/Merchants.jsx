import { useState } from 'react';
import {
  Box, Typography, TextField, MenuItem, Button, Card, CardContent,
  Dialog, DialogTitle, DialogContent, DialogActions, List, ListItem, ListItemText,
} from '@mui/material';
import { useSnackbar } from '../services/snackbar';

export default function Merchants() {
  const notify = useSnackbar();
  const [merchantId, setMerchantId] = useState('');
  const [action, setAction] = useState('');
  const [reason, setReason] = useState('');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [auditLog, setAuditLog] = useState([]);

  const handleSubmit = () => {
    setAuditLog((prev) => [{ merchantId, action, reason, timestamp: new Date().toLocaleString() }, ...prev]);
    notify(`${action} executed on ${merchantId}`);
    setConfirmOpen(false);
    setMerchantId('');
    setAction('');
    setReason('');
  };

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Merchant Management</Typography>
      <Card sx={{ maxWidth: 500, mb: 3 }}>
        <CardContent>
          <TextField fullWidth label="Merchant ID" margin="normal" value={merchantId} onChange={(e) => setMerchantId(e.target.value)} required />
          <TextField select fullWidth label="Action" margin="normal" value={action} onChange={(e) => setAction(e.target.value)} required>
            {['Approve', 'Suspend', 'Activate', 'Close'].map((a) => <MenuItem key={a} value={a}>{a}</MenuItem>)}
          </TextField>
          <TextField fullWidth label="Reason" margin="normal" multiline rows={2} value={reason} onChange={(e) => setReason(e.target.value)} required />
          <Button variant="contained" sx={{ mt: 1 }} onClick={() => setConfirmOpen(true)} disabled={!merchantId || !action || !reason}>
            Submit
          </Button>
        </CardContent>
      </Card>

      {auditLog.length > 0 && (
        <Card>
          <CardContent>
            <Typography variant="subtitle1" gutterBottom>Session Audit Log</Typography>
            <List dense>
              {auditLog.map((entry, i) => (
                <ListItem key={i}>
                  <ListItemText primary={`${entry.action} on ${entry.merchantId}`} secondary={`${entry.reason} - ${entry.timestamp}`} />
                </ListItem>
              ))}
            </List>
          </CardContent>
        </Card>
      )}

      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)}>
        <DialogTitle>Confirm Action</DialogTitle>
        <DialogContent>
          <Typography>Execute <strong>{action}</strong> on merchant <strong>{merchantId}</strong>?</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>Reason: {reason}</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleSubmit}>Confirm</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
