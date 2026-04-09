import { useState, useMemo } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  Tab, Tabs, IconButton, Tooltip, Divider,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import {
  AssignmentInd, UploadFile, Diamond, CheckCircle, Schedule, Warning,
} from '@mui/icons-material';
import { useSnackbar } from '../services/snackbar';

// ── Stub Data ─────────────────────────────────────────────────────────────────

const ASSETS = [
  {
    id: 'AST-001',
    customer: 'Tendai Moyo',
    type: 'Gold Coin',
    description: 'Krugerrand 1 oz gold coins (2019 mint)',
    quantity: 10,
    depositHouse: 'Harare Vault Centre',
    currentValue: 19850,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2024-06-12',
    lastValued: '2026-01-15',
    assignedValuer: 'Mr. F. Chikwanda',
  },
  {
    id: 'AST-002',
    customer: 'Chiedza Sithole',
    type: 'Gold Coin',
    description: 'Mosi-oa-Tunya 1 oz gold coins (RBZ 2022)',
    quantity: 5,
    depositHouse: 'Bulawayo Secure Vault',
    currentValue: 9925,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2024-09-03',
    lastValued: '2026-02-01',
    assignedValuer: 'Mrs. T. Ndhlovu',
  },
  {
    id: 'AST-003',
    customer: 'Farai Mutasa',
    type: 'Gold Bar',
    description: 'LBMA-certified 100g gold bar (Rand Refinery)',
    quantity: 2,
    depositHouse: 'Harare Vault Centre',
    currentValue: 12800,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2024-11-20',
    lastValued: '2025-12-10',
    assignedValuer: 'Mr. F. Chikwanda',
  },
  {
    id: 'AST-004',
    customer: 'Rudo Zvarevashe',
    type: 'Silver Bar',
    description: 'Cast silver bars 1 kg each (Johnson Matthey)',
    quantity: 20,
    depositHouse: 'Mutare Depository',
    currentValue: 7600,
    currency: 'USD',
    status: 'Active',
    verification: 'Partial',
    registeredDate: '2025-01-08',
    lastValued: null,
    assignedValuer: null,
  },
  {
    id: 'AST-005',
    customer: 'Blessing Chirwa',
    type: 'Gold Bar',
    description: 'LBMA 400 oz good delivery gold bar',
    quantity: 1,
    depositHouse: 'Harare Vault Centre',
    currentValue: 780000,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2025-02-14',
    lastValued: '2025-10-30',
    assignedValuer: 'Mrs. T. Ndhlovu',
  },
  {
    id: 'AST-006',
    customer: 'Ngonidzashe Banda',
    type: 'Silver Coin',
    description: 'Zimbabwe silver Pangolin commemorative coins',
    quantity: 50,
    depositHouse: 'Bulawayo Secure Vault',
    currentValue: 3200,
    currency: 'USD',
    status: 'Pending Review',
    verification: 'Not Verified',
    registeredDate: '2025-03-22',
    lastValued: null,
    assignedValuer: null,
  },
  {
    id: 'AST-007',
    customer: 'Tafadzwa Gumbo',
    type: 'Gold Coin',
    description: 'Krugerrand 1/2 oz proof gold coins (2021 mint)',
    quantity: 8,
    depositHouse: 'Masvingo Custodian Hub',
    currentValue: 7640,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2025-04-05',
    lastValued: '2025-11-20',
    assignedValuer: 'Mr. S. Mlambo',
  },
  {
    id: 'AST-008',
    customer: 'Rumbidzai Choto',
    type: 'Platinum Bar',
    description: 'Platinum ingot 50g (certified assay card)',
    quantity: 4,
    depositHouse: 'Harare Vault Centre',
    currentValue: 5900,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2025-05-17',
    lastValued: '2026-01-05',
    assignedValuer: 'Mr. F. Chikwanda',
  },
  {
    id: 'AST-009',
    customer: 'Munyaradzi Dube',
    type: 'Gold Bar',
    description: 'LBMA 1 kg gold cast bars (Credit Suisse)',
    quantity: 3,
    depositHouse: 'Bulawayo Secure Vault',
    currentValue: 288000,
    currency: 'USD',
    status: 'Inactive',
    verification: 'Partial',
    registeredDate: '2025-06-30',
    lastValued: '2025-09-14',
    assignedValuer: null,
  },
  {
    id: 'AST-010',
    customer: 'Chipo Mandizha',
    type: 'Silver Bar',
    description: 'Metalor 500g silver minted bars',
    quantity: 12,
    depositHouse: 'Mutare Depository',
    currentValue: 4320,
    currency: 'USD',
    status: 'Active',
    verification: 'Verified',
    registeredDate: '2025-08-11',
    lastValued: '2026-02-18',
    assignedValuer: 'Mrs. T. Ndhlovu',
  },
];

