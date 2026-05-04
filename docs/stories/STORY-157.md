# STORY-157: Drawer Open / Close / Running Balance

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 2
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 26

---

## User Story

As a **bank teller**
I want **to open my drawer with an opening float at the start of my shift, see my running balance per currency in the header all day, and close my drawer at the end of my shift with a count and variance review**
So that **I always know how much cash I should physically have and can balance to the penny at end-of-day**

---

## Description

### Background
The teller drawer is the heart of the cash workflow. Every deposit increases it; every withdrawal decreases it. The teller needs a constant visible indicator of their position, plus simple modals to open and close.

### Scope
**In scope:**
- Drawer header widget — visible on every screen, shows current balance per currency (e.g., `USD $5,420 · ZWG ₣12,300`) and a status pill (Open / Closed)
- "Open Drawer" modal accessible from `/drawer` and from any screen if no drawer is open — collects opening float per currency (denomination grid)
- "Close Drawer" modal — shows expected balance vs counted balance per currency, denomination grid for the count, variance display
- Drawer state fetched once on app load and refreshed after every cash transaction
- Opening Drawer requires the previous day's drawer to be closed
- Closing Drawer is BLOCKED if any of today's transactions are still `pending_signature` (STORY-170 will inject this check)

**Out of scope:**
- Vault float-out (STORY-167) — for Sprint 26 the opening float is entered manually; Sprint 28 wires it to the vault
- Vault surrender — same: manual close in 26, vault surrender in Sprint 28

### Header widget
```
┌────────────────────────────────────────────────────────────┐
│  GoldBank Teller   |  Drawer: Open  USD $5,420 · ZWG ₣12,300 │
│  Mtsunga (TLR-042) · Kuwadzana Branch                      │
└────────────────────────────────────────────────────────────┘
```

---

## Acceptance Criteria

- [ ] Header widget renders the current drawer balance per currency in the app shell
- [ ] Status pill: green "Open", red "Closed", grey "None" — the latter shows "Open Drawer" CTA
- [ ] Header refreshes after every successful deposit, withdrawal, reversal
- [ ] `/drawer` route shows the drawer detail screen with "Open" and "Close" actions
- [ ] "Open Drawer" modal: per-currency denomination grid, "Confirm Open Drawer" button calls `POST /api/teller/drawer/open` with the totals + breakdowns
- [ ] If the previous day's drawer is still open, "Open Drawer" returns 409 and the modal shows "Close yesterday's drawer first"
- [ ] After successful open, drawer header transitions to "Open" with the new totals
- [ ] "Close Drawer" modal: shows expected balance per currency (computed from running balance), denomination grid for the actual count, live variance per currency
- [ ] Variance is highlighted in red if non-zero, green if zero
- [ ] "Confirm Close Drawer" button calls `POST /api/teller/drawer/close` with the counted breakdowns
- [ ] If variance is non-zero, the close endpoint records it and the front-end shows a "Variance recorded for review" message
- [ ] After successful close, drawer header transitions to "Closed"; deposit/withdrawal screens are blocked with "Drawer is closed" until a new one is opened
- [ ] Manual test: open drawer with $5000 USD, do a $500 deposit, check header reads $5500 USD, close with counted $5500 — variance 0

---

## Technical Notes

### Header component
```jsx
function DrawerHeaderWidget() {
  const { currentDrawer, refreshDrawer } = useTellerSession();
  if (!currentDrawer) return <Chip label="No drawer" onClick={() => navigate('/drawer')} />;
  return <Box>...{Object.entries(currentDrawer.runningBalances).map(([cur, amt]) =>
    <Chip key={cur} label={`${cur} ${amt}`} />)}</Box>;
}
```

### Drawer fetch
On app mount and after each mutation, call `GET /api/teller/drawer/current`. Cache the result in `TellerSessionContext`.

### Variance highlight
```jsx
const variance = counted - expected;
<Typography color={variance === 0 ? 'success.main' : 'error.main'}>
  Variance: {variance > 0 ? '+' : ''}{variance.toFixed(2)}
</Typography>
```

---

## Dependencies

**Prerequisite Stories:** STORY-149 (drawer endpoints), STORY-153 (app shell)

**Blocked Stories:** STORY-155 and 156 both consume the drawer state

---

## Definition of Done

- [ ] Header widget and drawer screen implemented
- [ ] Open and Close modals work end-to-end
- [ ] Variance display correct
- [ ] Header refreshes after every cash mutation
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
