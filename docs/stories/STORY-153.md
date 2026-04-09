# STORY-153: bank-teller App Scaffolding

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 26

---

## User Story

As a **bank teller**
I want **a dedicated front-end application that I can log into with my teller credentials**
So that **I have a focused workspace separate from the back-office bank-client portal**

---

## Description

### Background
The teller workflow is high-velocity and very different from the back-office admin work that the existing bank-client supports. A dedicated React + Vite + MUI app at `bank-teller/` lets us optimise the UX for the counter without polluting bank-client's IA.

### Scope
**In scope:**
- New folder `bank-teller/` at the repo root
- Vite + React 18 + MUI 6 + React Router 7 — same stack as bank-client for cross-team familiarity
- Login screen → JWT auth → role guard
- App shell with header (teller name, branch, drawer status, logout) and main content area
- React Context for the active session (JWT, teller info, current drawer)
- Empty placeholder routes for the screens that ship in subsequent stories: `/customers`, `/customers/:id`, `/deposit`, `/withdrawal`, `/drawer`, `/eod`
- `package.json` script `dev` runs the dev server on port `5174` (avoid clash with bank-client's 5173)

**Out of scope:**
- Real screen implementations (covered by 154–157)
- Receipt printing (158)
- Vault screens (167)

---

## Acceptance Criteria

- [ ] `bank-teller/` directory exists with `package.json`, `vite.config.js`, `index.html`, `src/main.jsx`, `src/App.jsx`
- [ ] `npm run dev` starts the dev server on port `5174`
- [ ] Login screen at `/login` accepts username + password and POSTs to `/api/teller/auth/login` (or reuses existing `/api/auth/login` — depends on whether teller auth is already wired)
- [ ] On successful login, the JWT is stored in `sessionStorage` under key `unibank_teller_token`
- [ ] If the JWT role isn't `teller`, `branch_manager`, or `super_admin`, login is rejected with "Access denied — teller role required"
- [ ] App shell shows: tenant logo, teller full name, branch name, current drawer status (Open/Closed/None), Logout button
- [ ] React Router routes: `/login`, `/` (dashboard), `/customers`, `/customers/:id`, `/deposit`, `/withdrawal`, `/drawer`, `/eod`
- [ ] `/login` is the only public route; everything else requires a valid JWT (redirect to `/login` if missing)
- [ ] React Context `TellerSessionContext` exposes `{ jwt, teller, branch, currentDrawer, refresh, logout }`
- [ ] `currentDrawer` is fetched from `GET /api/teller/drawer/current` on app load and refreshed after any drawer mutation
- [ ] Logout clears `sessionStorage` and redirects to `/login`
- [ ] MUI theme matches bank-client (same primary colour, dark mode, fonts)
- [ ] Vite hot-reload works
- [ ] No console errors on first load

---

## Technical Notes

### Folder structure
```
bank-teller/
├── package.json
├── vite.config.js
├── index.html
├── src/
│   ├── main.jsx
│   ├── App.jsx
│   ├── theme.js
│   ├── auth/
│   │   ├── TellerSessionContext.jsx
│   │   └── ProtectedRoute.jsx
│   ├── layouts/
│   │   └── MainLayout.jsx
│   ├── pages/
│   │   ├── Login.jsx
│   │   ├── Dashboard.jsx
│   │   ├── Customers.jsx
│   │   ├── CustomerCard.jsx
│   │   ├── Deposit.jsx
│   │   ├── Withdrawal.jsx
│   │   ├── Drawer.jsx
│   │   └── EndOfDay.jsx
│   └── services/
│       └── api.js
```

### vite.config.js
```js
export default defineConfig({
  plugins: [react()],
  server: { port: 5174 },
});
```

### api.js base
```js
const API_BASE = 'http://localhost:5001/api/teller';
function token() { return sessionStorage.getItem('unibank_teller_token'); }
async function call(path, opts = {}) {
  const res = await fetch(`${API_BASE}${path}`, {
    ...opts,
    headers: { ...(opts.headers || {}), Authorization: `Bearer ${token()}`, 'Content-Type': 'application/json' },
  });
  if (res.status === 401) { sessionStorage.clear(); window.location = '/login'; }
  return res.ok ? res.json() : Promise.reject(await res.text());
}
```

---

## Dependencies

**Prerequisite Stories:** STORY-149 (gateway endpoints exist, including a teller auth endpoint or reused admin auth)

**Blocked Stories:** STORY-154, 155, 156, 157, 158, 159, 160, 161, 162, 167

---

## Definition of Done

- [ ] App scaffolds and runs on port 5174
- [ ] Login flow works against the dev gateway
- [ ] Role guard rejects non-teller JWTs
- [ ] App shell and protected routes in place
- [ ] Theme and shared layout match bank-client
- [ ] README.md in `bank-teller/` documents the dev setup
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
