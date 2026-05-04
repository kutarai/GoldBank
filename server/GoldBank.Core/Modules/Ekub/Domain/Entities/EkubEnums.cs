namespace GoldBank.Core.Modules.Ekub.Domain.Entities;

/// <summary>
/// Forming   — group has been created but does not yet have the 3 accepted members
///             required to start collecting contributions.
/// Active    — quorum reached; contributions and (in v2) loans may be recorded.
/// Closed    — group has been wound up; no further contributions or loans accepted.
/// Suspended — frozen by an admin / chairman pending dispute resolution.
/// </summary>
public enum EkubGroupStatus { Forming, Active, Suspended, Closed }

/// <summary>
/// Roles a member may hold within a single Ekub group. Each group must always have
/// exactly one Chairman, one Treasurer, and one Secretary; remaining people are
/// plain Members. Roles are independent of accept order — the creator is the
/// Chairman by default; Treasurer + Secretary are nominated by the Chairman.
/// </summary>
public enum EkubMemberRole { Chairman, Treasurer, Secretary, Member }

/// <summary>
/// Pending  — invitation sent, waiting on the invitee.
/// Accepted — invitee accepted; a corresponding EkubMembership row has been created.
/// Declined — invitee declined.
/// Revoked  — chairman/secretary cancelled the invitation before a response.
/// Expired  — auto-expired after the configured TTL.
/// </summary>
public enum EkubInvitationStatus { Pending, Accepted, Declined, Revoked, Expired }

/// <summary>
/// Pending   — member submitted the contribution (cash brought in); not yet
///             counted in the group pot until treasurer confirms.
/// Confirmed — treasurer confirmed receipt; contribution counted in the pot.
/// Rejected  — treasurer rejected (e.g. amount mismatch); not counted.
/// </summary>
public enum EkubContributionStatus { Pending, Confirmed, Rejected }

/// <summary>
/// Type of ledger movement against the group pot. Contributions add; fees and
/// (in v2) loan disbursements deduct; loan repayments add back including
/// interest which is distributed pro-rata to members on read.
/// </summary>
public enum EkubLedgerEntryKind { Contribution, MonthlyFee, LoanDisbursement, LoanRepaymentPrincipal, LoanRepaymentInterest }

/// <summary>
/// Voting        — open for member votes; resolves once &gt;50% of non-borrower
///                  active members have approved (advances to AwaitingTreasurer)
///                  or rejected (advances to Rejected).
/// AwaitingTreasurer — members approved; treasurer must confirm before
///                  the loan is disbursed.
/// Disbursed     — funds released; principal owed; status auto-progresses to
///                  Repaying once the first repayment is recorded.
/// Repaying      — partial repayments received; outstanding &gt; 0.
/// Closed        — fully repaid (outstanding == 0).
/// Rejected      — vote failed or treasurer rejected.
/// Defaulted     — admin-marked default (out-of-scope for v2 automation).
/// </summary>
public enum EkubLoanStatus { Voting, AwaitingTreasurer, Disbursed, Repaying, Closed, Rejected, Defaulted }
