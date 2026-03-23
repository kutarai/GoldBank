using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record ModelStatusResult(
    bool OllamaHealthy, string VisionModel, long VisionModelSize,
    bool FaceModelLoaded, string FaceModelName);

public sealed class GetModelStatusHandler
{
    private readonly OllamaClient _ollamaClient;
    private readonly FaceMatchingService _faceMatchingService;

    public GetModelStatusHandler(OllamaClient ollamaClient, FaceMatchingService faceMatchingService)
    {
        _ollamaClient = ollamaClient;
        _faceMatchingService = faceMatchingService;
    }

    public async Task<Result<ModelStatusResult>> HandleAsync(
        GetModelStatusCommand command, CancellationToken cancellationToken = default)
    {
        var (isHealthy, modelName, modelSize) = await _ollamaClient.CheckHealthAsync(cancellationToken);

        return Result.Success(new ModelStatusResult(
            isHealthy, modelName, modelSize,
            _faceMatchingService.IsModelLoaded, "arcface"));
    }
}
