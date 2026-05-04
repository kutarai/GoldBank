# GoldBank Branch Operations — Training Manual

**Audience:** Tellers, Vault Managers, Branch Managers
**System:** GoldBank Bank-Teller Application (`bank-teller`)
**Document version:** 1.0 — 2026-04-08
**Companion sample reports:** `docs/training/samples/`

---

## How to use this manual

This is the operational handbook for the GoldBank branch counter. Every workflow is broken into:

1. **Purpose** — what the process is for and when to run it
2. **Prerequisites** — what must be true before you start
3. **Step-by-step procedure** — numbered actions with screen sketches
4. **What the system does** — the side-effects you should expect
5. **Errors and recovery** — what to do when something goes wrong

Screen sketches use a fixed-width frame to represent the layout you'll see on screen. They are not pixel-perfect, but the **labels, button positions, and field names match the live application**.

```
+-----------------------------------------------------+
| This is what a screen sketch looks like.            |
| Buttons appear like:  [ Confirm Deposit ]           |
| Fields appear like:   Amount: [____________]        |
+-----------------------------------------------------+
```

> **⚠ Important** — boxes with a warning icon flag actions you cannot undo without supervisor intervention.

> **ℹ Tip** — boxes with an info icon contain shortcuts and operational hints from production tellers.

---

# Part 1 — Roles and Permissions

| Role            | What they can do                                                                                                  |
|-----------------|-------------------------------------------------------------------------------------------------------------------|
| **Teller**          | Open/close own drawer, search customers, process deposits, process withdrawals (with approval over threshold), reverse own same-shift transactions, print receipts |
| **Branch Manager**  | Everything a teller can do, plus: approve high-value withdrawals, view branch dashboard, approve drawer variances, run vault spot checks, issue float to tellers, reverse any teller's transaction |
| **Vault Manager**   | Open vault module, accept cash injections from HQ, ship cash to HQ, issue float, accept teller surrenders, run spot checks, print vault EOD report |
| **Admin**           | All of the above plus user/branch administration |

A user's role is set when their account is created. **You cannot change your own role.** Contact head office IT if you believe your role is wrong.

---

# Part 2 — Logging In and the App Layout

## 2.1 Logging in

The bank-teller app runs in a browser. Open it at **http://branch.goldbank.local:5174** (the exact URL is on the sticker on your monitor).

```
+------------------------------------------------------+
|                  GoldBank Teller                      |
|                                                      |
|     Username:  [_______________________]             |
|     Password:  [_______________________]             |
|                                                      |
|                       [   Sign In   ]                |
|                                                      |
|     Forgot password? Contact branch supervisor.      |
+------------------------------------------------------+
```

**Steps:**
1. Type your username (usually `firstname.lastname` or `t.username`).
2. Type your password.
3. Press **Sign In** or hit Enter.

The system will:
- Authenticate against the central directory.
- Load your role and branch assignment.
- Redirect you to the dashboard appropriate to your role.

**Errors:**
- *"Invalid credentials"* — wrong username or password. After 5 failed attempts your account is locked for 15 minutes; supervisor unlock required.
- *"Your user has no branch assigned"* — admin error; you cannot work until your branch is set.

> **ℹ Tip** — You will be **automatically locked out after 10 minutes of inactivity** to protect customer data. Just re-enter your password to resume.

## 2.2 The main layout

After login every screen has the same top bar:

```
+----------------------------------------------------------------------------+
| GoldBank Teller   [Customers] [Drawer] [Vault*]      Drawer Open  J. Doe   |
|                                                                  Borrowdale|
|                                                       [Logout]            |
+----------------------------------------------------------------------------+
| (Page content here — changes per route)                                    |
+----------------------------------------------------------------------------+
```

- The **[Vault]** button only appears for Branch Managers and Vault Managers.
- The **Drawer Open** chip is green when your drawer is open and red ("No drawer") when it isn't.
- Your **name** and **branch** are shown on the top right at all times — always confirm you're at the right branch before processing.

## 2.3 The offline indicator

If the gateway becomes unreachable a red banner appears at the top:

```
+----------------------------------------------------------------------------+
| ☁ Gateway unreachable — cash transactions are blocked until restored.      |
+----------------------------------------------------------------------------+
```

While this banner is showing, **all confirm buttons for cash mutations are disabled**. You can still browse customer cards. As soon as the connection is restored the banner disappears and the buttons re-enable on their own.

> **⚠ Important** — Never try to "force" a transaction by reloading the page when offline. Wait for the banner to clear, then resume.

---

# Part 3 — Teller Workflows

## 3.1 Opening Your Drawer (Start of Day)

**Purpose:** Tell the system how much physical cash you are starting your shift with. This becomes your *opening float* and is the baseline against which the system reconciles your end-of-day count.

**Prerequisites:**
- You have collected the cash from the vault manager (recorded as a vault DrawerIssue movement on the vault side).
- You know the exact denomination breakdown of what you received.
- Your previous day's drawer is **closed and balanced**. The system will refuse to open a new drawer if a previous one is still open.

### Steps

1. From the dashboard, click **Drawer** in the top bar.
2. The Drawer screen opens on the **Open Drawer** tab.

```
+----------------------------------------------------------------------------+
| Cash Drawer                                                               |
|                                                                            |
|   Open Drawer  |  Close Drawer (disabled)                                  |
|                                                                            |
|   Branch: Borrowdale Branch                                                |
|                                                                            |
|   Currency:  [ USD ▼ ]                                                     |
|                                                                            |
|   Denominations                                                            |
|   +------------------+   +------------------+                              |
|   | Notes            |   | Coins            |                              |
|   |  $100  [ 10 ]    |   |  $0.50 [  4 ]    |                              |
|   |  $50   [  4 ]    |   |  $0.25 [  8 ]    |                              |
|   |  $20   [ 25 ]    |   |  $0.10 [ 20 ]    |                              |
|   |  $10   [ 30 ]    |   |  $0.05 [ 40 ]    |                              |
|   |  $5    [ 60 ]    |   |  $0.01 [100 ]    |                              |
|   |  $1    [200 ]    |   |                  |                              |
|   +------------------+   +------------------+                              |
|                                                                            |
|                            Total: USD 2,500.00                             |
|                                                                            |
|              [   Open Drawer with USD 2,500   ]                            |
+----------------------------------------------------------------------------+
```

3. Confirm the currency (default USD). If you also need to load ZWG, you'll repeat the process per currency after first opening.
4. Tap each denomination box and enter the count of notes/coins of that face value.
5. Watch the **Total** at the bottom — it must match your physical count.
6. Click **Open Drawer with USD X,XXX**.

### What the system does

