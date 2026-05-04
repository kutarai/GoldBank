using System.Diagnostics;
using System.Security.Cryptography;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using GoldBank.HSM.Pkcs11;
using GoldBank.Protos.HSM;

namespace GoldBank.HSM.Services;

/// <summary>
/// gRPC service implementation for HSM cryptographic operations (STORY-021).
/// Maps all proto-defined RPCs to <see cref="SoftHsmProvider"/> calls with
/// structured logging, circuit breaker protection, and proper gRPC status codes.
/// </summary>
public sealed class HsmGrpcService : HSMService.HSMServiceBase
{
    private readonly SoftHsmProvider _hsmProvider;
    private readonly HsmCircuitBreaker _circuitBreaker;
    private readonly ILogger<HsmGrpcService> _logger;

    public HsmGrpcService(
        SoftHsmProvider hsmProvider,
        HsmCircuitBreaker circuitBreaker,
        ILogger<HsmGrpcService> logger)
    {
        _hsmProvider = hsmProvider;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    // ── GenerateKey ──────────────────────────────────────────────────

    public override Task<GenerateKeyResponse> GenerateKey(
        GenerateKeyRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("GenerateKey", request.KeyLabel, () =>
        {
            if (string.IsNullOrWhiteSpace(request.KeyLabel))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "key_label is required."));

            var (keyId, kcv, encryptedKey) = _hsmProvider.GenerateMasterKey(request.KeyLabel);

            return Task.FromResult(new GenerateKeyResponse
            {
                Success = true,
                KeyId = keyId,
                KeyCheckValue = kcv,
                EncryptedKey = Convert.ToBase64String(encryptedKey)
            });
        });
    }

    // ── DeriveSessionKey ─────────────────────────────────────────────

    public override Task<DeriveSessionKeyResponse> DeriveSessionKey(
        DeriveSessionKeyRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("DeriveSessionKey", request.MasterKeyId, () =>
        {
            if (string.IsNullOrWhiteSpace(request.MasterKeyId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "master_key_id is required."));

            if (string.IsNullOrWhiteSpace(request.DerivationData))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "derivation_data is required."));

            var (sessionKeyId, kcv) = _hsmProvider.DeriveSessionKey(request.MasterKeyId, request.DerivationData);

            return Task.FromResult(new DeriveSessionKeyResponse
            {
                Success = true,
                SessionKeyId = sessionKeyId,
                KeyCheckValue = kcv
            });
        });
    }

    // ── EncryptPINBlock ──────────────────────────────────────────────

    public override Task<PINBlockResponse> EncryptPINBlock(
        EncryptPINBlockRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("EncryptPINBlock", request.EncryptionKeyId, () =>
        {
            ValidatePinBlockRequest(request.Pin, request.AccountNumber, request.EncryptionKeyId);

            var encryptedBlock = _hsmProvider.EncryptPinBlock(
                request.Pin, request.AccountNumber, request.EncryptionKeyId);

            return Task.FromResult(new PINBlockResponse
            {
                Success = true,
                PinBlock = ByteString.CopyFrom(encryptedBlock)
            });
        });
    }

    // ── DecryptPINBlock ──────────────────────────────────────────────

    public override Task<PINBlockResponse> DecryptPINBlock(
        DecryptPINBlockRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("DecryptPINBlock", request.DecryptionKeyId, () =>
        {
            if (request.EncryptedPinBlock.IsEmpty)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "encrypted_pin_block is required."));

            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "account_number is required."));

            if (string.IsNullOrWhiteSpace(request.DecryptionKeyId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "decryption_key_id is required."));

            var clearPin = _hsmProvider.DecryptPinBlock(
                request.EncryptedPinBlock.ToByteArray(), request.AccountNumber, request.DecryptionKeyId);

            return Task.FromResult(new PINBlockResponse
            {
                Success = true,
                ClearPin = clearPin
            });
        });
    }

    // ── GenerateMAC ──────────────────────────────────────────────────

    public override Task<MACResponse> GenerateMAC(
        GenerateMACRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("GenerateMAC", request.MacKeyId, () =>
        {
            if (request.Data.IsEmpty)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "data is required."));

            if (string.IsNullOrWhiteSpace(request.MacKeyId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "mac_key_id is required."));

            var algorithm = MapMacAlgorithm(request.Algorithm);
            var mac = _hsmProvider.GenerateMac(request.Data.ToByteArray(), request.MacKeyId, algorithm);

            return Task.FromResult(new MACResponse
            {
                Success = true,
                Mac = ByteString.CopyFrom(mac)
            });
        });
    }

    // ── VerifyMAC ────────────────────────────────────────────────────

    public override Task<VerifyMACResponse> VerifyMAC(
        VerifyMACRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("VerifyMAC", request.MacKeyId, () =>
        {
            if (request.Data.IsEmpty)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "data is required."));

            if (string.IsNullOrWhiteSpace(request.MacKeyId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "mac_key_id is required."));

            if (request.Mac.IsEmpty)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "mac is required."));

            var algorithm = MapMacAlgorithm(request.Algorithm);
            var isValid = _hsmProvider.VerifyMac(
                request.Data.ToByteArray(), request.MacKeyId, algorithm, request.Mac.ToByteArray());

            return Task.FromResult(new VerifyMACResponse
            {
                Success = true,
                IsValid = isValid
            });
        });
    }

    // ── GenerateToken ────────────────────────────────────────────────

    public override Task<GenerateTokenResponse> GenerateToken(
        GenerateTokenRequest request, ServerCallContext context)
    {
        return ExecuteWithCircuitBreaker("GenerateToken", string.Empty, () =>
        {
            if (string.IsNullOrWhiteSpace(request.Pan))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "pan is required."));

            var (token, tokenReference) = _hsmProvider.GenerateToken(request.Pan);

            return Task.FromResult(new GenerateTokenResponse
            {
                Success = true,
                Token = token,
                TokenReference = tokenReference
            });
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Wraps an HSM operation with the circuit breaker, structured logging,
    /// duration measurement, and gRPC error mapping.
    /// </summary>
    private async Task<T> ExecuteWithCircuitBreaker<T>(
        string operationName, string keyReference, Func<Task<T>> operation)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _circuitBreaker.ExecuteAsync(operation, operationName);

            stopwatch.Stop();

            _logger.LogInformation(
                "HSM operation completed. Timestamp={Timestamp}, Operation={OperationType}, "
                + "KeyReference={KeyReference}, Result=Success, DurationMs={DurationMs}",
                DateTime.UtcNow.ToString("O"),
                operationName,
                string.IsNullOrEmpty(keyReference) ? "N/A" : keyReference,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (CircuitBreakerOpenException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                "HSM operation rejected by circuit breaker. Timestamp={Timestamp}, Operation={OperationType}, "
                + "KeyReference={KeyReference}, Result=CircuitOpen, DurationMs={DurationMs}",
                DateTime.UtcNow.ToString("O"),
                operationName,
                string.IsNullOrEmpty(keyReference) ? "N/A" : keyReference,
                stopwatch.ElapsedMilliseconds);

            throw new RpcException(new Status(StatusCode.Unavailable, ex.Message));
        }
        catch (RpcException)
        {
            stopwatch.Stop();
            throw; // Already a gRPC exception with correct status code
        }
        catch (CryptographicException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "HSM cryptographic error. Timestamp={Timestamp}, Operation={OperationType}, "
                + "KeyReference={KeyReference}, Result=CryptoError, DurationMs={DurationMs}",
                DateTime.UtcNow.ToString("O"),
                operationName,
                string.IsNullOrEmpty(keyReference) ? "N/A" : keyReference,
                stopwatch.ElapsedMilliseconds);

            throw new RpcException(new Status(StatusCode.Internal, $"Cryptographic operation failed: {ex.Message}"));
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                ex,
                "HSM operation invalid argument. Timestamp={Timestamp}, Operation={OperationType}, "
                + "KeyReference={KeyReference}, Result=InvalidArgument, DurationMs={DurationMs}",
                DateTime.UtcNow.ToString("O"),
                operationName,
                string.IsNullOrEmpty(keyReference) ? "N/A" : keyReference,
                stopwatch.ElapsedMilliseconds);

            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "HSM operation unexpected error. Timestamp={Timestamp}, Operation={OperationType}, "
                + "KeyReference={KeyReference}, Result=Error, DurationMs={DurationMs}",
                DateTime.UtcNow.ToString("O"),
                operationName,
                string.IsNullOrEmpty(keyReference) ? "N/A" : keyReference,
                stopwatch.ElapsedMilliseconds);

            throw new RpcException(new Status(StatusCode.Internal, $"HSM operation failed: {ex.Message}"));
        }
    }

    private static void ValidatePinBlockRequest(string pin, string accountNumber, string keyId)
    {
        if (string.IsNullOrWhiteSpace(pin))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "pin is required."));

        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "account_number is required."));

        if (string.IsNullOrWhiteSpace(keyId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "encryption_key_id is required."));
    }

    private static MacAlgorithm MapMacAlgorithm(MACAlgorithm protoAlgorithm)
    {
        return protoAlgorithm switch
        {
            MACAlgorithm.HmacSha256 => MacAlgorithm.HmacSha256,
            MACAlgorithm.CmacAes => MacAlgorithm.CmacAes,
            MACAlgorithm.Unspecified => throw new RpcException(
                new Status(StatusCode.InvalidArgument, "MAC algorithm must be specified.")),
            _ => throw new RpcException(
                new Status(StatusCode.InvalidArgument, $"Unsupported MAC algorithm: {protoAlgorithm}"))
        };
    }
}
