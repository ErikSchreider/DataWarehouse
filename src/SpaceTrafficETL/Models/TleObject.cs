namespace SpaceTrafficETL.Models;

public sealed record TleObject(
    string ObjectName,
    int NoradId,
    string Classification,
    string InternationalDesignator,
    int EpochYear,
    double EpochDay,
    DateTimeOffset Epoch,
    double InclinationDegrees,
    double Eccentricity,
    double MeanMotionRevolutionsPerDay,
    string Line1,
    string Line2,
    DateTimeOffset ParsedAt);
