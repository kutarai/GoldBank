using UniBank.SharedKernel.Domain;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Merchants.Domain.ValueObjects;

/// <summary>
/// Value object for validated GPS coordinates (STORY-050).
/// Latitude: -90 to 90, Longitude: -180 to 180.
/// </summary>
public sealed class GpsCoordinates : ValueObject
{
    public double Latitude { get; }
    public double Longitude { get; }
    public double AccuracyMeters { get; }

    private GpsCoordinates(double latitude, double longitude, double accuracyMeters)
    {
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = accuracyMeters;
    }

    public static Result<GpsCoordinates> Create(double latitude, double longitude, double accuracyMeters = 0)
    {
        if (latitude is < -90 or > 90)
            return Result.Failure<GpsCoordinates>(new Error("GPS.InvalidLatitude", "Latitude must be between -90 and 90."));

        if (longitude is < -180 or > 180)
            return Result.Failure<GpsCoordinates>(new Error("GPS.InvalidLongitude", "Longitude must be between -180 and 180."));

        return Result.Success(new GpsCoordinates(latitude, longitude, accuracyMeters));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}
