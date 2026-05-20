# Retrospective: GoldBank Sprints 1-9

**Date:** 2026-03-24
**Facilitator:** SM (Scrum Master)
**Scope:** Full original plan — Sprints 1 through 9 (Feb 24 - Aug 31, 2026)
**Format:** Post-release retrospective covering the entire 9-sprint delivery cycle

---

## 1. Executive Summary

The team delivered **all 83 stories (396 points)** across **9 three-week sprints**, completing the original GoldBank scope on schedule by end of August 2026. The system went from zero to a production-ready multi-tenant banking platform with mobile payments, NFC, QR, P2P transfers, agent network, payment switch integration, admin portal, and issuer-side card processing.

Post-plan development has continued with 4 new epics (EPIC-016 through EPIC-019), indicating the platform is now in a growth/enhancement phase.

| Metric | Planned | Actual |
|--------|---------|--------|
| Stories | 83 | 83 completed |
| Points | 396 | 396 completed |
| Sprints | 9 | 9 |
| Timeline | Feb 24 - Sep 2026 | Feb 24 - Aug 31, 2026 |
| Delivery Rate | 100% | 100% committed = completed |

---

## 2. What Went Well

### 2.1 Perfect Delivery Record
Every sprint achieved 100% of committed points — zero spillover, zero incomplete stories across all 9 sprints. This is remarkable for a greenfield banking platform and reflects either excellent estimation discipline or conservative commitments (see Section 3).

### 2.2 Foundation-First Approach Paid Off
Sprint 1 front-loaded infrastructure (scaffolding, Docker, DB, gRPC, CI/CD, messaging, monitoring) at 62 points — the highest velocity sprint. This investment unblocked all subsequent sprints and prevented infrastructure debt from accumulating.

### 2.3 Logical Dependency Ordering
The sprint sequence respected dependencies naturally:
- **S1-S2:** Infrastructure + user onboarding (accounts before payments)
- **S3-S4:** Payment capabilities (NFC/QR before P2P/agent)
- **S5-S6:** External integrations (switch + terminal after core payments)
- **S7-S8:** Admin/reporting/hardening (observability after features)
- **S9:** Issuer-side card processing (after switch integration)

No sprint was blocked waiting on a prior sprint's incomplete work.

### 2.4 Broad Feature Coverage
The platform covers an impressive breadth for 9 sprints:
- User registration with OTP + KYC (ID upload, selfie match)
- NFC contactless + EMV QR payments
- P2P domestic and cross-border transfers
- Cash-in/cash-out agent network with commissions
- Bill payments with provider registry
- ISO 8583 + ISO 20022 payment switch adapters
- Terminal management over MQTT
- Multi-tenant white-label support
- Full admin portal with RBAC, KYC review, dispute management
- Complete reporting suite (transactions, revenue, reconciliation)
- Fraud detection, security audit, pilot deployment prep
- Issuer-side card transaction processing

### 2.5 Clean Commit History
39 well-structured commits with conventional commit format (`feat(scope): description`). Each commit is atomic and traceable to a feature area.

---

## 3. What Could Be Improved

### 3.1 Significant Capacity Underutilization
The team's capacity was 144 points/sprint (1,296 total), but only **396 points were committed** across 9 sprints — **30.6% utilization**.

| Sprint | Committed | Capacity | Utilization |
|--------|-----------|----------|-------------|
| 1 | 62 | 144 | 43% |
| 2 | 49 | 144 | 34% |
| 3 | 45 | 144 | 31% |
| 4 | 47 | 144 | 33% |
| 5 | 47 | 144 | 33% |
| 6 | 29 | 144 | **20%** |
| 7 | 58 | 144 | 40% |
| 8 | 25 | 144 | **17%** |
| 9 | 34 | 144 | 24% |

**Possible explanations:**
- Story points underestimate actual complexity (points too small for scope)
- Significant untracked work (spikes, bugs, tech debt, meetings, reviews)
- Conservative capacity model (80% buffer may be too aggressive on top of 6-hour days)
- Team ramping or context-switching overhead not captured

**Recommendation:** Either recalibrate story point estimation, reduce the capacity model, or track unplanned work explicitly so the gap is understood. A 70% gap is too large to leave unexplained.

### 3.2 Velocity Variance Is High
Velocity ranged from **25 (Sprint 8) to 62 (Sprint 1)** — a 2.5x swing. The rolling average of 44 masks this instability.

```
Sprint:  1    2    3    4    5    6    7    8    9
Points: 62   49   45   47   47   29   58   25   34
        ▓▓▓  ▓▓   ▓▓   ▓▓   ▓▓   ▓    ▓▓▓  ▓    ▓▓
```

The downward trend in Sprints 6 and 8 may reflect:
- Sprint 6: Terminal management + white-label = fewer but more complex integration stories
- Sprint 8: Hardening/security/NFR validation = effort-heavy but low-point stories

**Recommendation:** Consider using T-shirt sizing or calibration sessions to ensure 1 point of security audit ≈ 1 point of CRUD screen. Alternatively, separate capacity for hardening vs feature sprints.

