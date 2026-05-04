import { useState } from 'react';
import {
  Box, Card, CardContent, Typography, Button, Alert, MenuItem, TextField, Tabs, Tab, CircularProgress, Snackbar,
  Dialog, DialogTitle, DialogContent, DialogActions,
} from '@mui/material';
import PrintIcon from '@mui/icons-material/Print';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import { useNavigate } from 'react-router-dom';
import DenominationGrid, { toBreakdown, getDenominationsFor } from '../components/DenominationGrid';
import { openDrawer, closeDrawer, openEodReport } from '../services/api';
import { useTellerSession } from '../auth/TellerSessionContext';
import { useSecurityState } from '../components/SecurityShell';

export default function Drawer() {
  const navigate = useNavigate();
  const { user, drawer, refreshDrawer } = useTellerSession();
  const { online } = useSecurityState();
  const [tab, setTab] = useState(drawer ? 1 : 0);

  // Branch is bound to the teller by the admin who created the user.
  // It's pulled from the JWT/session — never entered by the teller.
  const branchId = user?.branchId;

  // Open Drawer state
  const [openCurrency, setOpenCurrency] = useState('USD');
  const [openDenoms, setOpenDenoms] = useState({});
  const [opening, setOpening] = useState(false);
  const [openErr, setOpenErr] = useState(null);

  // Close Drawer state
  const [closeCurrency, setCloseCurrency] = useState('USD');
  const [closeDenoms, setCloseDenoms] = useState({});
  const [closing, setClosing] = useState(false);
  const [closeErr, setCloseErr] = useState(null);
  const [snack, setSnack] = useState(null);

  // EOD success dialog (after a successful close)
  const [closedDrawerId, setClosedDrawerId] = useState(null);

  // Variance warning dialog (when expected != counted)
  const [variancePreview, setVariancePreview] = useState(null);

  const openTotal = getDenominationsFor(openCurrency).reduce(
    (s, d) => s + d.face * (Number(openDenoms[d.face]) || 0), 0);
  const closeTotal = getDenominationsFor(closeCurrency).reduce(
    (s, d) => s + d.face * (Number(closeDenoms[d.face]) || 0), 0);

  const handleOpen = async () => {
    if (!branchId) {
      setOpenErr('No branch is assigned to your user. Contact an administrator.');
      return;
    }
    if (openTotal <= 0) { setOpenErr('Enter at least one denomination'); return; }
    setOpening(true);
    setOpenErr(null);
    try {
      const float = {
        [openCurrency]: { total: openTotal, denominations: toBreakdown(openCurrency, openDenoms) },
      };
      await openDrawer({ branchId, openingFloatJson: JSON.stringify(float) });
      await refreshDrawer();
      setSnack('Drawer opened');
      setOpenDenoms({});
      navigate('/customers');
    } catch (err) {
      setOpenErr(err.body?.message || err.body?.error || err.message);
    } finally {
      setOpening(false);
    }
  };

  const submitClose = async (confirmVariance) => {
    if (!drawer) return;
    setClosing(true);
    setCloseErr(null);
    try {
      const closingPayload = {
        [closeCurrency]: { total: closeTotal, denominations: toBreakdown(closeCurrency, closeDenoms) },
      };
      const closedId = drawer.id;
      await closeDrawer({
        drawerId: closedId,
        closingBalanceJson: JSON.stringify(closingPayload),
        confirmVariance,
      });
      await refreshDrawer();
      setCloseDenoms({});
      setVariancePreview(null);
      // Show success dialog with Print EOD Report option (STORY-159)
      setClosedDrawerId(closedId);
    } catch (err) {
      // Variance preflight: show recount/confirm dialog instead of erroring out
      if (err.status === 409 && err.body?.error === 'drawer.variance_detected') {
        setVariancePreview(err.body);
      } else {
        setCloseErr(err.body?.message || err.body?.error || err.message);
      }
    } finally {
      setClosing(false);
    }
  };

  const handleClose = () => submitClose(false);
  const handleConfirmVariance = () => submitClose(true);
  const handleRecount = () => { setVariancePreview(null); setCloseDenoms({}); };

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3 }}>Cash Drawer</Typography>

      {drawer && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Drawer is currently <strong>OPEN</strong> · opened {new Date(drawer.openedAt).toLocaleTimeString()}
        </Alert>
      )}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="Open Drawer" disabled={!!drawer} />
        <Tab label="Close Drawer" disabled={!drawer} />
      </Tabs>

      {tab === 0 && (
        <Card>
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>Opening Float</Typography>

            {!branchId ? (
              <Alert severity="error" sx={{ mb: 2 }}>
                Your user has no branch assigned. Ask an administrator to set your branch before opening a drawer.
              </Alert>
            ) : (
              <Alert severity="info" sx={{ mb: 2 }}>
                Branch: <strong>{user?.branchName ?? branchId}</strong>
              </Alert>
            )}

            <TextField
              select fullWidth margin="normal" label="Currency"
              value={openCurrency} onChange={(e) => { setOpenCurrency(e.target.value); setOpenDenoms({}); }}
            >
              <MenuItem value="USD">USD</MenuItem>
              <MenuItem value="ZWG">ZWG</MenuItem>
            </TextField>

            <Box sx={{ mt: 2 }}>
              <DenominationGrid
                currency={openCurrency}
                value={openDenoms}
                onChange={setOpenDenoms}
                targetAmount={null}
              />
            </Box>

            {openErr && <Alert severity="error" sx={{ mt: 2 }}>{openErr}</Alert>}

            <Button
              fullWidth variant="contained" sx={{ mt: 3, py: 1.2 }}
              disabled={opening || openTotal <= 0 || !branchId || !online}
              onClick={handleOpen}
            >
              {opening
                ? <CircularProgress size={22} />
                : !branchId
                  ? 'No branch assigned'
                  : `Open Drawer with ${openCurrency} ${openTotal.toLocaleString()}`}
            </Button>
          </CardContent>
        </Card>
      )}

      {tab === 1 && drawer && (
        <Card>
          <CardContent>
            <Typography variant="h6" sx={{ mb: 2 }}>Count and Close</Typography>

            <TextField
              select fullWidth margin="normal" label="Currency"
              value={closeCurrency} onChange={(e) => { setCloseCurrency(e.target.value); setCloseDenoms({}); }}
            >
              <MenuItem value="USD">USD</MenuItem>
              <MenuItem value="ZWG">ZWG</MenuItem>
            </TextField>

            <Box sx={{ mt: 2 }}>
              <DenominationGrid
                currency={closeCurrency}
                value={closeDenoms}
                onChange={setCloseDenoms}
                targetAmount={null}
              />
            </Box>

            {closeErr && <Alert severity="error" sx={{ mt: 2 }}>{closeErr}</Alert>}

            <Button
              fullWidth variant="contained" color="warning" sx={{ mt: 3, py: 1.2 }}
              disabled={closing || !online}
              onClick={handleClose}
            >
              {closing ? <CircularProgress size={22} /> : `Close Drawer with ${closeCurrency} ${closeTotal.toLocaleString()}`}
            </Button>
          </CardContent>
        </Card>
      )}

      <Snackbar
        open={!!snack}
        autoHideDuration={3000}
        onClose={() => setSnack(null)}
        message={snack}
      />

      {/* Variance warning — recount or confirm */}
      <Dialog open={!!variancePreview} maxWidth="sm" fullWidth disableEscapeKeyDown>
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CheckCircleIcon color="warning" />
          Cash Variance Detected
        </DialogTitle>
        <DialogContent>
          <Alert severity="warning" sx={{ mb: 2 }}>
            Your counted cash does not match the expected closing balance.
            Please recount, or confirm to proceed and post the variance.
          </Alert>
          {variancePreview && (
            <Box component="table" sx={{ width: '100%', borderCollapse: 'collapse', '& td, & th': { px: 1, py: 0.5, borderBottom: '1px solid', borderColor: 'divider' } }}>
              <thead>
                <tr>
                  <Box component="th" sx={{ textAlign: 'left' }}>Currency</Box>
                  <Box component="th" sx={{ textAlign: 'right' }}>Expected</Box>
                  <Box component="th" sx={{ textAlign: 'right' }}>Counted</Box>
                  <Box component="th" sx={{ textAlign: 'right' }}>Variance</Box>
                </tr>
              </thead>
              <tbody>
                {Object.keys({ ...(variancePreview.expected || {}), ...(variancePreview.counted || {}) }).map(ccy => {
                  const v = Number(variancePreview.variance?.[ccy] || 0);
                  return (
                    <tr key={ccy}>
                      <td>{ccy}</td>
                      <td style={{ textAlign: 'right' }}>{Number(variancePreview.expected?.[ccy] || 0).toLocaleString()}</td>
                      <td style={{ textAlign: 'right' }}>{Number(variancePreview.counted?.[ccy] || 0).toLocaleString()}</td>
                      <Box component="td" sx={{ textAlign: 'right', color: v === 0 ? 'text.primary' : (v > 0 ? 'success.main' : 'error.main'), fontWeight: 600 }}>
                        {v > 0 ? '+' : ''}{v.toLocaleString()}
                      </Box>
                    </tr>
                  );
                })}
              </tbody>
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={handleRecount}>Recount</Button>
          <Button variant="contained" color="warning" onClick={handleConfirmVariance} disabled={closing}>
            {closing ? <CircularProgress size={20} /> : 'Confirm and Close'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* STORY-159: post-close success dialog with Print EOD Report */}
      <Dialog open={!!closedDrawerId} disableEscapeKeyDown>
        <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CheckCircleIcon color="success" />
          Drawer Closed
        </DialogTitle>
        <DialogContent>
          <Typography>
            Your drawer has been closed successfully. Print the End-of-Day report
            and sign it together with your supervisor.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button
            startIcon={<PrintIcon />}
            onClick={() => closedDrawerId && openEodReport(closedDrawerId)}
          >
            Print EOD Report
          </Button>
          <Button
            variant="contained"
            onClick={() => { setClosedDrawerId(null); navigate('/'); }}
            autoFocus
          >
            Done
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