const VALUATION_HISTORY = [
  { id: 'VAL-001', date: '2026-02-18', asset: 'AST-010', customer: 'Chipo Mandizha', description: 'Metalor 500g silver minted bars', valuer: 'Mrs. T. Ndhlovu', licenseNo: 'ZW-VAL-0047', prevValue: 4100, newValue: 4320, currency: 'USD', notes: 'Spot price increase. All bars verified intact.' },
  { id: 'VAL-002', date: '2026-02-01', asset: 'AST-002', customer: 'Chiedza Sithole', description: 'Mosi-oa-Tunya 1 oz gold coins', valuer: 'Mrs. T. Ndhlovu', licenseNo: 'ZW-VAL-0047', prevValue: 9450, newValue: 9925, currency: 'USD', notes: 'Gold spot at $1985/oz. Coins in excellent condition.' },
  { id: 'VAL-003', date: '2026-01-15', asset: 'AST-001', customer: 'Tendai Moyo', description: 'Krugerrand 1 oz gold coins', valuer: 'Mr. F. Chikwanda', licenseNo: 'ZW-VAL-0019', prevValue: 18700, newValue: 19850, currency: 'USD', notes: 'Annual revaluation. Coins stored in certified capsules.' },
  { id: 'VAL-004', date: '2026-01-05', asset: 'AST-008', customer: 'Rumbidzai Choto', description: 'Platinum ingot 50g', valuer: 'Mr. F. Chikwanda', licenseNo: 'ZW-VAL-0019', prevValue: 6100, newValue: 5900, currency: 'USD', notes: 'Platinum spot declined. Weight confirmed by digital assay scale.' },
  { id: 'VAL-005', date: '2025-12-10', asset: 'AST-003', customer: 'Farai Mutasa', description: 'LBMA 100g gold bar (Rand Refinery)', valuer: 'Mr. F. Chikwanda', licenseNo: 'ZW-VAL-0019', prevValue: 11900, newValue: 12800, currency: 'USD', notes: 'LBMA serial confirmed. Assay certificate on file.' },
  { id: 'VAL-006', date: '2025-11-20', asset: 'AST-007', customer: 'Tafadzwa Gumbo', description: 'Krugerrand 1/2 oz proof coins', valuer: 'Mr. S. Mlambo', licenseNo: 'ZW-VAL-0033', prevValue: 7100, newValue: 7640, currency: 'USD', notes: 'Proof condition. Original boxes and certificates present.' },
  { id: 'VAL-007', date: '2025-10-30', asset: 'AST-005', customer: 'Blessing Chirwa', description: 'LBMA 400 oz good delivery bar', valuer: 'Mrs. T. Ndhlovu', licenseNo: 'ZW-VAL-0047', prevValue: 756000, newValue: 780000, currency: 'USD', notes: 'Good delivery bar inspected per LBMA standards.' },
  { id: 'VAL-008', date: '2025-09-14', asset: 'AST-009', customer: 'Munyaradzi Dube', description: 'LBMA 1 kg gold cast bars', valuer: 'Mr. S. Mlambo', licenseNo: 'ZW-VAL-0033', prevValue: 296000, newValue: 288000, currency: 'USD', notes: 'Spot price adjustment. No physical damage observed.' },
];

// ── Helpers ───────────────────────────────────────────────────────────────────

