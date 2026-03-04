namespace UniBank.SharedKernel.Constants;

public static class StatusCodes
{
    public const string Success = "00";
    public const string InsufficientFunds = "51";
    public const string InvalidAccount = "14";
    public const string ExpiredCard = "54";
    public const string TransactionNotPermitted = "57";
    public const string ExceedsLimit = "61";
    public const string SystemMalfunction = "96";
    public const string DuplicateTransaction = "94";
    public const string InvalidPin = "55";
    public const string PinTriesExceeded = "75";
    public const string AccountClosed = "62";
    public const string KycPending = "K1";
    public const string KycRejected = "K2";
    public const string DeviceNotBound = "D1";
    public const string OtpExpired = "O1";
    public const string OtpInvalid = "O2";
}