- Creates a `teller_drawer_sessions` row in your name with status **Open**.
- Saves the opening float JSON: `{"USD": {"total": 2500.00, "denominations": [...]}}`.
- Audit-logs the action with your user ID and timestamp.
- Redirects you to the customer search screen.

### Errors and recovery

| Error message                              | Cause                                      | Fix                                          |
|--------------------------------------------|--------------------------------------------|----------------------------------------------|
| *"No branch is assigned to your user"*     | Admin error                                | Stop. Contact admin. Do not improvise.       |
| *"Enter at least one denomination"*        | All counts are zero                        | Type a count > 0                             |
| *"A drawer is already open for this teller"* | Yesterday's drawer wasn't closed         | Close yesterday's drawer first (Part 3.4)   |

---

## 3.2 Finding a Customer

**Purpose:** Locate a customer's account before processing any deposit, withdrawal, or balance inquiry.

### Steps

1. Click **Customers** in the top bar (or click "Find Customer" on the dashboard).
2. The Customer Search screen opens.

```
+----------------------------------------------------------------------------+
| Find Customer                                                              |
|                                                                            |
|   Search:  [____________________________]   [ Search ]                     |
|                                                                            |
|   Search by phone number, account number, national ID, or name.            |
|                                                                            |
|   Results:                                                                 |
|   +----------------------------------------------------------------------+ |
|   |  Photo  Name              Account     Phone        KYC   Status      | |
|   +----------------------------------------------------------------------+ |
|   |  (img)  Chiedza Mutasa    ACC-1234    +263 77 ... Tier 2 Active     | |
|   |  (img)  Chiedza Marowa    ACC-5678    +263 71 ... Tier 1 Active     | |
|   +----------------------------------------------------------------------+ |
+----------------------------------------------------------------------------+
```

3. Type any of:
   - **Full or partial phone number** (with or without country code)
   - **Account number** (e.g. `ACC-1234`)
   - **National ID** (e.g. `12-345678 A 12`)
   - **First or last name**
4. Press Enter or click **Search**.
5. Click the matching row to open their **Customer Card**.

### What you see on the Customer Card

```
+----------------------------------------------------------------------------+
|  [ Process Deposit ]   [ Process Withdrawal ]                              |
|                                                                            |
|  Customer Card                                                             |
|                                                                            |
|  +---------------------+   +-------------------------------------+         |
|  | Photo               |   | Profile  |  Transactions            |         |
|  |  +---------------+  |   |--------------------------------------|         |
|  |  |   selfie      |  |   |  Chiedza Mutasa     [Active]         |         |
|  |  |   200x260     |  |   |                                      |         |
|  |  +---------------+  |   |  Account ID    ACC-1234              |         |
|  |                     |   |  Phone         +263 77 555 1234      |         |
|  | ID Document         |   |  Email         chiedza@example.com   |         |
|  |  +---------------+  |   |  Date of Birth 1988-04-12            |         |
|  |  |   ID card     |  |   |  National ID   12-345678 A 12        |         |
|  |  +---------------+  |   |  KYC Level     Level 2               |         |
|  |                     |   |                                      |         |
|  | Signature           |   |  Balances                            |         |
|  |  +---------------+  |   |    USD                  1,250.00     |         |
|  |  |   signature   |  |   |    ZWG                 12,000.00     |         |
|  |  +---------------+  |   |                                      |         |
|  |  Verified by teller |   |                                      |         |
|  |  on 2026-04-08      |   +-------------------------------------+         |
|  +---------------------+                                                    |
+----------------------------------------------------------------------------+
```

**Three things you must verify before processing a withdrawal**:

1. **Photo** — does the person at the counter look like the photo on file?
2. **ID Document** — is the physical ID they handed you the same as the one on screen? Numbers match?
3. **Signature** — does the new signature on the slip match the one on file? Verified-by tag should be present.

> **⚠ Important** — If any of the three checks fails, **do not process the withdrawal**. Politely ask the customer for additional verification or refer to the branch manager.

The **Transactions tab** on the right shows the customer's recent transaction history. Use it to:
- Confirm a previous deposit cleared
- Identify duplicate-payment requests
- Spot suspicious patterns

---

## 3.3 Processing a Deposit

**Purpose:** Credit a customer account with cash brought to the counter. The depositor may or may not be the account holder.

**Prerequisites:**
- Drawer is open.
- Customer account is found and active.
- Cash is physically counted.

### Steps

1. From the Customer Card, click **Process Deposit** at the top left.
2. The Deposit screen opens, pre-filled with the customer info.

```
+----------------------------------------------------------------------------+
| New Deposit                                                                |
|                                                                            |
|  +--------------------------+   +--------------------------------------+   |
|  | Transaction Details      |   | Denominations                        |   |
|  |                          |   |                                      |   |
|  | Customer                 |   |  Notes                Coins          |   |
|  |  Chiedza Mutasa          |   |   $100 [ 5 ]           $0.50 [ 0 ]   |   |
|  |  ACC-1234                |   |   $50  [ 4 ]           $0.25 [ 0 ]   |   |
|  |                          |   |   $20  [ 5 ]           $0.10 [ 0 ]   |   |
|  | Currency:    [ USD ▼ ]   |   |   $10  [ 5 ]           $0.05 [ 0 ]   |   |
|  |                          |   |   $5   [10 ]           $0.01 [ 0 ]   |   |
|  | Depositor:   [          ]|   |   $1   [50 ]                         |   |
|  |  May differ from holder. |   |                                      |   |
|  |                          |   |  Counted total: USD 1,000.00         |   |
|  | Amount:      [   1000   ]|   |  Target:        USD 1,000.00 ✓       |   |
|  |                          |   |                                      |   |
|  |  [   Confirm Deposit   ] |   |                                      |   |
|  +--------------------------+   +--------------------------------------+   |
+----------------------------------------------------------------------------+
```

3. Verify the currency. If the customer wants to deposit ZWG, switch the dropdown to ZWG.
4. **Depositor Name** — type the name of the person physically handing you the money. **This may be a third party.** If it's the account holder themselves, type their name to make the audit clean.
5. **Amount** — type the total amount the customer says they're depositing.
6. In the **Denominations** panel, count the notes and coins one denomination at a time and type each count.
7. Watch the **Counted total** indicator. It must match the **Target** (the Amount field). The system will refuse to confirm if they don't match.
8. Click **Confirm Deposit**.

### What the system does

- Validates the denomination breakdown sums exactly to the amount.
- Validates the currency exists on the customer's accounts.
- Inserts a `branch_cash_transactions` row of type `Deposit`.
- Posts a credit to the customer's `accounts.balance` for that currency.
- Generates an A6 receipt PDF containing: branch name, depositor name, customer name, amount, denomination breakdown, QR code, and a transaction reference.
- Increases your drawer's running cash position.
- Returns a success dialog with two buttons: **Print Receipt** and **OK**.

