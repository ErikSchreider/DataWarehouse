namespace SpaceTrafficETL.Models;

public sealed record TleRecord(
    string SatelliteName,
    int NoradId,
    string Classification,
    int InternationalDesignatorYear,
    int InternationalDesignatorLaunchNumber,
    string InternationalDesignatorPiece,
    int EpochYear,
    double EpochDay,
    string Line1,
    string Line2,
    DateTimeOffset ParsedAt);
