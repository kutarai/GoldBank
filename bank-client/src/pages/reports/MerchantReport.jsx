import { useState, useEffect } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, LinearProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { generateMerchantData } from '../../services/api';

export default function MerchantReport() {
  const [data, setData] = useState({ totalVolume: 0, totalTransactions: 0, merchants: [] });
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    generateMerchantData().then(setData).finally(() => setLoading(false));
  }, []);

  return (
    <Box>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      <Typography variant="h5" gutterBottom>Merchant Performance Report</Typography>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Card><CardContent>
            <Typography variant="body2" color="text.secondary">Total Volume (ZWG)</Typography>
            <Typography variant="h4" color="primary.main">{data.totalVolume.toLocaleString()}</Typography>
          </CardContent></Card>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Card><CardContent>
            <Typography variant="body2" color="text.secondary">Total Transactions</Typography>
            <Typography variant="h4" color="info.main">{data.totalTransactions.toLocaleString()}</Typography>
          </CardContent></Card>
        </Grid>
      </Grid>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Merchant Breakdown</Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Merchant</TableCell><TableCell>ID</TableCell>
                  <TableCell align="right">Volume (ZWG)</TableCell><TableCell align="right">Transactions</TableCell>
                  <TableCell align="right">Commission (ZWG)</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.merchants.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell>{m.name}</TableCell>
                    <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{m.id}</TableCell>
                    <TableCell align="right">{m.volume.toLocaleString()}</TableCell>
                    <TableCell align="right">{m.transactions.toLocaleString()}</TableCell>
                    <TableCell align="right">{m.commission.toLocaleString()}</TableCell>
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
