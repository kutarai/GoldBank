using SynergySwitch.Core.Models;

namespace SynergySwitch.Core.Interfaces;

/// <summary>
/// Processes ISO 20022 AcceptorAuthorisationRequests and returns responses.
/// </summary>
public interface IAuthorisationProcessor
{
    Task<AuthorisationResponse> ProcessAuthorisationAsync(AuthorisationRequest request);
}
