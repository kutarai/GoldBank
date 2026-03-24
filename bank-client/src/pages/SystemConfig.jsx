import { useState } from 'react';
import { Box, Typography, TextField, Button, Card, CardContent } from '@mui/material';
import { useSnackbar } from '../services/snackbar';

export default function SystemConfig() {
  const notify = useSnackbar();
  const [key, setKey] = useState('');
  const [value, setValue] = useState('');
  const [tenantId, setTenantId] = useState('');

  const handleSubmit = (e) => {
    e.preventDefault();
    notify(`Config "${key}" updated`);
    setKey('');
    setValue('');
    setTenantId('');
  };

  return (
    <Box>
      <Typography variant="h5" gutterBottom>System Configuration</Typography>
      <Card sx={{ maxWidth: 500 }}>
        <CardContent>
          <form onSubmit={handleSubmit}>
            <TextField fullWidth label="Key" margin="normal" value={key} onChange={(e) => setKey(e.target.value)} required />
            <TextField fullWidth label="Value (JSON)" margin="normal" multiline rows={4} value={value} onChange={(e) => setValue(e.target.value)} required />
            <TextField fullWidth label="Tenant ID (optional)" margin="normal" value={tenantId} onChange={(e) => setTenantId(e.target.value)} />
            <Button type="submit" variant="contained" sx={{ mt: 1 }}>Update Config</Button>
          </form>
        </CardContent>
      </Card>
    </Box>
  );
}