### 3.3 Sprint Status Tracking Is Now Stale
The `sprint-status.yaml` file stops at Sprint 9, yet active development has continued through what appears to be Sprints 15-17 (per commit messages and plan files referencing Sprint 15, 16, 17). This creates a tracking blind spot for:
- New epics (016-019) and their stories
- Current sprint velocity
- Remaining backlog visibility

**Recommendation:** Update sprint-status.yaml to include Sprints 10+ immediately. Establish a ritual to update it at each sprint boundary.

### 3.4 No Explicit Testing/QA Signal in Sprint Data
While STORY-074 (Performance Testing) and STORY-075 (Security Audit) exist, there is no visible test coverage tracking, QA acceptance, or automated test pass/fail data tied to sprint completion. The commit history shows feature commits but no test-specific commits.

**Recommendation:** Add test coverage metrics to the sprint status. Consider a "definition of done" that requires test evidence per story.

### 3.5 No Assignments Tracked
Every story has `assigned_to: null`. While the team may track assignments elsewhere, having this data in sprint-status would enable:
- Load balancing analysis
- Knowledge-silo detection
- Bus factor assessment

---

## 4. Key Risks Identified

### 4.1 Scope Creep Without Updated Planning
Four new epics have been added post-plan:

| Epic | Scope | Est. Points |
|------|-------|-------------|
| EPIC-016 | SynergySwitch Integration | 47 |
| EPIC-017 | AI Vision Services (Qwen3-VL) | 69 |
| EPIC-018 | Mobile UI Enhancements | TBD |
| EPIC-019 | Admin Portal Enhancements | TBD |

STORY-093 (Dual-Currency Accounts) is also in backlog. This is significant new scope without an updated sprint plan, capacity model, or target date.

**Risk Level: Medium** — Work is progressing but without formal planning guardrails.

### 4.2 Architecture Complexity Growing
The original 9-component architecture now includes:
- AI inference layer (Ollama + Qwen3-VL + ArcFace)
- SynergySwitch payment switch (standalone .NET app in monorepo)
- Expanded mobile app (Kotlin Multiplatform with AI features)
- Enhanced admin portal (Blazor with 7+ RBAC roles)

Each addition increases integration surface area, deployment complexity, and testing burden.

### 4.3 Single-Branch Development
All 39 commits are on a single feature branch with no evidence of feature branches, PR reviews, or branch protection. For a banking platform, this presents:
- No peer review gate for security-sensitive code
- Risk of regression without isolated feature development
- Compliance concerns (audit trail of reviews)

---

## 5. Action Items

| # | Action | Owner | Priority |
|---|--------|-------|----------|
| 1 | **Update sprint-status.yaml** to include Sprints 10+ with current epic work | SM | High |
| 2 | **Recalibrate capacity model** — investigate the 70% utilization gap; either adjust capacity down or track unplanned work | SM | High |
| 3 | **Create updated sprint plan** for EPICs 016-019 with target dates | SM + PM | High |
| 4 | **Establish PR review process** — at minimum for security-sensitive modules (HSM, switching, auth, payments) | Tech Lead | High |
| 5 | **Add test coverage tracking** to sprint status and definition of done | QA | Medium |
| 6 | **Normalize velocity** — run a calibration session on point estimation for upcoming sprints | SM | Medium |
| 7 | **Track assignments** in sprint-status.yaml to detect knowledge silos | SM | Low |
| 8 | **Document STORY-093** impact — dual-currency accounts affects registration, NFC, P2P, reconciliation; needs cross-cutting impact analysis | Architect | Medium |

---

## 6. Metrics Summary

### Delivery
- **Total Stories Delivered:** 83/83 (100%)
- **Total Points Delivered:** 396/396 (100%)
- **Sprints with Spillover:** 0/9
- **Average Velocity:** 44 pts/sprint
- **Velocity Std Dev:** ~12.4 pts (high variance)

### Capacity
- **Total Capacity (9 sprints):** 1,296 points
- **Total Committed:** 396 points (30.6%)
- **Capacity Gap:** 900 points unaccounted

### Timeline
- **Planned Start:** 2026-02-24
- **Planned End:** ~September 2026
- **Actual End (Sprint 9):** 2026-08-31
- **On Schedule:** Yes (slightly ahead)

### Post-Plan Growth
- **New Epics Added:** 4 (EPIC-016 through EPIC-019)
- **New Stories in Backlog:** STORY-093+
- **Commits Since Sprint 9:** 38 (all on current branch)

---

## 7. Overall Assessment

**Grade: B+**

The team delivered a complete, ambitious banking platform on time with zero spillover. The foundation-first approach and clean dependency ordering were textbook execution. However, the significant capacity gap, lack of sprint tracking for post-plan work, absence of test evidence, and single-branch workflow prevent a top grade. The immediate priority is bringing planning discipline back to the post-Sprint-9 work before the scope of EPICs 016-019 outpaces the team's ability to track and manage it.

**Bottom Line:** Excellent delivery engine, needs better observability of its own process.
