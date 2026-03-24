import { Box, Typography, Button } from '@mui/material';
import { useNavigate } from 'react-router-dom';

export default function NotFound() {
  const navigate = useNavigate();
  return (
    <Box sx={{ textAlign: 'center', mt: 10 }}>
      <Typography variant="h2" gutterBottom>404</Typography>
      <Typography variant="h6" color="text.secondary" gutterBottom>Page not found</Typography>
      <Button variant="contained" onClick={() => navigate('/')}>Back to Dashboard</Button>
    </Box>
  );
}
