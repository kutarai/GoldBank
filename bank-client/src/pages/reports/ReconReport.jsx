import { useState, useMemo } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, TextField, Button, Chip,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { generateReconData } from '../../services/api';
import dayjs from 'dayjs';

export default function ReconReport() {
  const [batchDate, setBatchDate] = useState(dayjs().format('YYYY-MM-DD'));
  const [partnerCode, setPartnerCode] = useState('');
  const data = useMemo(() => generateReconData(), []);

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Reconciliation Report</Typography>

      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <TextField size="small" label="Batch Date" type="date" value={batchDate} onChange={(e) => setBatchDate(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }} />
        <TextField size="small" label="Partner Code" value={partnerCode} onChange={(e) => setPartnerCode(e.target.value)} />
        <Button variant="contained">Run Report</Button>
      </Box>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[
          ['Total Transactions', data.totalTransactions.toLocaleString()],
          ['Total Amount (ZWG)', data.totalAmount.toLocaleString()],
          ['Matched', data.matched.toLocaleString()],
          ['Unmatched', data.unmatched.toLocaleString()],
        ].map(([label, val]) => (
          <Grid size={{ xs: 6, sm: 3 }} key={label}>
            <Card><CardContent>
              <Typography variant="body2" color="text.secondary">{label}</Typography>
              <Typography variant="h5">{val}</Typography>
            </CardContent></Card>
          </Grid>
        ))}
      </Grid>

      <Box sx={{ mb: 2 }}>
        <Chip label={`Status: ${data.status}`} color="success" />
      </Box>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Discrepancies</Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Transaction ID</TableCell>
                  <TableCell align="right">Internal Amount</TableCell>
                  <TableCell align="right">Partner Amount</TableCell>
                  <TableCell align="right">Difference</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.discrepancies.map((d) => (
                  <TableRow key={d.transactionId}>
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{d.transactionId}</TableCell>
                    <TableCell align="right">{d.internalAmount.toLocaleString()}</TableCell>
                    <TableCell align="right">{d.partnerAmount.toLocaleString()}</TableCell>
                    <TableCell align="right" sx={{ color: d.difference < 0 ? 'error.main' : 'success.main' }}>
                      {d.difference > 0 ? '+' : ''}{d.difference.toFixed(2)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </CardContent>
      </Card>
    </Box>
  );
}
