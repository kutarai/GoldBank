# STORY-156: Withdrawal Screen

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 4
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 26

---

## User Story

As a **bank teller**
I want **a withdrawal screen that locks the form until I have visually verified the customer's identity, and that triggers a supervisor PIN modal for high-value withdrawals**
So that **I cannot pay out cash without dual control and identity proof**

---

## Description

### Background
Withdrawals are the highest-risk teller transaction. FR-005 enforces three controls: (1) the Customer Card with photo + ID + signature must be visible, (2) the teller must explicitly tick "Identity verified", and (3) high-value withdrawals require a physically-present supervisor's PIN. This screen orchestrates all three.

### Scope
**In scope:**
- Account pre-populated via `?account=` from the Customer Card "Process Withdrawal" button
- Inline mini Customer Card (photo + signature thumbnails) at the top of the screen so the teller can keep verifying as they fill the form
- "Identity verified by photo, signature, and ID" checkbox — form is **locked** until ticked
- Currency, amount, denomination grid (same component as deposit)
- Confirm button: validates, calls `/api/teller/withdrawals`
- If response is `requiresApproval`, opens a supervisor PIN modal
- Supervisor modal collects username + PIN, calls the approve endpoint
- On final success, navigates to receipt preview

**Out of scope:**
- Server-side approval flow (STORY-152)
- Receipt PDF (STORY-158)
- Signed-document upload (STORY-170)

---

## Acceptance Criteria

- [ ] Screen requires `?account=` query param; otherwise redirects to customer search
- [ ] Mini Customer Card at top shows photo, signature, customer name, account ID, balance — fetched from `GET /api/teller/customers/{id}/card`
- [ ] "Identity verified by photo, signature, and ID" checkbox is rendered prominently with a warning icon
- [ ] All form inputs (currency, amount, denomination grid, confirm button) are **disabled** until the checkbox is ticked
- [ ] Currency dropdown limited to currencies the customer holds
- [ ] Available balance shown for the selected currency; amount field disallows exceeding it
- [ ] Denomination grid (same component as STORY-155) with live grand total and "remaining" indicator
- [ ] Confirm button enabled only when: identity checkbox ticked, valid amount, denominations sum to amount, balance sufficient
- [ ] On confirm, POST `/api/teller/withdrawals` with `{ accountId, currency, amount, denominationBreakdown, identityVerified: true }`
- [ ] If response 202 with `requiresApproval`, open the Supervisor Approval modal
- [ ] Supervisor Approval modal: warning banner, supervisor username input, supervisor PIN input (masked), Cancel and Approve buttons
- [ ] Approve button calls `POST /api/teller/withdrawals/{pendingId}/approve` with the supervisor credentials
- [ ] On approval failure, modal shows "Approval failed" without revealing whether the username or PIN was wrong
- [ ] On approval success or no-approval-needed 200, navigate to Receipt Preview
- [ ] Drawer header running balance updates after success
- [ ] No drawer? Block the screen with a "Open your drawer first" banner

---

## Technical Notes

### Form lock pattern
```jsx
const [identityVerified, setIdentityVerified] = useState(false);
<TextField disabled={!identityVerified} ... />
<Button disabled={!identityVerified || !validForm} ... />
```

### Supervisor approval modal
```jsx
function ApprovalModal({ pendingId, onSuccess, onCancel }) {
  const [username, setUsername] = useState('');
  const [pin, setPin] = useState('');
  const [error, setError] = useState(null);
  const handleApprove = async () => {
    try {
      await api.post(`/withdrawals/${pendingId}/approve`, { supervisorUsername: username, supervisorPin: pin });
      onSuccess();
    } catch { setError('Approval failed.'); }
  };
  return <Dialog>...<TextField type="password" value={pin} ... />...</Dialog>;
}
```

---

## Dependencies

**Prerequisite Stories:** STORY-149, 150, 151, 152, 153, 154, 155 (denomination grid component reuse)

**Blocked Stories:** None directly

---

## Definition of Done

- [ ] Screen implemented with locked-form pattern
- [ ] Supervisor approval modal flow works end-to-end
- [ ] Mini customer card visible during the flow
- [ ] Validation and error handling complete
- [ ] Manual test for under-threshold and over-threshold withdrawals
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
