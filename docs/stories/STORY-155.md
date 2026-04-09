# STORY-155: Deposit Screen

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 4
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 26

---

## User Story

As a **bank teller**
I want **a focused screen to record a cash deposit with depositor name, currency, amount, and a denomination grid that auto-totals**
So that **I can process deposits in under 60 seconds with zero arithmetic mistakes**

---

## Description

### Background
The deposit screen is the most-used screen in the bank-teller app. It must be fast, error-resistant, and produce a printable receipt. Anyone may deposit (no identity verification needed) so the flow is short.

### Scope
**In scope:**
- Account selector (free text → search → pick OR pre-populated from Customer Card "Process Deposit" button)
- Currency selector (constrained to currencies the customer has accounts for)
- Depositor name field (free text — may differ from account holder)
- Amount field (numeric, large)
- Denomination grid (rendered from `currency_denominations` for the chosen currency, grouped Notes / Coins)
- Per-denomination quantity inputs with auto-computed sub-total per row
- Live grand total and live "remaining" indicator
- Confirm button enabled only when grand total === amount AND validation passes
- On confirm: POST `/api/teller/deposits`, show receipt preview, "Print" and "Done" buttons

**Out of scope:**
- Receipt PDF generation server-side (STORY-158)
- Signed-document upload (STORY-170)
- Vault movements (STORY-167)

### User flow
1. Teller picks account (or arrives pre-populated)
2. Selects currency → denomination grid loads
3. Enters depositor name and amount
4. Enters denominations → grand total updates live
5. Confirms when totals match
6. Receipt preview opens with Print + Done buttons

---

## Acceptance Criteria

- [ ] Account selector supports free text search and pre-population via `?account=` query param
- [ ] Currency dropdown lists only the currencies the customer holds accounts for
- [ ] Depositor name is a required field (1–200 chars)
- [ ] Amount is a required positive decimal
- [ ] Denomination grid renders with Notes group on top and Coins group below, denominations in descending face value
- [ ] Each row shows: face value, count input, computed sub-total
- [ ] Grand total updates on every keystroke
- [ ] "Remaining" indicator shows `amount − grandTotal`; turns green when zero
- [ ] Confirm button disabled while remaining ≠ 0
- [ ] Confirm button disabled if any input is invalid
- [ ] On confirm, POST `/api/teller/deposits` with `{ accountId, currency, amount, depositorName, denominationBreakdown }`
- [ ] On 200, navigate to a Receipt Preview screen showing the deposit details
- [ ] On error, show inline error message and keep the form data
- [ ] Drawer header running balance updates after a successful deposit
- [ ] No drawer? Block the screen with a banner "Open your drawer first" that links to `/drawer`

---

## Technical Notes

### Grid component
```jsx
function DenominationGrid({ currency, value, onChange }) {
  const denoms = useDenominations(currency); // hook calls /api/teller/denominations?currency=...
  return (
    <Box>
      <Typography>Notes</Typography>
      {denoms.filter(d => d.type === 'Note').map(d => (
        <Row key={d.id} d={d} count={value[d.id] ?? 0}
             onCount={c => onChange({ ...value, [d.id]: c })} />
      ))}
      <Typography>Coins</Typography>
      {denoms.filter(d => d.type === 'Coin').map(d => (
        <Row key={d.id} d={d} count={value[d.id] ?? 0}
             onCount={c => onChange({ ...value, [d.id]: c })} />
      ))}
    </Box>
  );
}
```

### Live total
```jsx
const grandTotal = useMemo(() =>
  denoms.reduce((sum, d) => sum + d.faceValue * (value[d.id] ?? 0), 0),
  [denoms, value]);
const remaining = amount - grandTotal;
```

---

## Dependencies

**Prerequisite Stories:** STORY-149, 151, 153 (app shell exists), 154 (account search infra), 163 (denomination registry — until then use hardcoded)

**Blocked Stories:** None directly; STORY-158 produces the actual PDF that this screen previews

---

## Definition of Done

- [ ] Screen implemented with full denomination grid + live totals
- [ ] Validation prevents submission unless totals match
- [ ] Drawer balance refreshes after successful deposit
- [ ] Manual test of multi-denomination deposit
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
