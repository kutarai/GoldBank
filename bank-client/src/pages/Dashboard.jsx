import { useState, useEffect } from 'react';
import { Grid, Card, CardContent, Typography, Box } from '@mui/material';
import { People, Receipt, TrendingUp, AttachMoney, Storefront, SupportAgent, PointOfSale } from '@mui/icons-material';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
import { generateDashboardMetrics } from '../services/api';

const fmt = (n) => n >= 1_000_000 ? `${(n / 1_000_000).toFixed(1)}M` : n >= 1_000 ? `${(n / 1_000).toFixed(1)}K` : String(n);

function StatCard({ icon, label, value, color }) {
  return (
    <Card>
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
        <Box sx={{ bgcolor: `${color}.light`, borderRadius: 2, p: 1.5, display: 'flex' }}>{icon}</Box>
        <Box>
          <Typography variant="body2" color="text.secondary">{label}</Typography>
          <Typography variant="h5" fontWeight="bold">{value}</Typography>
        </Box>
      </CardContent>
    </Card>
  );
}

export default function Dashboard() {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    generateDashboardMetrics().then(setData).finally(() => setLoading(false));
  }, []);

  if (loading || !data) return null;

  const stats = [
    { icon: <People color="primary" />, label: 'Total Users', value: fmt(data.totalUsers), color: 'primary' },
    { icon: <People color="success" />, label: 'Active Users', value: fmt(data.activeUsers), color: 'success' },
    { icon: <Receipt color="info" />, label: 'Transactions', value: fmt(data.totalTransactions), color: 'info' },
    { icon: <TrendingUp color="warning" />, label: 'Volume (ZWG)', value: fmt(data.transactionVolume), color: 'warning' },
    { icon: <AttachMoney color="success" />, label: 'Revenue (ZWG)', value: fmt(data.revenue), color: 'success' },
    { icon: <Storefront color="secondary" />, label: 'Merchants', value: fmt(data.merchants), color: 'secondary' },
    { icon: <SupportAgent color="info" />, label: 'Agents', value: fmt(data.agents), color: 'info' },
    { icon: <PointOfSale color="warning" />, label: 'Terminals', value: fmt(data.terminals), color: 'warning' },
  ];

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Dashboard</Typography>
      <Grid container spacing={2} sx={{ mb: 3 }}>
        {stats.map((s) => (
          <Grid size={{ xs: 12, sm: 6, md: 3 }} key={s.label}>
            <StatCard {...s} />
          </Grid>
        ))}
      </Grid>
      <Card>
        <CardContent>
          <Typography variant="h6" gutterBottom>Daily Transactions (Last 30 Days)</Typography>
          <ResponsiveContainer width="100%" height={300}>
            <LineChart data={data.dailyTransactions}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" tick={{ fontSize: 12 }} />
              <YAxis />
              <Tooltip />
              <Line type="monotone" dataKey="count" stroke="#1976d2" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </CardContent>
      </Card>
    </Box>
  );
}
