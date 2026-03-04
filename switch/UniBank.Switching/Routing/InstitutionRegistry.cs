namespace UniBank.Switching.Routing;

/// <summary>
/// Protocol preference for an institution's switch connection.
/// </summary>
public enum SwitchProtocol
{
    /// <summary>ISO 8583 binary messaging (traditional card networks).</summary>
    Iso8583,

    /// <summary>ISO 20022 XML messaging (modern payment rails).</summary>
    Iso20022
}

/// <summary>
/// Describes a financial institution connected to the national payment switch,
/// including its identifier, routing prefix, protocol preference, and endpoint.
/// </summary>
public sealed record InstitutionInfo(
    string InstitutionId,
    string Name,
    string RoutingPrefix,
    SwitchProtocol Protocol,
    string Endpoint);

/// <summary>
/// Registry of financial institutions connected to the national payment switch.
/// Provides look-up by institution code and by account routing prefix so the
/// router can determine which adapter and endpoint to use for outbound messages.
/// </summary>
public sealed class InstitutionRegistry
{
    private readonly Dictionary<string, InstitutionInfo> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<InstitutionInfo> _institutions = [];

    /// <summary>
    /// Registers a new institution in the registry.
    /// </summary>
    public void Register(InstitutionInfo institution)
    {
        ArgumentNullException.ThrowIfNull(institution);
        _byId[institution.InstitutionId] = institution;
        // Avoid duplicates in the prefix list
        _institutions.RemoveAll(i =>
            string.Equals(i.InstitutionId, institution.InstitutionId, StringComparison.OrdinalIgnoreCase));
        _institutions.Add(institution);
    }

    /// <summary>
    /// Looks up an institution by its unique identifier.
    /// </summary>
    public InstitutionInfo? GetById(string institutionId)
    {
        return _byId.TryGetValue(institutionId, out var info) ? info : null;
    }

    /// <summary>
    /// Resolves the institution responsible for a given account number by matching
    /// the longest routing prefix. Returns null if no institution matches.
    /// </summary>
    public InstitutionInfo? ResolveByAccountPrefix(string accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber))
        {
            return null;
        }

        InstitutionInfo? best = null;
        var bestLength = 0;

        foreach (var inst in _institutions)
        {
            if (accountNumber.StartsWith(inst.RoutingPrefix, StringComparison.Ordinal)
                && inst.RoutingPrefix.Length > bestLength)
            {
                best = inst;
                bestLength = inst.RoutingPrefix.Length;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns all registered institutions.
    /// </summary>
    public IReadOnlyList<InstitutionInfo> GetAll() => _institutions.AsReadOnly();

    /// <summary>
    /// Checks whether an institution with the given identifier is registered.
    /// </summary>
    public bool Contains(string institutionId) => _byId.ContainsKey(institutionId);

    /// <summary>
    /// Seeds the registry with a set of default institutions for the national switch.
    /// Useful for development and testing.
    /// </summary>
    public void SeedDefaults()
    {
        Register(new InstitutionInfo(
            "UNIBANK", "UniBank", "6001", SwitchProtocol.Iso20022, "localhost:5003"));
        Register(new InstitutionInfo(
            "NATBANK", "National Bank", "6002", SwitchProtocol.Iso8583, "localhost:9001"));
        Register(new InstitutionInfo(
            "FIRSTBN", "First Bank", "6003", SwitchProtocol.Iso20022, "localhost:9002"));
        Register(new InstitutionInfo(
            "STDBANK", "Standard Bank", "6004", SwitchProtocol.Iso8583, "localhost:9003"));
        Register(new InstitutionInfo(
            "COMBANK", "Commercial Bank", "6005", SwitchProtocol.Iso20022, "localhost:9004"));
    }
}