const ASSET_TYPES = ['All', 'Gold Coin', 'Gold Bar', 'Silver Bar', 'Silver Coin', 'Platinum Bar'];
const STATUSES = ['All', 'Active', 'Inactive', 'Pending Review'];
const CURRENCIES = ['USD', 'ZWG'];
const VALUERS = ['Mr. F. Chikwanda', 'Mrs. T. Ndhlovu', 'Mr. S. Mlambo', 'Ms. P. Zimba'];

const TODAY = new Date('2026-03-24');

function daysSince(dateStr) {
  if (!dateStr) return null;
  const d = new Date(dateStr);
  return Math.floor((TODAY - d) / (1000 * 60 * 60 * 24));
}

function daysOverdue(dateStr) {
  const days = daysSince(dateStr);
  if (days === null) return 9999;
  return Math.max(0, days - 30);
}

function fmt(val, currency = 'USD') {
  return `${currency} ${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
}

function pctChange(prev, next) {
  if (!prev) return null;
  return (((next - prev) / prev) * 100).toFixed(1);
}

const TYPE_COLORS = {
  'Gold Coin': 'warning',
  'Gold Bar': 'warning',
  'Silver Bar': 'default',
  'Silver Coin': 'default',
  'Platinum Bar': 'info',
};

const STATUS_COLORS = {
  Active: 'success',
  Inactive: 'default',
  'Pending Review': 'warning',
};

const VERIFY_COLORS = {
  Verified: 'success',
  Partial: 'warning',
  'Not Verified': 'error',
};

// ── Stats Cards ───────────────────────────────────────────────────────────────

function StatsCards() {
  const totalAssets = ASSETS.length;
  const totalValue = ASSETS.reduce((s, a) => s + a.currentValue, 0);
  const pendingValuations = ASSETS.filter((a) => daysOverdue(a.lastValued) > 0).length;
  const valuationsThisMonth = VALUATION_HISTORY.filter((v) => v.date.startsWith('2026-02') || v.date.startsWith('2026-03')).length;

  const stats = [
    { label: 'Total Assets Under Custody', value: totalAssets, color: 'primary.main', suffix: ' assets' },
    { label: 'Total Portfolio Value (USD)', value: `$${totalValue.toLocaleString()}`, color: 'success.main', suffix: '' },
    { label: 'Pending Valuations', value: pendingValuations, color: 'warning.main', suffix: '' },
    { label: 'Valuations This Month', value: valuationsThisMonth, color: 'info.main', suffix: '' },
  ];

  return (
    <Grid container spacing={2} sx={{ mb: 3 }}>
      {stats.map(({ label, value, color, suffix }) => (
        <Grid size={{ xs: 12, sm: 6, md: 3 }} key={label}>
          <Card>
            <CardContent>
              <Typography variant="body2" color="text.secondary">{label}</Typography>
              <Typography variant="h4" sx={{ color, fontWeight: 700 }}>
                {value}{suffix}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
      ))}
    </Grid>
  );
}

// ── Asset Detail Dialog ───────────────────────────────────────────────────────

function AssetDetailDialog({ asset, onClose }) {
  if (!asset) return null;
  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
        <Diamond fontSize="small" color="warning" />
        Asset Detail: {asset.id}
      </DialogTitle>
      <DialogContent>
        <Table size="small">
          <TableBody>
            {[
              ['Customer', asset.customer],
              ['Registered', asset.registeredDate],
              ['Description', asset.description],
              ['Type', <Chip label={asset.type} color={TYPE_COLORS[asset.type] || 'default'} size="small" />],
              ['Quantity', asset.quantity],
              ['Deposit House', asset.depositHouse],
              ['Current Value', <Typography variant="body1" color="success.main" fontWeight="medium">{fmt(asset.currentValue, asset.currency)}</Typography>],
              ['Last Valued', asset.lastValued ? `${asset.lastValued} (${daysSince(asset.lastValued)} days ago)` : '—'],
              ['Status', <Chip label={asset.status} color={STATUS_COLORS[asset.status] || 'default'} size="small" />],
              ['Verification', <Chip label={asset.verification} color={VERIFY_COLORS[asset.verification] || 'default'} size="small" />],
              ...(asset.assignedValuer ? [['Assigned Valuer', asset.assignedValuer]] : []),
            ].map(([label, value]) => (
              <TableRow key={label}>
                <TableCell sx={{ width: '35%', color: 'text.secondary', borderBottom: 'none', py: 1 }}>{label}</TableCell>
                <TableCell sx={{ borderBottom: 'none', py: 1 }}>{value}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Assign Valuer Dialog ──────────────────────────────────────────────────────

function AssignValuerDialog({ asset, onClose, onAssign }) {
  const [valuer, setValuer] = useState(asset?.assignedValuer || '');
  const [scheduledDate, setScheduledDate] = useState('');

  const handleSubmit = () => {
    onAssign(asset.id, valuer, scheduledDate);
    onClose();
  };

  if (!asset) return null;
  return (
    <Dialog open onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Assign Valuer — {asset.id}</DialogTitle>
      <DialogContent>
        <Grid container spacing={2} sx={{ mt: 0.5 }}>
          <Grid size={12}>
            <Typography variant="body2" color="text.secondary" gutterBottom>
              {asset.customer} — {asset.description}
            </Typography>
          </Grid>
          <Grid size={12}>
            <TextField
              select
              fullWidth
              label="Valuer"
              value={valuer}
              onChange={(e) => setValuer(e.target.value)}
            >
              {VALUERS.map((v) => <MenuItem key={v} value={v}>{v}</MenuItem>)}
            </TextField>
          </Grid>
          <Grid size={12}>
            <TextField
              fullWidth
              label="Scheduled Date"
              type="date"
              InputLabelProps={{ shrink: true }}
              value={scheduledDate}
              onChange={(e) => setScheduledDate(e.target.value)}
            />
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={!valuer || !scheduledDate}>
          Assign
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Valuation Submit Dialog ───────────────────────────────────────────────────

function ValuationDialog({ asset, onClose, onSubmit }) {
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('USD');
  const [valuerName, setValuerName] = useState(asset?.assignedValuer || '');
  const [licenseNo, setLicenseNo] = useState('');
  const [notes, setNotes] = useState('');

  const handleSubmit = () => {
    onSubmit({
      assetId: asset.id,
      amount: parseFloat(amount),
      currency,
      valuerName,
      licenseNo,
      notes,
    });
    onClose();
  };

  const canSubmit = amount && parseFloat(amount) > 0 && valuerName && licenseNo;

  if (!asset) return null;
  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Submit Valuation — {asset.id}</DialogTitle>
      <DialogContent>
        <Grid container spacing={2} sx={{ mt: 0.5 }}>
          {/* Asset Info (read-only) */}
          <Grid size={12}>
            <Typography variant="subtitle2" color="text.secondary" gutterBottom>Asset Information</Typography>
            <Paper variant="outlined" sx={{ p: 1.5, bgcolor: 'action.hover' }}>
              <Grid container spacing={1}>
                <Grid size={6}>
                  <Typography variant="caption" color="text.secondary">Type</Typography>
                  <Typography variant="body2">{asset.type}</Typography>
                </Grid>
                <Grid size={6}>
                  <Typography variant="caption" color="text.secondary">Quantity</Typography>
                  <Typography variant="body2">{asset.quantity}</Typography>
                </Grid>
                <Grid size={12}>
                  <Typography variant="caption" color="text.secondary">Description</Typography>
                  <Typography variant="body2">{asset.description}</Typography>
                </Grid>
                <Grid size={12}>
                  <Typography variant="caption" color="text.secondary">Deposit House</Typography>
                  <Typography variant="body2">{asset.depositHouse}</Typography>
                </Grid>
                <Grid size={6}>
                  <Typography variant="caption" color="text.secondary">Previous Value</Typography>
                  <Typography variant="body2">{asset.currentValue ? fmt(asset.currentValue, asset.currency) : '—'}</Typography>
                </Grid>
                <Grid size={6}>
                  <Typography variant="caption" color="text.secondary">Last Valued</Typography>
                  <Typography variant="body2">{asset.lastValued || 'Never'}</Typography>
                </Grid>
              </Grid>
            </Paper>
          </Grid>

          <Grid size={12}><Divider /></Grid>

          {/* Valuation Amount */}
          <Grid size={8}>
            <TextField
              fullWidth
              label="Valuation Amount"
              type="number"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              inputProps={{ min: 0, step: 0.01 }}
              required
            />
          </Grid>
          <Grid size={4}>
            <TextField
              select
              fullWidth
              label="Currency"
              value={currency}
              onChange={(e) => setCurrency(e.target.value)}
            >
              {CURRENCIES.map((c) => <MenuItem key={c} value={c}>{c}</MenuItem>)}
            </TextField>
          </Grid>

          {/* Valuer Details */}
          <Grid size={12}>
            <TextField
              fullWidth
              label="Valuer Name"
              value={valuerName}
              onChange={(e) => setValuerName(e.target.value)}
              required
            />
          </Grid>
          <Grid size={12}>
            <TextField
              fullWidth
              label="Valuer License Number"
              placeholder="e.g. ZW-VAL-0019"
              value={licenseNo}
              onChange={(e) => setLicenseNo(e.target.value)}
              required
            />
          </Grid>
          <Grid size={12}>
            <TextField
              fullWidth
              label="Notes"
              multiline
              rows={3}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Physical condition, spot price reference, observations…"
            />
          </Grid>
          <Grid size={12}>
            <Button
              variant="outlined"
              startIcon={<UploadFile />}
              fullWidth
              onClick={() => {}}
            >
              Upload Valuation Report (PDF)
            </Button>
            <Typography variant="caption" color="text.secondary">
              Report upload not yet connected to storage backend.
            </Typography>
          </Grid>
        </Grid>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={!canSubmit}>
          Submit Valuation
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ── Tab 0: Asset Registry ─────────────────────────────────────────────────────

function AssetRegistry() {
  const [typeFilter, setTypeFilter] = useState('All');
  const [statusFilter, setStatusFilter] = useState('All');
  const [detailAsset, setDetailAsset] = useState(null);

  const filtered = useMemo(() => ASSETS.filter((a) => {
    if (typeFilter !== 'All' && a.type !== typeFilter) return false;
    if (statusFilter !== 'All' && a.status !== statusFilter) return false;
    return true;
  }), [typeFilter, statusFilter]);

  const columns = [
    { field: 'customer', headerName: 'Customer Name', flex: 1.2, minWidth: 140 },
    {
      field: 'type', headerName: 'Asset Type', width: 140,
      renderCell: ({ value }) => (
        <Chip label={value} color={TYPE_COLORS[value] || 'default'} size="small" variant="outlined" />
      ),
    },
    { field: 'description', headerName: 'Description', flex: 2, minWidth: 200 },
    { field: 'quantity', headerName: 'Qty', width: 70, type: 'number' },
    { field: 'depositHouse', headerName: 'Deposit House', flex: 1.2, minWidth: 150 },
    {
      field: 'currentValue', headerName: 'Current Value', width: 150, type: 'number',
      renderCell: ({ row }) => (
        <Typography variant="body2" color="success.main" fontWeight={600}>
          {fmt(row.currentValue, row.currency)}
        </Typography>
      ),
    },
    {
      field: 'status', headerName: 'Status', width: 130,
      renderCell: ({ value }) => (
        <Chip label={value} color={STATUS_COLORS[value] || 'default'} size="small" />
      ),
    },
    {
      field: 'verification', headerName: 'Verification', width: 130,
      renderCell: ({ value }) => (
        <Chip label={value} color={VERIFY_COLORS[value] || 'default'} size="small" variant="outlined" />
      ),
    },
    { field: 'registeredDate', headerName: 'Registered', width: 110 },
  ];

  return (
    <>
      {/* Filters */}
      <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
        <TextField
          select
          size="small"
          label="Asset Type"
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value)}
          sx={{ width: 170 }}
        >
          {ASSET_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </TextField>
        <TextField
          select
          size="small"
          label="Status"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          sx={{ width: 160 }}
        >
          {STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>
        <Typography variant="body2" color="text.secondary" sx={{ alignSelf: 'center' }}>
          {filtered.length} record{filtered.length !== 1 ? 's' : ''}
        </Typography>
      </Box>

      <Paper sx={{ height: 480 }}>
        <DataGrid
          rows={filtered}
          columns={columns}
          pageSizeOptions={[5, 10, 25]}
          initialState={{ pagination: { paginationModel: { pageSize: 10 } } }}
          onRowClick={({ row }) => setDetailAsset(row)}
          sx={{ cursor: 'pointer' }}
          disableRowSelectionOnClick
        />
      </Paper>

      {detailAsset && (
        <AssetDetailDialog asset={detailAsset} onClose={() => setDetailAsset(null)} />
      )}
    </>
  );
}

// ── Tab 1: Valuation Queue ────────────────────────────────────────────────────

function ValuationQueue() {
  const notify = useSnackbar();
  const [assignTarget, setAssignTarget] = useState(null);
  const [valuationTarget, setValuationTarget] = useState(null);
  const [assetOverrides, setAssetOverrides] = useState({});

  const queueAssets = useMemo(() =>
    ASSETS
      .filter((a) => a.status !== 'Inactive' && daysOverdue(a.lastValued) > 0)
      .map((a) => ({
        ...a,
        ...(assetOverrides[a.id] || {}),
        daysOverdue: daysOverdue((assetOverrides[a.id] || a).lastValued),
      }))
      .sort((a, b) => b.daysOverdue - a.daysOverdue),
    [assetOverrides],
  );

  const handleAssign = (assetId, valuer, scheduledDate) => {
    setAssetOverrides((prev) => ({
      ...prev,
      [assetId]: { ...prev[assetId], assignedValuer: valuer, scheduledDate },
    }));
    notify(`Valuer ${valuer} assigned to ${assetId}`, 'success');
  };

  const handleValuationSubmit = (data) => {
    setAssetOverrides((prev) => ({
      ...prev,
      [data.assetId]: {
        ...prev[data.assetId],
        currentValue: data.amount,
        currency: data.currency,
        lastValued: TODAY.toISOString().slice(0, 10),
      },
    }));
    notify(`Valuation submitted for ${data.assetId}`, 'success');
  };

  const columns = [
    { field: 'customer', headerName: 'Customer', flex: 1, minWidth: 130 },
    { field: 'id', headerName: 'Asset ID', width: 100 },
    {
      field: 'type', headerName: 'Type', width: 130,
      renderCell: ({ value }) => (
        <Chip label={value} color={TYPE_COLORS[value] || 'default'} size="small" variant="outlined" />
      ),
    },
    {
      field: 'lastValued', headerName: 'Last Valued', width: 120,
      renderCell: ({ value }) => value || <Typography variant="body2" color="text.secondary">Never</Typography>,
    },
    {
      field: 'daysOverdue', headerName: 'Days Overdue', width: 120, type: 'number',
      renderCell: ({ value }) => (
        <Chip
          label={value >= 9999 ? 'Never valued' : `${value}d`}
          color={value >= 9999 ? 'error' : value > 60 ? 'error' : 'warning'}
          size="small"
          icon={<Warning fontSize="small" />}
        />
      ),
    },
    {
      field: 'assignedValuer', headerName: 'Assigned Valuer', flex: 1, minWidth: 150,
      renderCell: ({ value }) => value
        ? <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}><CheckCircle color="success" fontSize="small" /><Typography variant="body2">{value}</Typography></Box>
        : <Typography variant="body2" color="text.secondary">Unassigned</Typography>,
    },
    {
      field: 'actions', headerName: 'Actions', width: 230, sortable: false,
      renderCell: ({ row }) => (
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Tooltip title="Assign Valuer">
            <Button
              size="small"
              variant="outlined"
              startIcon={<AssignmentInd fontSize="small" />}
              onClick={() => setAssignTarget(row)}
            >
              Assign
            </Button>
          </Tooltip>
          <Tooltip title="Submit Valuation Result">
            <Button
              size="small"
              variant="contained"
              startIcon={<Schedule fontSize="small" />}
              onClick={() => setValuationTarget(row)}
            >
              Value
            </Button>
          </Tooltip>
        </Box>
      ),
    },
  ];

  return (
    <>
      {queueAssets.length === 0 ? (
        <Box sx={{ textAlign: 'center', py: 6 }}>
          <CheckCircle color="success" sx={{ fontSize: 48, mb: 1 }} />
          <Typography variant="h6" color="text.secondary">All assets are up to date</Typography>
          <Typography variant="body2" color="text.secondary">No valuations overdue.</Typography>
        </Box>
      ) : (
        <Paper sx={{ height: 480 }}>
          <DataGrid
            rows={queueAssets}
            columns={columns}
            pageSizeOptions={[5, 10]}
            initialState={{ pagination: { paginationModel: { pageSize: 10 } } }}
            disableRowSelectionOnClick
            getRowId={(r) => r.id}
          />
        </Paper>
      )}

      {assignTarget && (
        <AssignValuerDialog
          asset={assignTarget}
          onClose={() => setAssignTarget(null)}
          onAssign={handleAssign}
        />
      )}

      {valuationTarget && (
        <ValuationDialog
          asset={valuationTarget}
          onClose={() => setValuationTarget(null)}
          onSubmit={handleValuationSubmit}
        />
      )}
    </>
  );
}

// ── Tab 2: Valuation History ──────────────────────────────────────────────────

function ValuationHistory() {
  const columns = [
    { field: 'date', headerName: 'Date', width: 110 },
    { field: 'asset', headerName: 'Asset ID', width: 100 },
    {
      field: 'description', headerName: 'Asset', flex: 1.5, minWidth: 180,
      renderCell: ({ value }) => (
        <Tooltip title={value}><Typography variant="body2" noWrap>{value}</Typography></Tooltip>
      ),
    },
    { field: 'customer', headerName: 'Customer', flex: 1, minWidth: 130 },
    { field: 'valuer', headerName: 'Valuer', flex: 1, minWidth: 140 },
    {
      field: 'prevValue', headerName: 'Prev Value', width: 140, type: 'number',
      renderCell: ({ row }) => (
        <Typography variant="body2">{fmt(row.prevValue, row.currency)}</Typography>
      ),
    },
    {
      field: 'newValue', headerName: 'New Value', width: 140, type: 'number',
      renderCell: ({ row }) => (
        <Typography variant="body2" fontWeight={600} color="success.main">
          {fmt(row.newValue, row.currency)}
        </Typography>
      ),
    },
    {
      field: 'change', headerName: 'Change %', width: 110,
      renderCell: ({ row }) => {
        const pct = pctChange(row.prevValue, row.newValue);
        if (pct === null) return '—';
        const positive = parseFloat(pct) >= 0;
        return (
          <Chip
            label={`${positive ? '+' : ''}${pct}%`}
            color={positive ? 'success' : 'error'}
            size="small"
            variant="outlined"
          />
        );
      },
    },
    {
      field: 'report', headerName: 'Report', width: 100, sortable: false,
      renderCell: () => (
        <Button size="small" startIcon={<UploadFile fontSize="small" />} disabled>
          View
        </Button>
      ),
    },
  ];

  return (
    <Paper sx={{ height: 480 }}>
      <DataGrid
        rows={VALUATION_HISTORY}
        columns={columns}
        pageSizeOptions={[5, 10, 25]}
        initialState={{ pagination: { paginationModel: { pageSize: 10 } } }}
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
      />
    </Paper>
  );
}

// ── Main Page ─────────────────────────────────────────────────────────────────

export default function AssetValuation() {
  const [tab, setTab] = useState(0);

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <Diamond color="warning" />
        <Typography variant="h5">Asset Custody &amp; Valuation</Typography>
      </Box>

      <StatsCards />

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Asset Registry" />
        <Tab label="Valuation Queue" />
        <Tab label="Valuation History" />
      </Tabs>

      {tab === 0 && <AssetRegistry />}
      {tab === 1 && <ValuationQueue />}
      {tab === 2 && <ValuationHistory />}
    </Box>
  );
}