### The success dialog

```
+----------------------------------------------------------------------------+
|  ✓  Transaction Successfully Completed                                     |
|                                                                            |
|     Deposit recorded.                                                      |
|     Reference: TXN-20260408-001234                                         |
|                                                                            |
|             [ Print Receipt ]              [   OK   ]                      |
+----------------------------------------------------------------------------+
```

- **Print Receipt** opens the PDF in a new tab; print on the receipt printer.
- **OK** closes the dialog and returns you to the Customers list.

> **ℹ Tip** — Always print the receipt and stamp it before handing it to the depositor. The QR code on the receipt links to a verification endpoint anyone can scan to confirm the transaction is real.

### Sample receipt content

```
============================================================
                  GOLDBANK CASH RECEIPT
============================================================
Branch:      Borrowdale Branch
Teller:      J. Doe
Date/Time:   2026-04-08 10:23:45
Reference:   TXN-20260408-001234

Type:        DEPOSIT
Customer:    Chiedza Mutasa  (ACC-1234)
Depositor:   Chiedza Mutasa
Currency:    USD
Amount:      1,000.00

Denomination Breakdown:
   $100 x  5  =   500.00
   $50  x  4  =   200.00
   $20  x  5  =   100.00
   $10  x  5  =    50.00
   $5   x 10  =    50.00
   $1   x 50  =    50.00
   ----                                  --------
   Total notes: 79               1,000.00

[QR CODE]                  Customer signature: ____________
============================================================
```

### Errors and recovery

| Error message                              | Cause                                      | Fix                                          |
|--------------------------------------------|--------------------------------------------|----------------------------------------------|
| *"Denomination breakdown does not sum"*    | Amount ≠ counted denominations              | Recount; correct the count or the amount    |
| *"Customer not found"*                     | Account ID changed or wrong customer       | Re-search                                    |
| *"You must open your drawer first"*        | No active drawer session                   | Open drawer (Part 3.1)                       |
| *"Account is frozen / suspended"*          | Compliance hold on the account             | Refer to branch manager                      |

---

## 3.4 Processing a Withdrawal

**Purpose:** Pay out cash from a customer's account. **The customer must be present.**

**Prerequisites:**
- Drawer is open and has enough cash of the requested currency and denomination mix.
- Customer is at the counter, holding their physical ID.
- Customer's photo, ID, and signature on the Customer Card have all been verified.

### Steps

1. From the Customer Card, click **Process Withdrawal**.
2. The Withdrawal screen opens.

```
+----------------------------------------------------------------------------+
| New Withdrawal                                                             |
|                                                                            |
|  +-----------------------------+                                           |
|  | Mini Customer Card          |                                           |
|  | (img) Chiedza Mutasa        |                                           |
|  |       ACC-1234 · +263 77... |                                           |
|  |       Available USD 1,250   |                  (signature img)         |
|  +-----------------------------+                                           |
|                                                                            |
|  ⚠  [ ] I have verified the customer's identity by photo, signature,       |
|         and physical ID document.                                          |
|                                                                            |
|  +--------------------------+   +--------------------------------------+   |
|  | Transaction Details      |   | Denominations                        |   |
|  | Currency:    [ USD ▼ ]   |   |  (grid as in deposit, disabled       |   |
|  | Amount:      [          ]|   |   until verification box ticked)     |   |
|  |  Available 1,250         |   |                                      |   |
|  |                          |   |                                      |   |
|  |  [ Confirm Withdrawal ]  |   |                                      |   |
|  +--------------------------+   +--------------------------------------+   |
+----------------------------------------------------------------------------+
```

3. **Tick the verification checkbox** — this is mandatory and unlocks the rest of the form. By ticking it you are personally certifying that the photo, ID document, and signature on the Customer Card match the person standing at the counter.
4. Confirm the **Currency**.
5. Type the **Amount** the customer is withdrawing. The "Available" helper text shows what's in their account.
6. Count out the cash by denomination, entering each count.
7. Click **Confirm Withdrawal**.

### What the system does

The flow branches depending on the amount:

#### Standard withdrawal (under high-value threshold)

- Debits the customer's account immediately.
- Inserts a `branch_cash_transactions` row of type `Withdrawal`.
- Decreases your drawer's running cash position.
- Generates a receipt PDF.
- Returns the same success dialog as a deposit.
- **Hand the cash to the customer along with the printed receipt.** Ask them to sign on the receipt — the slip is filed locally.

#### High-value withdrawal (over threshold)

If the amount is above the configured high-value threshold (default USD 5,000), the system **does not debit immediately**. Instead it puts the transaction in a **pending approval** state and shows the supervisor approval dialog:

```
+----------------------------------------------------------------------------+
|  Supervisor Approval Required                                              |
|                                                                            |
|  ⚠ This withdrawal exceeds the high-value threshold. A supervisor must     |
|    enter their PIN to proceed.                                             |
|                                                                            |
|  Supervisor Username: [__________________]                                 |
|  Supervisor PIN:      [__________________]                                 |
|                                                                            |
|              [ Cancel ]              [    Approve    ]                     |
+----------------------------------------------------------------------------+
```

- Wave the branch supervisor over to the counter.
- They enter **their** username and PIN. **Do not type their PIN for them — they must do it themselves.**
- On valid PIN: the system finalises the withdrawal and posts it. Receipt is generated.
- On invalid PIN: the dialog stays open and shows an error. Three failures lock the supervisor account.

> **⚠ Important** — Never approve your own transaction. The system blocks it server-side, but more importantly it's a fundamental control violation. Always use a different supervisor.

### Errors and recovery

| Error message                              | Cause                                      | Fix                                          |
|--------------------------------------------|--------------------------------------------|----------------------------------------------|
| *"Insufficient funds"*                     | Balance < amount                           | Show customer their balance; reduce or abort |
| *"Account is not active or KYC incomplete"* | Frozen, suspended, or KYC tier 0          | Refer to branch manager                      |
| *"You must verify identity first"*         | Verification box not ticked                | Tick the box (after verifying!)              |
| *"Drawer has insufficient denominations"*  | Drawer doesn't physically have enough notes/coins of that face | Issue a different mix or decline the txn |

---

## 3.5 Reversing a Transaction

**Purpose:** Cancel a transaction you just processed by mistake (wrong amount, wrong customer, wrong currency). Reversal creates a **compensating** transaction — it does not delete the original.

**When you can reverse:**
- Same business day, same drawer session
- The transaction status is `completed` (not already reversed, not part of a chain)

