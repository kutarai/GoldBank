# STORY-154: Customer Search + Customer Card Screen

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 26

---

## User Story

As a **bank teller**
I want **to search for a customer by phone, account number, ID number, or name and see their photo, ID, and signature side-by-side with their account info**
So that **I can verify their identity quickly before processing a withdrawal**

---

## Description

### Background
Identity verification is the most safety-critical step of a withdrawal. The teller must visually confirm: the person at the counter matches the photo on file, their signature matches the stored signature, and their ID document matches the person and the stored ID. This screen makes that verification trivial.

### Scope
**In scope:**
- Customer search bar on the dashboard (and on the deposit/withdrawal entry points)
- Search results list with name, account ID, phone, balance, status chip
- "Open Card" button on each result → navigates to `/customers/{id}`
- Customer Card screen with:
  - Large prominent photo (selfie) on the left
  - ID document image and signature image stacked on the right
  - Profile fields below: full name, phone, email, DOB, national ID, KYC level, status
  - Balance(s) per currency
  - Status flags: frozen, suspended, signature verified
  - Action buttons: "Process Deposit", "Process Withdrawal" (disabled if account frozen/suspended/closed)

**Out of scope:**
- Editing customer data
- Adding new customers
- Deposit/withdrawal forms (155, 156)

### User flow
1. Teller types in the search box → debounced API call → results appear
2. Teller clicks a result → Customer Card opens
3. Teller visually verifies photo + ID + signature match the person at the counter
4. Teller clicks "Process Deposit" or "Process Withdrawal" → corresponding screen pre-populated with this account

---

## Acceptance Criteria

- [ ] Search box on the dashboard accepts free text and calls `GET /api/teller/customers/search?q=...` (300 ms debounce)
- [ ] Search supports phone number, account number, national ID, full name, partial name (server-side LIKE)
- [ ] Empty query returns no results (don't auto-list everyone)
- [ ] Result list shows: name, masked phone, balance per currency, status chip
- [ ] Clicking a result navigates to `/customers/{accountId}`
- [ ] Customer Card screen calls `GET /api/teller/customers/{id}/card` (STORY-150) and renders the response
- [ ] Selfie image is large (≥ 400 px wide), prominently placed left
- [ ] ID image and signature image stacked on the right, each ≥ 250 px wide, with `objectFit: contain`
- [ ] Profile fields displayed in a label/value table
- [ ] Status flags rendered as colored chips (green=Active, orange=Suspended, blue=Frozen, red=Closed)
- [ ] "Process Deposit" button always enabled (anyone can deposit)
- [ ] "Process Withdrawal" button disabled if status ∈ (Frozen, Suspended, Closed) OR KYC level < threshold
- [ ] Disabled state shows tooltip explaining why
- [ ] Card screen sets `Cache-Control: no-store` (PII)
- [ ] Loading skeletons display while fetching
- [ ] Errors (404, 403) show friendly messages
- [ ] Mobile responsive layout collapses to single column on narrow screens

---

## Technical Notes

### Search debounce
```jsx
const [query, setQuery] = useState('');
const [results, setResults] = useState([]);
useEffect(() => {
  if (!query.trim()) { setResults([]); return; }
  const id = setTimeout(() => {
    api.get(`/customers/search?q=${encodeURIComponent(query)}`).then(setResults);
  }, 300);
  return () => clearTimeout(id);
}, [query]);
```

### Customer Card layout
Two-column Grid: left `md=5` for photo, right `md=7` for ID + signature + profile + actions.

### Disabled withdrawal logic
```jsx
const cannotWithdraw = !card || card.status !== 'Active' || card.kycLevel < 1;
<Tooltip title={cannotWithdraw ? 'Account is not active or KYC incomplete' : ''}>
  <span><Button disabled={cannotWithdraw} onClick={() => navigate(`/withdrawal?account=${card.accountId}`)}>
    Process Withdrawal
  </Button></span>
</Tooltip>
```

---

## Dependencies

**Prerequisite Stories:** STORY-149 (search endpoint), STORY-150 (card endpoint), STORY-153 (app shell)

**Blocked Stories:** STORY-156 (withdrawal screen requires the verified card)

---

## Definition of Done

- [ ] Search and Customer Card screens implemented
- [ ] Disabled-state logic working
- [ ] Loading and error states
- [ ] Responsive layout
- [ ] Manual test on real customer data
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
