import { useState, useMemo } from 'react';
import {
  Box, Typography, TextField, MenuItem, Grid, Chip, IconButton, Collapse,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
} from '@mui/material';
import { ExpandMore, ExpandLess } from '@mui/icons-material';
import { generateAuditTrail } from '../services/api';

export default function AuditTrail() {
  const data = useMemo(() => generateAuditTrail(), []);
  const [userFilter, setUserFilter] = useState('');
  const [actionFilter, setActionFilter] = useState('');
  const [expanded, setExpanded] = useState({});

  const users = [...new Set(data.map((d) => d.adminUser))];
  const actions = [...new Set(data.map((d) => d.action))];

  const filtered = data.filter((d) => {
    if (userFilter && d.adminUser !== userFilter) return false;
    if (actionFilter && d.action !== actionFilter) return false;
    return true;
  });

  const toggle = (id) => setExpanded((e) => ({ ...e, [id]: !e[id] }));

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Audit Trail</Typography>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 6, sm: 3 }}>
          <TextField select size="small" fullWidth label="Admin User" value={userFilter} onChange={(e) => setUserFilter(e.target.value)}>
            <MenuItem value="">All</MenuItem>
            {users.map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
          </TextField>
        </Grid>
        <Grid size={{ xs: 6, sm: 3 }}>
          <TextField select size="small" fullWidth label="Action Type" value={actionFilter} onChange={(e) => setActionFilter(e.target.value)}>
            <MenuItem value="">All</MenuItem>
            {actions.map((a) => <MenuItem key={a} value={a}>{a}</MenuItem>)}
          </TextField>
        </Grid>
      </Grid>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell width={40} /><TableCell>Admin User</TableCell><TableCell>Action</TableCell>
              <TableCell>Target</TableCell><TableCell>Timestamp</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {filtered.map((d) => (
              <>
                <TableRow key={d.id} hover>
                  <TableCell>
                    <IconButton size="small" onClick={() => toggle(d.id)}>
                      {expanded[d.id] ? <ExpandLess /> : <ExpandMore />}
                    </IconButton>
                  </TableCell>
                  <TableCell>{d.adminUser}</TableCell>
                  <TableCell><Chip label={d.action} size="small" /></TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{d.target}</TableCell>
                  <TableCell>{d.timestamp}</TableCell>
                </TableRow>
                <TableRow key={`${d.id}-detail`}>
                  <TableCell colSpan={5} sx={{ p: 0 }}>
                    <Collapse in={expanded[d.id]}>
                      <Box sx={{ p: 2, bgcolor: 'action.hover', fontFamily: 'monospace', fontSize: '0.8rem', whiteSpace: 'pre-wrap' }}>
                        {d.details}
                      </Box>
                    </Collapse>
                  </TableCell>
                </TableRow>
              </>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>{filtered.length} entries</Typography>
    </Box>
  );
}
