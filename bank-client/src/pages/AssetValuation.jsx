import { useEffect, useState, useMemo } from 'react';
import {
  Box, Typography, Card, CardContent, Grid, Chip, Button, TextField, MenuItem,
  Dialog, DialogTitle, DialogContent, DialogActions, LinearProgress,
  Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Paper,
  Tab, Tabs, IconButton, Tooltip, Divider,
} from '@mui/material';
import { DataGrid } from '@mui/x-data-grid';
import {
  AssignmentInd, UploadFile, Diamond, CheckCircle, Schedule, Warning,
} from '@mui/icons-material';
import { useSnackbar } from '../services/snackbar';
import { generateAssets, generateAssetValuations, submitAssetValuation } from '../services/api';

// ── Static lookups ───────────────────────────────────────────────────────────

const ASSET_TYPES = ['All', 'Gold Coin', 'Gold Bar', 'Silver Bar', 'Silver Coin', 'Platinum Bar', 'Precious Stone', 'Other'];
const STATUSES    = ['All', 'Active', 'Inactive', 'Pending Review'];
const CURRENCIES  = ['USD', 'ZWG'];
const VALUERS     = ['Mr. F. Chikwanda', 'Mrs. T. Ndhlovu', 'Mr. S. Mlambo', 'Ms. P. Zimba'];

const TODAY = new Date();

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
  'Precious Stone': 'secondary',
  'Other': 'default',
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

// ── Stats Cards ──────────────────────────────────────────────────────────────

function StatsCards({ assets, valuations }) {
  const totalAssets = assets.length;
  const totalValue = assets.reduce((s, a) => s + Number(a.currentValue || 0), 0);
  const pendingValuations = assets.filter((a) => daysOverdue(a.lastValued) > 0).length;
  const ym = TODAY.toISOString().slice(0, 7);
  const valuationsThisMonth = valuations.filter((v) => v.date && v.date.startsWith(ym)).length;

  const stats = [
    { label: 'Total Assets Under Custody', value: totalAssets, color: 'primary.main', suffix: ' assets' },
    { label: 'Total Portfolio Value (USD)', value: `$${totalValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`, color: 'success.main', suffix: '' },
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

// ── Asset Detail Dialog ──────────────────────────────────────────────────────

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
              ['Quantity', `${asset.quantity}${asset.unit ? ' ' + asset.unit : ''}`],
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

// ── Assign Valuer Dialog ─────────────────────────────────────────────────────

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

// ── Valuation Submit Dialog ──────────────────────────────────────────────────

function ValuationDialog({ asset, onClose, onSubmit }) {
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('USD');
  const [valuerName, setValuerName] = useState(asset?.assignedValuer || '');
  const [licenseNo, setLicenseNo] = useState('');
  const [notes, setNotes] = useState('');

  const handleSubmit = () => {
    onSubmit({
      assetId:   asset.id,         // short id, used in toast messages
      assetUuid: asset.assetUuid,  // server-side primary key for the POST
      amount:    parseFloat(amount),
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

// ── Tab 0: Asset Registry ────────────────────────────────────────────────────

function AssetRegistry({ assets }) {
  const [typeFilter, setTypeFilter] = useState('All');
  const [statusFilter, setStatusFilter] = useState('All');
  const [detailAsset, setDetailAsset] = useState(null);

  const filtered = useMemo(() => assets.filter((a) => {
    if (typeFilter !== 'All' && a.type !== typeFilter) return false;
    if (statusFilter !== 'All' && a.status !== statusFilter) return false;
    return true;
  }), [assets, typeFilter, statusFilter]);

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

// ── Tab 1: Valuation Queue ───────────────────────────────────────────────────

function ValuationQueue({ assets, onValuationSubmitted }) {
  const notify = useSnackbar();
  const [assignTarget, setAssignTarget] = useState(null);
  const [valuationTarget, setValuationTarget] = useState(null);
  const [assignOverrides, setAssignOverrides] = useState({});

  const queueAssets = useMemo(() =>
    assets
      .filter((a) => a.status !== 'Inactive' && daysOverdue(a.lastValued) > 0)
      .map((a) => ({
        ...a,
        ...(assignOverrides[a.id] || {}),
        daysOverdue: daysOverdue(a.lastValued),
      }))
      .sort((a, b) => b.daysOverdue - a.daysOverdue),
    [assets, assignOverrides],
  );

  const handleAssign = (assetId, valuer, scheduledDate) => {
    // Assignment isn't persisted server-side yet — keep it in-memory only.
    setAssignOverrides((prev) => ({
      ...prev,
      [assetId]: { ...prev[assetId], assignedValuer: valuer, scheduledDate },
    }));
    notify(`Valuer ${valuer} assigned to ${assetId}`, 'success');
  };

  const handleValuationSubmit = async (data) => {
    const result = await submitAssetValuation({
      assetId:    data.assetUuid,
      amount:     data.amount,
      currency:   data.currency,
      valuerName: data.valuerName,
      licenseNo:  data.licenseNo,
      notes:      data.notes,
    });
    if (result) {
      notify(`Valuation submitted for ${data.assetId} (${data.currency} ${data.amount.toLocaleString()})`, 'success');
      onValuationSubmitted?.();
    } else {
      notify(`Failed to submit valuation for ${data.assetId}`, 'error');
    }
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

// ── Tab 2: Valuation History ─────────────────────────────────────────────────

function ValuationHistory({ valuations }) {
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
        <Typography variant="body2">{row.prevValue != null ? fmt(row.prevValue, row.currency) : '—'}</Typography>
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

  if (valuations.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 6 }}>
        <Typography variant="body2" color="text.secondary">
          No valuations recorded yet. Submit one from the Valuation Queue tab.
        </Typography>
      </Box>
    );
  }

  return (
    <Paper sx={{ height: 480 }}>
      <DataGrid
        rows={valuations}
        columns={columns}
        pageSizeOptions={[5, 10, 25]}
        initialState={{ pagination: { paginationModel: { pageSize: 10 } } }}
        disableRowSelectionOnClick
        getRowId={(r) => r.id}
      />
    </Paper>
  );
}

// ── Main Page ────────────────────────────────────────────────────────────────

export default function AssetValuation() {
  const [tab, setTab] = useState(0);
  const [assets, setAssets] = useState([]);
  const [valuations, setValuations] = useState([]);
  const [loading, setLoading] = useState(true);

  const reload = () => {
    setLoading(true);
    Promise.all([generateAssets(), generateAssetValuations()])
      .then(([a, v]) => {
        setAssets(a);
        setValuations(v);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => { reload(); }, []);

  return (
    <Box>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
        <Diamond color="warning" />
        <Typography variant="h5">Asset Custody &amp; Valuation</Typography>
      </Box>

      {loading && <LinearProgress sx={{ mb: 2 }} />}

      <StatsCards assets={assets} valuations={valuations} />

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 3 }}>
        <Tab label="Asset Registry" />
        <Tab label="Valuation Queue" />
        <Tab label="Valuation History" />
      </Tabs>

      {tab === 0 && <AssetRegistry assets={assets} />}
      {tab === 1 && <ValuationQueue assets={assets} onValuationSubmitted={reload} />}
      {tab === 2 && <ValuationHistory valuations={valuations} />}
    </Box>
  );
}
