import { useState, useEffect } from 'react';
import {
  Box, Typography, TextField, MenuItem, Button, Chip, IconButton, Menu, MenuItem as MItem,
  Dialog, DialogTitle, DialogContent, DialogActions, Grid, Divider, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, TablePagination, Tabs, Tab,
} from '@mui/material';
import { MoreVert, Search } from '@mui/icons-material';
import { generateCustomers, generateTransactions } from '../services/api';
import { useSnackbar } from '../services/snackbar';

const STATUS_COLORS = { Active: 'success', Suspended: 'warning', Frozen: 'info', Closed: 'error' };

const ASSET_STATUS_COLORS = { 'In Custody': 'success', 'Pending Verification': 'warning', 'Released': 'default', 'Disputed': 'error' };

const STUB_ASSETS = [
  {
    id: 'AST001',
    type: 'Gold Coin',
    description: 'Krugerrand Gold Coins (1 oz)',
    quantity: 5,
    depositHouse: 'Fidelity Gold Refinery',
    status: 'In Custody',
    currentValue: 9500,
    lastVerified: '2026-03-10',
  },
  {
    id: 'AST002',
    type: 'Gold Coin',
    description: 'Mosi-oa-Tunya Gold Coins (1 oz)',
    quantity: 10,
    depositHouse: 'ZB Bank Vault Services',
    status: 'In Custody',
    currentValue: 18200,
    lastVerified: '2026-03-15',
  },
  {
    id: 'AST003',
    type: 'Silver Bar',
    description: 'LBMA Silver Bar (100 oz)',
    quantity: 2,
    depositHouse: 'Stanbic Precious Metals',
    status: 'Pending Verification',
    currentValue: 4800,
    lastVerified: '2026-02-28',
  },
];

const TOTAL_PORTFOLIO_VALUE = STUB_ASSETS.reduce((sum, a) => sum + a.currentValue, 0);

