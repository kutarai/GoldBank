namespace GoldBank.SharedKernel.Constants;

public static class ErrorMessages
{
    public const string InsufficientFunds = "Insufficient funds for this transaction.";
    public const string InvalidAccount = "The account number is invalid or does not exist.";
    public const string TransactionLimitExceeded = "Transaction amount exceeds the allowed limit.";
    public const string AccountNotActive = "Account is not active. Please complete KYC verification.";
    public const string InvalidPin = "The PIN entered is incorrect.";
    public const string PinTriesExceeded = "Maximum PIN attempts exceeded. Account temporarily locked.";
    public const string DeviceNotRegistered = "This device is not registered for this account.";
    public const string OtpExpired = "The OTP has expired. Please request a new one.";
    public const string OtpInvalid = "The OTP entered is invalid.";
    public const string SessionExpired = "Your session has expired. Please log in again.";
    public const string TenantNotFound = "Tenant configuration not found.";
    public const string UnauthorizedAccess = "You are not authorized to perform this action.";
}