**When you cannot reverse:**
- Drawer has been closed
- Original transaction is more than 24 hours old
- It's a third party's transaction (unless you're a branch manager)

### Steps

1. Go to the **Dashboard** (click the GoldBank Teller logo top left).
2. Scroll to the **Today's Transactions** table.

```
+----------------------------------------------------------------------------+
| Today's Transactions                                                       |
| Time   Type        Currency Amount    Depositor       Status   Actions     |
| 10:23  Deposit     USD      1,000.00  C. Mutasa       complete [P] [↶]    |
| 10:31  Withdrawal  USD        500.00  C. Mutasa       complete [P] [↶]    |
| 10:45  Deposit     ZWG     20,000.00  F. Chikwanha    complete [P] [↶]    |
+----------------------------------------------------------------------------+
```

3. Find the transaction you want to reverse.
4. Click the **↶ (reverse) icon** in the Actions column.
5. The reversal dialog opens:

```
+----------------------------------------------------------------------------+
|  Reverse Transaction                                                       |
|                                                                            |
|  ⚠ You are about to reverse a Deposit of USD 1,000.00. This will create    |
|    a compensating transaction and requires supervisor approval.            |
|                                                                            |
|  Reason:                                                                   |
|  [ Wrong customer — meant to deposit to ACC-5678            ]              |
|                                                                            |
|  Supervisor Username: [__________]                                         |
|  Supervisor PIN:      [__________]                                         |
|                                                                            |
|              [ Cancel ]              [   Reverse   ]                       |
+----------------------------------------------------------------------------+
```

6. Type a clear, specific **Reason**. "Mistake" is not acceptable. Example: *"Wrong customer — depositor meant ACC-5678 not ACC-1234"*.
7. Branch supervisor enters their username and PIN.
8. Click **Reverse**.

### What the system does

- Inserts a new `branch_cash_transactions` row of type `Reversal` with `direction = opposite of original`.
- Reverses the customer balance change.
- Marks the original transaction `status = reversed` and stores `reversed_at`.
- Both the original and the reversal show **strikethrough** in the dashboard.
- The drawer running balance returns to its pre-transaction state.

> **ℹ Tip** — Print receipts for **both** the original (annotated "REVERSED") and the reversal. File them stapled together.

---

## 3.6 Closing Your Drawer (End of Day)

**Purpose:** Reconcile your physical cash with the system's expected closing balance, surrender the cash to the vault, and produce the End-of-Day report.

**Prerequisites:**
- All your transactions for the day are completed (no pending approvals).
- You have physically counted all cash in your drawer.
- The vault manager is available to receive the surrender.

### Steps

1. Click **Drawer** in the top bar.
2. Switch to the **Close Drawer** tab.

```
+----------------------------------------------------------------------------+
| Cash Drawer  ✓ Drawer is currently OPEN · opened 08:32                     |
|                                                                            |
|   Open Drawer (disabled) | Close Drawer                                    |
|                                                                            |
|   Count and Close                                                          |
|                                                                            |
|   Currency:  [ USD ▼ ]                                                     |
|                                                                            |
|   (Denomination grid — count what's physically in the drawer)              |
|                                                                            |
|              [ Close Drawer with USD 4,250.00 ]                            |
+----------------------------------------------------------------------------+
```

3. Select the currency.
4. Count the physical cash one denomination at a time and enter each count.
5. The total at the bottom shows your **counted** amount.
6. Click **Close Drawer with USD X,XXX**.

### What the system does — happy path (no variance)

- Computes the **expected closing** = opening float + day's deposits − day's withdrawals.
- Compares your counted total to expected.
- If they match: closes the drawer, generates the EOD report PDF, shows the success dialog with **Print EOD Report** button.

### What the system does — variance detected

If your counted total doesn't match expected, the system stops and shows the **variance dialog**:

```
+----------------------------------------------------------------------------+
|  ⚠  Cash Variance Detected                                                 |
|                                                                            |
|  Your counted cash does not match the expected closing balance.            |
|  Please recount, or confirm to proceed and post the variance.              |
|                                                                            |
|   Currency  Expected   Counted   Variance                                  |
|   USD       4,250.00   4,247.50    -2.50    (red)                          |
|   ZWG      18,000.00  18,000.00     0.00                                   |
|                                                                            |
|              [ Recount ]              [ Confirm and Close ]                |
+----------------------------------------------------------------------------+
```

You have **two options**:

#### Option A — Recount (recommended)

1. Click **Recount**. The dialog closes and the denomination grid is cleared.
2. Physically recount your cash from scratch.
3. Enter the new counts and click **Close Drawer** again.
4. If it now matches, the drawer closes normally.

> **⚠ Important** — A small variance is usually a miscount, not a missing note. Always recount at least once before confirming a variance.

#### Option B — Confirm and Close (variance accepted)

1. If after recounting you're confident the variance is real, click **Confirm and Close**.
2. The system records the variance JSON on the drawer record.
3. The drawer closes with status `Closed` and a flag visible to your branch manager.
4. The branch manager will follow up with you to investigate.

### After successful close

```
+----------------------------------------------------------------------------+
|  ✓  Drawer Closed                                                          |
|                                                                            |
|     Your drawer has been closed successfully. Print the End-of-Day report  |
|     and sign it together with your supervisor.                             |
|                                                                            |
|        [ 🖨 Print EOD Report ]              [   Done   ]                    |
+----------------------------------------------------------------------------+
```

1. Click **Print EOD Report** — a one-page A4 PDF opens. **Print two copies.**
2. Sign both copies. Have your branch supervisor sign both.
3. File one in the branch's daily file. The other goes into the cash bag with the surrendered float.
4. Walk the cash + signed report to the vault manager (Part 4.4).

### Sample EOD report content

```
================================================================================
            GOLDBANK — END OF DAY TELLER REPORT
================================================================================
Branch:        Borrowdale Branch
Teller:        J. Doe
Business Date: 2026-04-08
Drawer ID:     a3f2-...-9c1d
Opened:        08:32:11
Closed:        17:18:44

OPENING FLOAT
  USD                 2,500.00
  ZWG                15,000.00

TRANSACTIONS
  Time   Ref          Type       Customer        Ccy  Amount     Status
  08:54  TXN-...0001  Deposit    C. Mutasa       USD    250.00  completed
  09:12  TXN-...0002  Withdrawal F. Chikwanha    USD    100.00  completed
  09:38  TXN-...0003  Deposit    R. Nyamupfukudza USD 1,500.00  completed
  ...   (chronological list of every transaction)

PER-CURRENCY TOTALS
  Currency  Deposits   Withdrawals  Net      Expected Close  Counted  Variance
  USD       4,250.00   2,500.00     1,750    4,250.00       4,250.00   0.00
  ZWG      12,000.00      0.00     12,000    27,000.00     27,000.00   0.00

SIGNATURES
  Teller:     ___________________   Date: __________
  Supervisor: ___________________   Date: __________

================================================================================
                    Generated 2026-04-08 17:18:45 UTC
================================================================================
```

---

# Part 4 — Vault Manager Workflows

The Vault module is accessed via the **Vault** button in the top bar (only visible to Vault Managers and Branch Managers).

```
+----------------------------------------------------------------------------+
| Vault - Borrowdale Branch       Last spot check: Balanced       [Refresh] |
|                                                                            |
| +-----------+ +-----------+ +-----------+                                  |
| | Vault USD | | Vault ZWG | |           |                                  |
| | 50,000    | |400,000    | |           |                                  |
| +-----------+ +-----------+ +-----------+                                  |
|                                                                            |
| [ Cash Injection ] [ Withdrawal to HQ ] [ Issue Float ] [ Receive Surrender ] |
| [ Run Spot Check ] [ Print Vault EOD Report ]                              |
|                                                                            |
| +--------------------------+   +--------------------------------------+    |
| | Denomination Stock       |   | Recent Movements                     |    |
| | Ccy Face Type Count Value|   | Time Type      Dir Ccy Amount        |    |
| | USD 100 Note  300 30000  |   | 09:00 CashInjection In USD 30000     |    |
| | USD  50 Note  200 10000  |   | 09:15 Issue Float Out USD  2500      |    |
| | ...                      |   | ...                                  |    |
| +--------------------------+   +--------------------------------------+    |
+----------------------------------------------------------------------------+
```

The dashboard auto-refreshes every 60 seconds. You can also click **Refresh**.

## 4.1 Cash Injection (Receiving Cash from HQ)

**Purpose:** Receive a Cash-In-Transit shipment from head office and book it into the vault.

**Prerequisites:**
- The CIT carrier has delivered a sealed bag with a manifest.
- The bag's seal numbers match the manifest.
- A witness (branch supervisor) is present.

### Steps

1. Click **Cash Injection**.
2. The movement dialog opens:

```
+----------------------------------------------------------------------------+
|  Cash Injection (CIT in)                                                   |
|                                                                            |
|  Currency:  [ USD ▼ ]                                                      |
|                                                                            |
|  (denomination grid)                                                       |
|                                                                            |
|  Notes: $100 [300]  $50 [200] ...    Total: USD 50,000.00                  |
|                                                                            |
|  Notes: [ CIT delivery #CIT-20260408-014, seal A-12345 OK             ]    |
|                                                                            |
|              [ Cancel ]              [ Confirm Movement ]                  |
+----------------------------------------------------------------------------+
```

3. Open the bag in the presence of the witness.
4. Count the cash one denomination at a time, entering each count.
5. The total must match the manifest exactly.
6. In **Notes**, record the CIT reference and seal numbers.
7. Click **Confirm Movement**.

### What the system does

- Inserts a `vault_movements` row of type `CashInjection`, direction `In`.
- Atomically increments `vault_denomination_stock` row counts for every denomination in the breakdown (transactional — if any step fails the whole movement rolls back).
- Returns to the dashboard, which now shows the new totals.

> **⚠ Important** — Never sign for a CIT bag without opening and counting it. The seal can be intact but the contents wrong; you are signing for what's inside, not what's claimed.

## 4.2 Withdrawal to HQ (Shipping Surplus)

**Purpose:** Ship excess cash back to head office to keep the branch within its insured limit and to free up capital.

### Steps

1. Click **Withdrawal to HQ**.
2. Same dialog as Cash Injection, but the direction is **Out** (the system handles this internally based on the type).
3. Count the cash you are putting into the bag.
4. Enter the counts.
5. Notes field: record destination, courier waybill, seal numbers.
6. Click **Confirm Movement**.

### What the system does

- Inserts a `vault_movements` row of type `CashWithdrawal`, direction `Out`.
- Atomically **decrements** stock counts.
- **If any denomination would go negative, the whole movement is rejected.** The dialog will show an error and the bag must not be sealed.

## 4.3 Issuing Float to a Teller

**Purpose:** Hand a teller their morning starting float, recorded as both a vault outflow and a teller inflow.

**Prerequisites:**
- Teller has presented their cash bag and an empty drawer.
- Teller has not yet opened a drawer for the day in the system.

### Steps

1. Click **Issue Float to Teller**.
2. Currency dropdown — pick the currency.
3. Denomination grid — count out the cash you are about to hand to the teller.
4. Notes — record the teller's name and any reference.
5. Click **Confirm Movement**.

### What the system does

- Inserts a `vault_movements` row of type `DrawerIssue`, direction `Out`.
- Decrements vault stock for those denominations.
- **The teller must then open their drawer** in the system using the same denomination breakdown (Part 3.1). The two records together form a complete audit trail.

## 4.4 Receiving Surrender from a Teller

**Purpose:** Take in a teller's end-of-day surrender, count it against their EOD report, and update the vault stock.

**Prerequisites:**
- Teller has closed their drawer.
- Teller has handed you a signed EOD report and the cash bag.

### Steps

1. Compare the cash in the bag to the **Counted** column on the teller's EOD report.
2. Click **Receive Surrender**.
3. Currency, denomination grid — count the cash, denomination by denomination.
4. The totals must match the teller's EOD report.
5. Notes — reference the drawer ID from the EOD report.
6. Click **Confirm Movement**.

### What the system does

- Inserts a `vault_movements` row of type `DrawerSurrender`, direction `In`.
- Increments vault stock atomically.
- The teller's drawer is now fully reconciled.

> **⚠ Important** — If the cash in the bag doesn't match the teller's EOD report, **stop the surrender process** and call the branch manager immediately. Do not adjust the count silently.

## 4.5 Running a Spot Check

**Purpose:** Independent physical verification that the vault stock the system shows matches the cash actually in the vault. Should be done **at least daily** at random times. The branch manager may also call an unscheduled spot check.

**Prerequisites:**
- Witness (branch manager or other supervisor) physically present.
- Vault is closed and accessible to no one but the two of you.

### Steps

1. Click **Run Spot Check** on the vault dashboard.
2. The spot check dialog opens with the **Expected** column pre-filled from the system's current stock:

```
+----------------------------------------------------------------------------+
|  Vault Spot Check                                                          |
|                                                                            |
|  ℹ Physically count each denomination in the vault and enter the actual    |
|    count below. A distinct witness must approve.                           |
|                                                                            |
|  Currency  Face   Expected  Counted                                        |
|  USD       100      300     [ 300 ]                                        |
|  USD        50      200     [ 200 ]                                        |
|  USD        20      150     [ 149 ]                                        |
|  USD        10      120     [ 120 ]                                        |
|  ...                                                                       |
|                                                                            |
|  Witness User ID: [______________________]                                 |
|                                                                            |
|  [ ☐ Confirm variance and post adjustment ]                                |
|                                                                            |
|              [ Close ]              [    Submit    ]                       |
+----------------------------------------------------------------------------+
```

3. **Both of you physically count** each denomination together. Enter the actual counts.
4. Type the witness's user ID. The system rejects the spot check if the witness is the same person submitting it.
5. Click **Submit**.

### What the system does — happy path (no variance)

- Computes variance = counted − expected, per denomination.
- All variances are zero → records a `vault_spot_checks` row with `has_variance = false`, updates `vaults.last_spot_check_result = Balanced`.
- Dashboard updates the chip to **Balanced**.

### What the system does — variance detected

- The system returns a 409 with the variance breakdown.
- You see a warning: *"Variance found — toggle 'Confirm variance' and re-submit to post adjustment."*
- **Step 1 — recount.** Just like the teller's drawer close, always recount before accepting a variance.
- **Step 2 — investigate.** Check the recent movement log. Look for movements you don't remember.
- **Step 3 — escalate.** If you cannot reconcile, call the branch manager **before** posting the adjustment.
- **Step 4 — post the adjustment.** Once the branch manager has signed off, tick **Confirm variance and post adjustment** and click Submit.

The system then:
- Inserts a synthetic `vault_movements` row of type `SpotCheckAdjust` with the net difference per denomination.
- Atomically updates the stock to match the counted amount.
- Records the spot check with `has_variance = true` and links to the adjustment movement.
- Updates `vaults.last_spot_check_result = Variance`.
- The dashboard chip turns orange.

> **⚠ Important** — Never tick "Confirm variance" without a supervisor signature. The system records who confirmed; this is one of the most heavily audited actions in the platform.

## 4.6 Printing the Vault EOD Report

**Purpose:** Daily one-page summary of every vault movement, current stock, and any spot-check activity. Required for branch records and audit.

### Steps

1. At end of day (after the last teller has surrendered) click **Print Vault EOD Report**.
2. A new tab opens with the PDF.
3. Print **two copies**.
4. Sign both — vault manager and branch supervisor.
5. File one in the branch daily file; one is kept by the vault manager.

### Sample vault EOD report

A real sample is in [`docs/training/samples/sample-vault-eod.pdf`](samples/sample-vault-eod.pdf). The structure is:

```
================================================================================
                    BRANCH VAULT — END OF DAY
================================================================================
Borrowdale Branch · Vault - Borrowdale Branch
Business date: 2026-04-08

CLOSING STOCK
  Currency  Face    Type   Count   Value
  USD       100.00  Note     300    30,000.00
  USD        50.00  Note     200    10,000.00
  USD        20.00  Note     150     3,000.00
  USD        10.00  Note     120     1,200.00
  USD         5.00  Note     500     2,500.00
  USD         1.00  Note    1000     1,000.00
  ZWG       200.00  Note     400    80,000.00
  ZWG       100.00  Note     500    50,000.00
  ...

MOVEMENTS
  Time   Type             Dir  Ccy  Amount       Notes
  09:00  CashInjection    In   USD  30,000.00    CIT-014, seal A-12345 OK
  09:15  DrawerIssue      Out  USD   2,500.00    Teller J. Doe morning float
  09:18  DrawerIssue      Out  USD   2,500.00    Teller P. Mwale morning float
  17:25  DrawerSurrender  In   USD   4,250.00    Drawer a3f2-...-9c1d
  17:32  DrawerSurrender  In   USD   3,890.00    Drawer b8c1-...-2e4f
  17:50  SpotCheckAdjust  Out  USD       2.50    Spot-check adjustment

SPOT CHECKS
  17:48 · Variance · adj movement: 7c1f-...-3a2e

SIGNATURES
  Vault Manager: _______________________  Date: __________
  Branch Supervisor: ___________________  Date: __________

================================================================================
```

---

# Part 5 — Branch Manager Workflows

The Branch Manager has access to **everything** the teller and vault manager have, plus a **Supervisor Dashboard** at the home page.

## 5.1 The Supervisor Dashboard

When you log in as a branch manager you land on the supervisor dashboard instead of the teller dashboard:

```
+----------------------------------------------------------------------------+
| Branch Supervisor Dashboard            Last refresh: 17:23 [Refresh]       |
|                                                                            |
| +----------------+ +----------------+ +----------------+ +----------------+|
| | Active Tellers | | Pending        | | Today's Volume | | Variance Alerts||
| |       4        | | Approvals      | |   USD 24,500   | |       1        ||
| |                | |       2        | |   ZWG 180,000  | |                ||
| +----------------+ +----------------+ +----------------+ +----------------+|
|                                                                            |
| Active Tellers                                                             |
|   Teller          Drawer     Opened    Currency Balances                   |
|   J. Doe          a3f2-...   08:32     USD 4,150 / ZWG 27,000              |
|   P. Mwale        b8c1-...   08:35     USD 3,800                           |
|   ...                                                                      |
|                                                                            |
| Pending Approvals                                                          |
|   Time  Teller     Customer     Ccy  Amount     Reason       Action        |
|   16:14 J. Doe     C. Mutasa    USD  6,500.00   High value   [Approve]     |
|   16:20 P. Mwale   F. Chikwanha USD  8,000.00   High value   [Approve]     |
|                                                                            |
| Variance Alerts                                                            |
|   Teller    Closed   Currency  Expected   Counted   Variance               |
|   T. Banda  17:01    USD       3,200.00   3,197.50    -2.50                |
|                                                                            |
+----------------------------------------------------------------------------+
```

This page **auto-refreshes every 30 seconds** and is the primary view you should keep open during your shift.

### Acting on each tile

- **Active Tellers** — See who is on shift. Click a row to drill into their drawer details.
- **Pending Approvals** — Click **Approve** on a row to open the approval modal (same as the in-line approval at the teller counter, but you can also do it remotely from your office).
- **Today's Volume** — Real-time deposits − withdrawals per currency.
- **Variance Alerts** — Tellers who closed with a non-zero variance. Click to drill in and investigate.

## 5.2 Approving a High-Value Withdrawal

When a teller initiates a withdrawal above the threshold, two things happen:

1. The teller's screen shows the supervisor PIN dialog.
2. A row appears on your **Pending Approvals** tile.

You have two ways to approve:

### Option A — At the counter

The teller calls you over. You walk to the counter, watch the customer hand over the ID, then enter your username and PIN directly into the dialog on the teller's screen.

> This is the **preferred path** because you see the customer in person.

### Option B — Remote from your office (use sparingly)

1. From the Supervisor Dashboard, click **Approve** on the row.
2. Same dialog opens.
3. Confirm the teller, customer, amount.
4. Enter your username and PIN.
5. Click **Approve**.

> **⚠ Important** — Never approve remotely without first phoning the teller to confirm a real customer is at the counter. Approval-fraud rings exist.

## 5.3 Reversing Another Teller's Transaction

Tellers can only reverse their own same-shift transactions. As a branch manager you can reverse **any teller's** transaction in the current business day, including ones from a closed drawer (with caveats).

### Steps

1. Search for the customer (Part 3.2).
2. Open the **Transactions** tab on the Customer Card.
3. Find the row to reverse.
4. Click the **↶** icon (only visible to managers, not tellers, on someone else's transactions).
5. Same reversal dialog as Part 3.5.

The reason field is mandatory and **must include the operational rationale**, e.g. *"Customer dispute resolved in their favour — case BMD-20260408-002, see attached email"*.

## 5.4 Resolving a Drawer Variance

When a teller closes their drawer with a variance, an alert appears on your dashboard. Resolve it before the day ends:

1. Click the variance alert row to open the drawer detail.
2. Review:
   - The **expected** vs **counted** amounts per currency
   - The day's transaction list (any patterns?)
   - The teller's notes
3. Talk to the teller. Have them recount one more time if cash is still in the bag.
4. **If the variance is real and explained:** record the reason in your supervisor log book, sign the EOD report, and let the surrender go ahead.
5. **If the variance is unexplained:** initiate a cash incident report (paper form, head office). The teller's drawer remains flagged in the system for audit.

## 5.5 Authorising a Spot-Check Variance

If the vault manager runs a spot check and finds a variance, they should call you **before** ticking "Confirm variance and post adjustment". Your job:

1. Walk to the vault.
2. Recount the disputed denominations together with the vault manager.
3. Check the movement log for anomalies (`GET /vaults/{id}/movements`).
4. **If recount resolves it:** the vault manager re-submits the spot check with the correct counts.
5. **If the variance is real:** decide whether to:
   - Post the adjustment (signs off the system) — you witness the vault manager tick the box and submit.
   - **Or** freeze the vault and escalate to head office security.

In either case, file a **Vault Variance Incident Report** by end of day.

## 5.6 End-of-Day Branch Sign-Off

At the close of business, before the last person leaves the branch:

1. Confirm **all tellers have closed their drawers**. Check the Active Tellers tile shows zero.
2. Confirm **all variance alerts are resolved or escalated**.
3. Confirm the **vault manager has run a spot check** for the day.
4. Confirm the **vault EOD report has been printed and signed** (one copy in the daily file, one with the vault manager).
5. Sign the **Branch Day-End Sign-Off** form (paper) which lists every teller, their EOD totals, and any incidents.

> **ℹ Tip** — Keep the supervisor dashboard open in a second monitor for the entire day. Variances and pending approvals are time-sensitive.

---

# Part 6 — Common Errors and Recovery Cookbook

## 6.1 "Drawer is already open"

You try to open a drawer and the system says one is already open in your name.

**Cause:** Yesterday's drawer was never closed, OR the system crashed mid-close.

**Fix:**
1. Find the open drawer (it will be visible on your dashboard).
2. Close it normally (Part 3.6) — count whatever cash is currently in your physical drawer.
3. The system will compute an automatic variance based on yesterday's expected closing. **Recount carefully.**
4. Then open today's drawer with the same cash.

## 6.2 "Customer not found" but they have an account

**Cause:** Search term has a typo or matches no field exactly.

**Fix:**
- Try just the last 4 digits of their phone.
- Try their first name only.
- Try their account number from a previous receipt.
- Last resort: ask them for their national ID and search by that.

## 6.3 The receipt printer didn't print

**Cause:** Network issue, out of paper, jam.

**Fix:**
1. The receipt is **already saved on the server**. The transaction is complete.
2. Go to the **Today's Transactions** table on the dashboard.
3. Click the **Print** icon (🖨) next to the row to re-print.
4. If still no luck, take a screenshot of the success dialog and write the reference number on a manual receipt slip.

## 6.4 The system shows offline banner but everyone else is fine

**Cause:** Your specific machine has lost connectivity to the gateway.

**Fix:**
1. Don't reload — wait 60 seconds.
2. Check the network cable / wifi.
3. Try to ping the gateway hostname from a command prompt.
4. If still offline after 5 minutes, log out, log into another teller's machine to finish urgent transactions, and call IT.

## 6.5 "Insufficient funds" on a customer who clearly has money

**Cause:** The customer has multiple accounts and you are looking at the wrong one. OR a recent transaction is still pending and has reserved the funds.

**Fix:**
1. Open the Customer Card and look at **Balances** — make sure the currency you're withdrawing matches.
2. Open the **Transactions** tab and check for any pending rows.
3. If a pending withdrawal is blocking funds, identify it (it'll have a "pending" chip) and either approve or reverse it before retrying.

## 6.6 You closed the wrong drawer

**Cause:** Two drawers open under your manager and you closed the other teller's.

**Fix:**
1. Stop. Do not open a new drawer.
2. Call the branch manager.
3. The manager has a `/drawer/reopen` admin path (escalation only) or, in the worst case, the database can be patched to set `status='Open'` again — but this is a last resort and creates an audit trail.
4. **Prevention:** always confirm the teller name on the drawer screen header before clicking Close.

---

# Part 7 — Daily Checklists

## 7.1 Teller — Start of day

- [ ] Logged in to bank-teller
- [ ] Branch shown top-right is correct
- [ ] Collected float from vault manager
- [ ] Counted float against vault manager's manifest
- [ ] Opened drawer with the same denomination breakdown
- [ ] Confirmed Drawer Open chip is green
- [ ] Receipt printer has paper

## 7.2 Teller — End of day

- [ ] All transactions completed (no pending approvals on dashboard)
- [ ] Counted all cash in drawer one denomination at a time
- [ ] Closed drawer in system; matched expected (or escalated variance)
- [ ] Printed two EOD reports
- [ ] Both signed by self and supervisor
- [ ] Cash bagged and labelled
- [ ] Cash + signed EOD walked to vault manager
- [ ] Logged out of bank-teller

## 7.3 Vault Manager — Daily

- [ ] Vault unlocked in presence of branch manager
- [ ] Received any CIT shipments (bags counted, not just sealed)
- [ ] Issued morning floats to tellers, recorded each as DrawerIssue
- [ ] Available throughout the day for unscheduled spot checks or top-ups
- [ ] Received all teller surrenders, reconciled against EOD reports
- [ ] Ran end-of-day spot check with witness
- [ ] Printed and signed vault EOD report
- [ ] Confirmed branch manager has signed off
- [ ] Vault locked in presence of branch manager

## 7.4 Branch Manager — Daily

- [ ] Logged in, supervisor dashboard open on second monitor
- [ ] Reviewed yesterday's incident log
- [ ] Authorised any high-value withdrawals as they arose
- [ ] Investigated any variance alerts before EOD
- [ ] Witnessed vault spot check
- [ ] Confirmed every teller's drawer is closed and balanced
- [ ] Signed branch day-end sign-off form
- [ ] Filed paper records in daily file

---

# Part 8 — Glossary

| Term                          | Meaning                                                                   |
|-------------------------------|---------------------------------------------------------------------------|
| **Drawer** / **Cash drawer**   | The set of physical cash a teller is responsible for during a single shift |
| **Float**                      | The cash a teller starts a shift with                                     |
| **Surrender**                  | Returning the drawer cash to the vault at end of day                      |
| **CIT**                        | Cash-in-transit — armed transport of cash between branches and HQ         |
| **Vault**                      | The branch's central cash store, separate from teller drawers             |
| **Spot check**                 | An unscheduled physical count of vault cash for control purposes          |
| **Variance**                   | A difference between expected and counted cash                            |
| **Reversal**                   | A compensating transaction that cancels the effect of an earlier one      |
| **High-value threshold**       | The amount above which a withdrawal needs supervisor approval (default USD 5,000) |
| **EOD report**                 | End-of-Day report — the signed PDF a teller or vault produces on close    |
| **KYC tier**                   | Customer's verification level; determines transaction limits              |

---

# Part 9 — Quick Reference Card

Print this section and pin it to the wall by every teller workstation.

```
+----------------------------------------------------------------------------+
|  GOLDBANK TELLER QUICK REFERENCE                                            |
|                                                                            |
|  START OF DAY                  END OF DAY                                  |
|  1. Login                       1. Close all pending txns                  |
|  2. Drawer → Open Drawer        2. Drawer → Close Drawer                   |
|  3. Count float, enter          3. Count cash, enter                       |
|  4. Confirm                     4. Recount on variance                     |
|  5. Print printer test          5. Print EOD report (x2)                   |
|                                 6. Sign + supervisor sign                  |
|                                 7. Walk cash to vault                      |
|                                                                            |
|  WITHDRAWAL — VERIFY 3 THINGS:                                             |
|    1. Photo on screen = person at counter                                  |
|    2. ID document numbers match                                            |
|    3. Signature on slip = signature on screen                              |
|    Then tick the verification box and process.                             |
|                                                                            |
|  REVERSAL — only your own, only same shift, supervisor PIN required.       |
|                                                                            |
|  EMERGENCY                                                                 |
|    Offline banner → wait, do not retry                                     |
|    Variance        → recount once before confirming                        |
|    Receipt no print→ reprint from dashboard                                |
|    Locked out      → re-enter password, 5 fails = call supervisor          |
+----------------------------------------------------------------------------+
```

---

# Appendix A — Sample API responses

For tellers and supervisors who want to understand what the system stores. None of this is needed for daily operation but it's useful when troubleshooting.

### Customer card (`GET /api/teller/customers/{id}/card`)
```json
{
  "accountId": "ACC-1234",
  "fullName": "Chiedza Mutasa",
  "phone": "+26377555 1234",
  "kycLevel": 2,
  "status": "active",
  "balances": [
    { "currency": "USD", "balance": 1250.00, "accountIdRaw": "..." },
    { "currency": "ZWG", "balance": 12000.00, "accountIdRaw": "..." }
  ],
  "selfieImageUrl": "data:image/jpeg;base64,...",
  "idImageUrl": "data:image/jpeg;base64,...",
  "signatureImageUrl": "data:image/png;base64,...",
  "signatureVerifiedBy": "teller",
  "signatureVerifiedAt": "2026-04-08T10:00:00Z"
}
```

### Drawer close with variance (`POST /api/teller/drawer/close`, 409)
```json
{
  "error": "drawer.variance_detected",
  "message": "Closing balance does not match expected. Recount or confirm to proceed.",
  "expected": { "USD": 4250.00, "ZWG": 18000.00 },
  "counted":  { "USD": 4247.50, "ZWG": 18000.00 },
  "variance": { "USD":   -2.50, "ZWG":     0.00 }
}
```

To proceed, resubmit with `confirmVariance: true`.

### Vault detail (`GET /api/teller/vaults/{id}`)
```json
{
  "id": "ded3...",
  "name": "Vault - Borrowdale Branch",
  "lastSpotCheckResult": "Balanced",
  "stock": [
    { "currency": "USD", "face": 100, "type": "Note", "count": 300, "value": 30000 },
    ...
  ],
  "totalsByCurrency": { "USD": 50000.00, "ZWG": 400000.00 }
}
```

---

# Appendix B — Sample Reports

The following sample reports were generated by the **live GoldBank gateway** against the development database and reflect the exact format you'll see in production. Each was produced by a real API call inside a real drawer session.

| File | Format | Description |
|---|---|---|
| [`samples/sample-deposit-receipt.pdf`](samples/sample-deposit-receipt.pdf) | A6, 1 page | Cash deposit receipt — USD 250.00 deposited by Chiedza Mutasa to ACC-0004. Includes denomination breakdown, QR verification code, and reference `DEP-20260408191135910`. |
| [`samples/sample-withdrawal-receipt.pdf`](samples/sample-withdrawal-receipt.pdf) | A6, 1 page | Cash withdrawal receipt — USD 100.00 paid out, two USD-50 notes. Reference `WDR-20260408191154179`. |
| [`samples/sample-teller-eod.pdf`](samples/sample-teller-eod.pdf) | A4, 1 page | Teller End-of-Day report for the drawer that processed the two transactions above. Shows opening float (USD 2,500), all day's transactions in chronological order, expected vs counted closing (USD 2,650), and signature blocks. |
| [`samples/sample-vault-eod.pdf`](samples/sample-vault-eod.pdf) | A4, 1 page | Branch vault End-of-Day report — closing denomination stock, every vault movement of the day (CashInjection, DrawerIssue, DrawerSurrender, SpotCheckAdjust), and signature blocks. |

**These are not mockups.** Open them in any PDF viewer to see the actual layout, fonts, and content the system produces.

---

**End of Manual**

For amendments, contact: branch.operations@goldbank.local
Document maintained by: Branch Operations / Training
