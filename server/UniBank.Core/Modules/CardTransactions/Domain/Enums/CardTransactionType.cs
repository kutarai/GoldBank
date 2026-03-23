namespace UniBank.Core.Modules.CardTransactions.Domain.Enums;

/// <summary>
/// Types of card transactions processed by the issuing bank (EPIC-015).
/// </summary>
public enum CardTransactionType
{
    /// <summary>Purchase at a merchant who is a client of this bank.</summary>
    OnUsPurchase,

    /// <summary>Purchase at a merchant who is not a client of this bank.</summary>
    OffUsPurchase,

    /// <summary>Deposit at a merchant who is a client of this bank.</summary>
    OnUsDeposit,

    /// <summary>Deposit at a merchant who is not a client of this bank.</summary>
    OffUsDeposit,

    /// <summary>Balance enquiry at an on-us terminal (merchant is a client of this bank).</summary>
    OnUsBalanceEnquiry,

    /// <summary>Balance enquiry at an off-us terminal (merchant at another bank or external ATM).</summary>
    OffUsBalanceEnquiry,

    /// <summary>Mini-statement enquiry at an on-us terminal.</summary>
    OnUsStatementEnquiry,

    /// <summary>Mini-statement enquiry at an off-us terminal.</summary>
    OffUsStatementEnquiry
}
