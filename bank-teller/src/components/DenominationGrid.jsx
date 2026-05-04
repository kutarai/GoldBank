import { useMemo } from 'react';
import { Box, Typography, TextField, Table, TableBody, TableCell, TableHead, TableRow, Paper } from '@mui/material';

// Hardcoded denomination registry — matches DenominationValidationService.cs (Sprint 25).
// Will be replaced by an API call in STORY-163.
const DENOMS = {
  USD: [
    { face: 100, type: 'Note' },
    { face: 50,  type: 'Note' },
    { face: 20,  type: 'Note' },
    { face: 10,  type: 'Note' },
    { face: 5,   type: 'Note' },
    { face: 1,   type: 'Note' },
    { face: 0.50, type: 'Coin' },
    { face: 0.25, type: 'Coin' },
    { face: 0.10, type: 'Coin' },
    { face: 0.05, type: 'Coin' },
    { face: 0.01, type: 'Coin' },
  ],
  ZWG: [
    { face: 200, type: 'Note' },
    { face: 100, type: 'Note' },
    { face: 50,  type: 'Note' },
    { face: 20,  type: 'Note' },
    { face: 10,  type: 'Note' },
    { face: 5,   type: 'Note' },
    { face: 2,   type: 'Note' },
    { face: 1,   type: 'Note' },
    { face: 0.50, type: 'Coin' },
    { face: 0.25, type: 'Coin' },
    { face: 0.10, type: 'Coin' },
    { face: 0.05, type: 'Coin' },
  ],
};

export function getDenominationsFor(currency) {
  return DENOMS[currency] || [];
}

/**
 * Controlled denomination grid.
 * value: { [face: string]: count }
 * onChange(newValue)
 */
export default function DenominationGrid({ currency, value, onChange, targetAmount }) {
  const denoms = getDenominationsFor(currency);
  const notes = denoms.filter(d => d.type === 'Note');
  const coins = denoms.filter(d => d.type === 'Coin');

  const grandTotal = useMemo(() =>
    denoms.reduce((sum, d) => sum + d.face * (Number(value[d.face]) || 0), 0),
    [denoms, value]);

  const remaining = targetAmount != null ? Number(targetAmount) - grandTotal : null;

  const updateCount = (face, count) => {
    const next = { ...value };
    if (!count || count === '0') delete next[face];
    else next[face] = parseInt(count, 10) || 0;
    onChange(next);
  };

  const renderRows = (rows) => rows.map(d => (
    <TableRow key={d.face}>
      <TableCell sx={{ width: '30%' }}>{d.face >= 1 ? d.face : d.face.toFixed(2)}</TableCell>
      <TableCell sx={{ width: '40%' }}>
        <TextField
          type="number"
          size="small"
          inputProps={{ min: 0, style: { textAlign: 'right' } }}
          value={value[d.face] ?? ''}
          onChange={(e) => updateCount(d.face, e.target.value)}
          sx={{ width: 100 }}
        />
      </TableCell>
      <TableCell align="right">
        {((Number(value[d.face]) || 0) * d.face).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
      </TableCell>
    </TableRow>
  ));

  return (
    <Box>
      <Paper variant="outlined" sx={{ p: 2 }}>
        {notes.length > 0 && (
          <>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>Notes</Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Denomination</TableCell>
                  <TableCell>Count</TableCell>
                  <TableCell align="right">Subtotal</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>{renderRows(notes)}</TableBody>
            </Table>
          </>
        )}

        {coins.length > 0 && (
          <>
            <Typography variant="subtitle2" color="text.secondary" sx={{ mt: 2, mb: 1 }}>Coins</Typography>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Denomination</TableCell>
                  <TableCell>Count</TableCell>
                  <TableCell align="right">Subtotal</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>{renderRows(coins)}</TableBody>
            </Table>
          </>
        )}

        <Box sx={{ mt: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderTop: 1, borderColor: 'divider', pt: 1 }}>
          <Typography variant="h6">Total: {grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</Typography>
          {targetAmount != null && (
            <Typography
              variant="h6"
              color={remaining === 0 ? 'success.main' : remaining > 0 ? 'warning.main' : 'error.main'}
            >
              Remaining: {remaining.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </Typography>
          )}
        </Box>
      </Paper>
    </Box>
  );
}

// Convert the {face: count} value object to the API breakdown shape
export function toBreakdown(currency, value) {
  const denoms = getDenominationsFor(currency);
  return denoms
    .map(d => ({ faceValue: d.face, count: Number(value[d.face]) || 0, type: d.type }))
    .filter(line => line.count > 0);
}