export default function Customers() {
  const notify = useSnackbar();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [page, setPage] = useState(0);
  const [data, setData] = useState({ items: [], total: 0 });
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState(null);
  const [detailTab, setDetailTab] = useState(0);
  const [actionOpen, setActionOpen] = useState(false);
  const [action, setAction] = useState('');
  const [reason, setReason] = useState('');
  const [anchorEl, setAnchorEl] = useState(null);
  const [menuCustomer, setMenuCustomer] = useState(null);

  const load = () => setData(generateCustomers(page, 10, search, statusFilter));
  useEffect(load, [page, search, statusFilter]);

  const handleAction = () => {
    notify(`${action} executed on ${selected?.name}`);
    setActionOpen(false);
    setReason('');
  };

  const openMenu = (e, customer) => { setAnchorEl(e.currentTarget); setMenuCustomer(customer); };
  const closeMenu = () => { setAnchorEl(null); setMenuCustomer(null); };

  const startAction = (act) => {
    setSelected(menuCustomer);
    setAction(act);
    closeMenu();
    if (act === 'View') {
      setDetailTab(0);
      setDetailOpen(true);
    } else {
      setActionOpen(true);
    }
  };

  const recentTxns = generateTransactions().slice(0, 5);

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Customers</Typography>
      <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
        <TextField size="small" placeholder="Search name or phone..." value={search} onChange={(e) => { setSearch(e.target.value); setPage(0); }}
          slotProps={{ input: { startAdornment: <Search sx={{ mr: 1 }} /> } }} sx={{ minWidth: 280 }} />
        <TextField select size="small" label="Status" value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(0); }} sx={{ minWidth: 140 }}>
          <MenuItem value="">All</MenuItem>
          {['Active', 'Suspended', 'Frozen', 'Closed'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell><TableCell>Phone</TableCell><TableCell>Status</TableCell>
              <TableCell>KYC</TableCell><TableCell align="right">Balance (ZWG)</TableCell>
              <TableCell>Created</TableCell><TableCell>Last Login</TableCell><TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {data.items.map((c) => (
              <TableRow key={c.id} hover>
                <TableCell>{c.name}</TableCell>
                <TableCell>{c.phone}</TableCell>
                <TableCell><Chip label={c.status} color={STATUS_COLORS[c.status]} size="small" /></TableCell>
                <TableCell>Level {c.kycLevel}</TableCell>
                <TableCell align="right">{c.balanceZwg.toLocaleString()}</TableCell>
                <TableCell>{c.created}</TableCell>
                <TableCell>{c.lastLogin}</TableCell>
                <TableCell>
                  <IconButton size="small" onClick={(e) => openMenu(e, c)}><MoreVert /></IconButton>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <TablePagination component="div" count={data.total} page={page} rowsPerPage={10} onPageChange={(_, p) => setPage(p)} rowsPerPageOptions={[10]} />
      </TableContainer>

      <Menu anchorEl={anchorEl} open={Boolean(anchorEl)} onClose={closeMenu}>
        {['View', 'Activate', 'Suspend', 'Freeze', 'Unfreeze', 'Reset PIN', 'Close'].map((a) => (
          <MItem key={a} onClick={() => startAction(a)}>{a}</MItem>
        ))}
      </Menu>

      {/* Detail Dialog */}
      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth>
        <DialogTitle>Customer Details: {selected?.name}</DialogTitle>
        <DialogContent sx={{ px: 0, pb: 0 }}>
          <Tabs
            value={detailTab}
            onChange={(_, v) => setDetailTab(v)}
            sx={{ borderBottom: 1, borderColor: 'divider', px: 3 }}
          >
            <Tab label="Profile" />
            <Tab label="Transactions" />
            <Tab label="Assets" />
          </Tabs>

          {selected && (
            <Box sx={{ px: 3, pt: 2, pb: 1 }}>
              {/* Tab 0: Profile */}
              {detailTab === 0 && (
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, md: 6 }}>
                    <Typography variant="subtitle2" color="text.secondary">Account ID</Typography>
                    <Typography>{selected.id}</Typography>
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Phone</Typography>
                    <Typography>{selected.phone}</Typography>
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Email</Typography>
                    <Typography>{selected.email}</Typography>
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>National ID</Typography>
                    <Typography>{selected.nationalId}</Typography>
                  </Grid>
                  <Grid size={{ xs: 12, md: 6 }}>
                    <Typography variant="subtitle2" color="text.secondary">Status</Typography>
                    <Chip label={selected.status} color={STATUS_COLORS[selected.status]} size="small" />
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>KYC Level</Typography>
                    <Typography>Level {selected.kycLevel}</Typography>
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Balance (ZWG)</Typography>
                    <Typography variant="h6">{selected.balanceZwg.toLocaleString()}</Typography>
                    <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 1 }}>Balance (USD)</Typography>
                    <Typography variant="h6">${selected.balanceUsd.toLocaleString()}</Typography>
                  </Grid>
                </Grid>
              )}

              {/* Tab 1: Transactions */}
              {detailTab === 1 && (
                <Box>
                  <Typography variant="subtitle1" gutterBottom>Recent Transactions</Typography>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>ID</TableCell><TableCell>Type</TableCell>
                        <TableCell align="right">Amount</TableCell><TableCell>Status</TableCell><TableCell>Date</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {recentTxns.map((t) => (
                        <TableRow key={t.id}>
                          <TableCell>{t.id}</TableCell><TableCell>{t.type}</TableCell>
                          <TableCell align="right">{t.amount.toLocaleString()}</TableCell>
                          <TableCell><Chip label={t.status} size="small" /></TableCell>
                          <TableCell>{t.date}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}

              {/* Tab 2: Assets */}
              {detailTab === 2 && (
                <Box>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
                    <Typography variant="subtitle1">Assets in Trust</Typography>
                    <Box sx={{ textAlign: 'right' }}>
                      <Typography variant="caption" color="text.secondary">Total Portfolio Value</Typography>
                      <Typography variant="h6" color="success.main">
                        ${TOTAL_PORTFOLIO_VALUE.toLocaleString()}
                      </Typography>
                    </Box>
                  </Box>
                  <Divider sx={{ mb: 2 }} />
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Asset Type</TableCell>
                        <TableCell>Description</TableCell>
                        <TableCell align="right">Qty</TableCell>
                        <TableCell>Deposit House</TableCell>
                        <TableCell>Status</TableCell>
                        <TableCell align="right">Current Value</TableCell>
                        <TableCell>Last Verified</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {STUB_ASSETS.map((a) => (
                        <TableRow key={a.id}>
                          <TableCell>
                            <Chip label={a.type} size="small" variant="outlined" />
                          </TableCell>
                          <TableCell>{a.description}</TableCell>
                          <TableCell align="right">{a.quantity}</TableCell>
                          <TableCell>{a.depositHouse}</TableCell>
                          <TableCell>
                            <Chip
                              label={a.status}
                              color={ASSET_STATUS_COLORS[a.status]}
                              size="small"
                            />
                          </TableCell>
                          <TableCell align="right">
                            <Typography variant="body2" fontWeight="medium">
                              ${a.currentValue.toLocaleString()}
                            </Typography>
                          </TableCell>
                          <TableCell>{a.lastVerified}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDetailOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>

      {/* Action Confirm Dialog */}
      <Dialog open={actionOpen} onClose={() => setActionOpen(false)}>
        <DialogTitle>{action} Account</DialogTitle>
        <DialogContent>
          <Typography sx={{ mb: 2 }}>Confirm <strong>{action}</strong> on {selected?.name} ({selected?.id})?</Typography>
          <TextField label="Reason" fullWidth required multiline rows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setActionOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleAction} disabled={!reason}>Confirm</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
