# STORY-162: Bank-Teller Production Hardening

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 1
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 27

---

## User Story

As a **branch operations manager**
I want **the bank-teller app to be hardened for production: idle timeout, session lock screen, double-submit protection, offline guard**
So that **walking away from the workstation, accidental double-clicks, and gateway outages can't cause incorrect transactions**

---

## Description

### Background
Cash-handling software demands defensive UX. This story rolls together the small-but-critical hardening tasks that don't fit elsewhere.

### Scope
**In scope:**
- 10-minute idle timeout — inactivity → lock screen requiring teller PIN to resume
- Double-submit protection — every confirm button disables itself for 2 seconds after click
- Offline guard — every screen polls `GET /api/teller/health` every 30 seconds; on failure, blocks all cash actions with a "Gateway unreachable — try again when connection restored" banner
- Cache-Control headers verified across all PII screens (`no-store`)
- React error boundary that catches render errors and shows a friendly recovery screen
- Console logs scrubbed of PII

**Out of scope:**
- Real authentication hardening (JWT refresh, MFA)
- Penetration testing

---

## Acceptance Criteria

- [ ] After 10 minutes of no mouse/keyboard activity, the app shows a lock screen requiring the teller's PIN
- [ ] Lock screen unlock calls `POST /api/teller/auth/unlock` (or reuses `/login` flow) and resumes the app on success
- [ ] Every confirm button disables itself for 2 seconds after click and shows a spinner
- [ ] Offline detection: every 30 s the app pings the gateway health endpoint; failed pings show a top-of-screen banner and disable cash mutation buttons
- [ ] No PII in browser console at any point (verified by manual test)
- [ ] Error boundary at the route level catches React errors and shows "Something went wrong — please try again or contact support"
- [ ] All Customer Card and transaction screens send `Cache-Control: no-store` headers (already done; this is the verification pass)

---

## Technical Notes

### Idle timeout
```jsx
const [lastActivity, setLastActivity] = useState(Date.now());
useEffect(() => {
  const onAct = () => setLastActivity(Date.now());
  window.addEventListener('mousemove', onAct);
  window.addEventListener('keydown', onAct);
  return () => { window.removeEventListener('mousemove', onAct); window.removeEventListener('keydown', onAct); };
}, []);
useEffect(() => {
  const id = setInterval(() => {
    if (Date.now() - lastActivity > 10 * 60 * 1000) setLocked(true);
  }, 5000);
  return () => clearInterval(id);
}, [lastActivity]);
```

---

## Dependencies

**Prerequisite Stories:** STORY-153 + everything from Sprint 26

**Blocked Stories:** None

---

## Definition of Done

- [ ] All 5 hardening tasks implemented
- [ ] Manual test pass for each
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
