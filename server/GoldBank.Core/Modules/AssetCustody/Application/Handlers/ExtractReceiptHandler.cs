using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Modules.AI.Infrastructure.Services;
using GoldBank.Core.Modules.AssetCustody.Application.Commands;
using GoldBank.SharedKernel.Results;
using Error = GoldBank.SharedKernel.Results.Error;

namespace GoldBank.Core.Modules.AssetCustody.Application.Handlers;

/// <summary>
/// Fields parsed from a safe deposit receipt image via Qwen3-VL OCR (STORY-138).
/// Null values indicate the field was not legible or absent in the image.
/// </summary>
public sealed record DepositReceiptFields(
    [property: JsonPropertyName("deposit_house")]   string?  DepositHouse,
    [property: JsonPropertyName("receipt_number")]  string?  ReceiptNumber,
    [property: JsonPropertyName("date")]            string?  Date,
    [property: JsonPropertyName("depositor_name")]  string?  DepositorName,
    [property: JsonPropertyName("description")]     string?  Description,
    [property: JsonPropertyName("quantity")]        string?  Quantity,
    [property: JsonPropertyName("weight")]          string?  Weight,
    [property: JsonPropertyName("purity")]          string?  Purity);

/// <summary>
/// Extracts structured deposit receipt fields from a receipt image using Qwen3-VL
/// via <see cref="OllamaClient.ExtractFromImageAsync{T}"/> (STORY-138).
/// </summary>
public sealed class ExtractReceiptHandler
{
    private const string ExtractionPrompt =
        "Extract the following fields from this safe deposit receipt image: " +
        "deposit house name, receipt number, date of deposit, depositor name, " +
        "asset description, quantity, any weight or purity information. " +
        "Return as JSON with keys: deposit_house, receipt_number, date, " +
        "depositor_name, description, quantity, weight, purity";

    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<ExtractReceiptHandler> _logger;

    public ExtractReceiptHandler(
        OllamaClient ollamaClient,
        ILogger<ExtractReceiptHandler> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<Result<DepositReceiptFields>> HandleAsync(
        ExtractDepositReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = await _ollamaClient.ExtractFromImageAsync<DepositReceiptFields>(
                ExtractionPrompt,
                command.ReceiptImage,
                cancellationToken);

            _logger.LogInformation(
                "Deposit receipt OCR completed for customer {CustomerId}: receipt={Receipt}, house={House}",
                command.CustomerId, fields.ReceiptNumber, fields.DepositHouse);

            return Result.Success(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Deposit receipt OCR failed for customer {CustomerId}", command.CustomerId);

            return Result.Failure<DepositReceiptFields>(
                new Error("ReceiptOcr.Failed", $"Failed to extract receipt fields: {ex.Message}"));
        }
    }
}
