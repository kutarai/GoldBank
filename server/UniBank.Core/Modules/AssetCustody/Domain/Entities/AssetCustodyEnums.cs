namespace UniBank.Core.Modules.AssetCustody.Domain.Entities;

public enum AssetType { GoldCoin, GoldBar, Silver, Platinum, PreciousStone, Other }
public enum AssetStatus { PendingVerification, Active, Released, Suspended, PendingRelease }
public enum VerificationStatus { Pending, Verified, Failed, Expired }
public enum TrustStatus { Verified, Probationary, Suspended }
