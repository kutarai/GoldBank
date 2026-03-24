import { useState, useEffect } from 'react';
import { Box, Typography, Card, CardContent, Grid, TextField, MenuItem, LinearProgress } from '@mui/material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import { generateUserGrowthData } from '../../services/api';

export default function UserGrowthReport() {
  const [granularity, setGranularity] = useState('Daily');
  const [data, setData] = useState({ totalRegistered: 0, totalActive: 0, growthRate: 0, data: [] });
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    setLoading(true);
    generateUserGrowthData(granularity).then(setData).finally(() => setLoading(false));
  }, [granularity]);

  return (
    <Box>
      {loading && <LinearProgress sx={{ mb: 1 }} />}
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">User Growth Report</Typography>
        <TextField select size="small" value={granularity} onChange={(e) => setGranularity(e.target.value)} sx={{ minWidth: 140 }}>
          {['Daily', 'Weekly', 'Monthly'].map((g) => <MenuItem key={g} value={g}>{g}</MenuItem>)}
        </TextField>
      </Box>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        {[['Total Registered', data.totalRegistered.toLocaleString(), 'primary'], ['Total Active', data.totalActive.toLocaleString(), 'success'], ['Growth Rate', `${data.growthRate}%`, 'info']].map(([label, val, color]) => (
          <Grid size={{ xs: 12, sm: 4 }} key={label}>
            <Card><CardContent>
              <Typography variant="body2" color="text.secondary">{label}</Typography>
              <Typography variant="h4" color={`${color}.main`}>{val}</Typography>
            </CardContent></Card>
          </Grid>
        ))}
      </Grid>

      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>User Growth Trends</Typography>
          <ResponsiveContainer width="100%" height={350}>
            <LineChart data={data.data}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="period" tick={{ fontSize: 12 }} />
              <YAxis />
              <Tooltip />
              <Legend />
              <Line type="monotone" dataKey="newRegistrations" name="New Registrations" stroke="#1976d2" strokeWidth={2} />
              <Line type="monotone" dataKey="activeUsers" name="Active Users" stroke="#2e7d32" strokeWidth={2} />
              <Line type="monotone" dataKey="churned" name="Churned" stroke="#d32f2f" strokeWidth={2} />
            </LineChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </Box>
  );
}
