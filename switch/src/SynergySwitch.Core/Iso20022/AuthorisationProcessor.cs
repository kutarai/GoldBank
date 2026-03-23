using Microsoft.Extensions.Logging;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Iso8583;
using SynergySwitch.Core.Models;
using SynergySwitch.Data;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.Iso20022;

/// <summary>
/// Processes ISO 20022 AcceptorAuthorisationRequests.
///
/// Validates the request, then forwards it to the bank via ISO 8583 (through
/// <see cref="BankAuthorisationService"/>). If the bank connection is configured
/// in offline mode, the transaction is approved locally.
/// </summary>
public class AuthorisationProcessor : IAuthorisationProcessor
{
    private readonly SwitchDbContext _db;
    private readonly BankAuthorisationService _bankService;
    private readonly ILogger<AuthorisationProcessor> _logger;

    public AuthorisationProcessor(
        SwitchDbContext db,
        BankAuthorisationService bankService,
        ILogger<AuthorisationProcessor> logger)
    {
        _db = db;
        _bankService = bankService;
        _logger = logger;
    }

    public async Task<AuthorisationResponse> ProcessAuthorisationAsync(AuthorisationRequest request)
    {
        var requestTimestamp = DateTime.UtcNow;
        var maskedPan = MaskPan(request.Pan);

        _logger.LogInformation(
            "Processing authorisation: PAN={MaskedPan}, amount={Amount} {Currency}, entry={EntryMode}",
            maskedPan, request.Amount, request.Currency, request.CardEntryMode);

        // ── Validate request ──
        var (isValid, declineReason) = ValidateRequest(request);

        AuthorisationResponse response;

        if (!isValid)
        {
            // Local validation failure — don't send to bank
            var emvTag8A = ResponseCodeMapper.ToEmvTag8A(declineReason!);
            response = new AuthorisationResponse
            {
                ExchangeId = request.ExchangeId,
                TransactionReference = request.TransactionReference,
                ResponseCode = AuthorisationResponseCode.Declined,
                ResponseReason = declineReason!,
                AuthorisationCode = null,
                EmvResponseCode = emvTag8A,
                DisplayMessage = ResponseCodeMapper.GetDisplayMessage(declineReason!)
            };
        }
        else
        {
            // ── Log EMV ICC data if present ──
            if (request.IccRelatedData is { Length: > 0 })
            {
                var emvTags = EmvTlvParser.Parse(request.IccRelatedData);
                var cryptogram = emvTags.GetValueOrDefault("9F26");
                var cid = emvTags.GetValueOrDefault("9F27");
                var tvr = emvTags.GetValueOrDefault("95");

                _logger.LogInformation(
                    "EMV data: cryptogram={Cryptogram}, CID={CID}, TVR={TVR}, tags={TagCount}",
                    cryptogram ?? "N/A", cid ?? "N/A", tvr ?? "N/A", emvTags.Count);

                if (tvr != null && tvr.Length >= 2)
                {
                    int tvrByte1 = Convert.ToInt32(tvr[..2], 16);
                    if ((tvrByte1 & 0x40) != 0)
                    {
                        _logger.LogWarning("TVR indicates offline data auth failure");
                    }
                }
            }

            // ── Forward to bank via ISO 8583 ──
            response = await _bankService.AuthoriseAsync(request);
        }

        // ── Log transaction ──
        var txnLog = new TransactionLogEntity
        {
            ExchangeId = request.ExchangeId,
            TransactionReference = request.TransactionReference,
            TerminalId = request.TerminalId,
            MerchantId = request.MerchantId,
            PanLastFour = request.Pan.Length >= 4 ? request.Pan[^4..] : request.Pan,
            Amount = request.Amount,
            Currency = request.Currency,
            CardEntryMode = request.CardEntryMode,
            CvmMethod = request.CvmMethod,
            ResponseCode = response.ResponseCode.ToString(),
            ResponseReason = response.ResponseReason,
            AuthorisationCode = response.AuthorisationCode,
            RequestTimestamp = requestTimestamp,
            ResponseTimestamp = DateTime.UtcNow,
            HasIccData = request.IccRelatedData is { Length: > 0 }
        };

        _db.TransactionLogs.Add(txnLog);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Authorisation result: PAN={MaskedPan}, response={Response}, authCode={AuthCode}",
            maskedPan, response.ResponseCode, response.AuthorisationCode ?? "N/A");

        return response;
    }

    private (bool isValid, string? declineReason) ValidateRequest(AuthorisationRequest request)
    {
        if (string.IsNullOrEmpty(request.Pan) || request.Pan.Length < 13 || request.Pan.Length > 19)
            return (false, "0014");

        if (!request.Pan.All(char.IsDigit))
            return (false, "0014");

        if (request.Amount <= 0)
            return (false, "0013");

        if (!string.IsNullOrEmpty(request.ExpiryDate) && request.ExpiryDate.Length == 4)
        {
            if (int.TryParse(request.ExpiryDate[..2], out int yy) &&
                int.TryParse(request.ExpiryDate[2..], out int mm))
            {
                int year = 2000 + yy;
                if (new DateTime(year, mm, 1).AddMonths(1) < DateTime.UtcNow)
                    return (false, "0054");
            }
        }

        return (true, null);
    }

    private static string MaskPan(string pan)
    {
        if (string.IsNullOrEmpty(pan) || pan.Length < 8)
            return "****";
        return pan[..6] + new string('*', pan.Length - 10) + pan[^4..];
    }
}
