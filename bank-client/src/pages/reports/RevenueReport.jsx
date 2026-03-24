import { useState, useMemo } from 'react';
import {
  Box, Typography, Card, CardContent, TextField, MenuItem,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { generateRevenueData } from '../../services/api';

export default function RevenueReport() {
  const [granularity, setGranularity] = useState('Daily');
  const data = useMemo(() => generateRevenueData(granularity), [granularity]);

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Revenue Report</Typography>
        <TextField select size="small" value={granularity} onChange={(e) => setGranularity(e.target.value)} sx={{ minWidth: 140 }}>
          {['Daily', 'Weekly', 'Monthly'].map((g) => <MenuItem key={g} value={g}>{g}</MenuItem>)}
        </TextField>
      </Box>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="body2" color="text.secondary">Total Revenue (ZWG)</Typography>
          <Typography variant="h3" color="success.main">{data.totalRevenue.toLocaleString()}</Typography>
        </CardContent>
      </Card>

      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>Revenue Over Time</Typography>
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={data.data}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="period" tick={{ fontSize: 12 }} />
              <YAxis />
              <Tooltip />
              <Bar dataKey="revenue" fill="#1976d2" />
            </BarChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Revenue by Transaction Type</Typography>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow><TableCell>Type</TableCell><TableCell align="right">Amount (ZWG)</TableCell><TableCell align="right">%</TableCell></TableRow>
              </TableHead>
              <TableBody>
                {data.breakdown.map((b) => (
                  <TableRow key={b.type}>
                    <TableCell>{b.type}</TableCell>
                    <TableCell align="right">{b.amount.toLocaleString()}</TableCell>
                    <TableCell align="right">{b.percentage}%</TableCell>
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
