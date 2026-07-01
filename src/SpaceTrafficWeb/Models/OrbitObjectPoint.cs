namespace SpaceTrafficWeb.Models;

public sealed record OrbitObjectPoint(
    long ObjectId,
    long? NoradId,
    string ObjectName,
    string ObjectType,
    string OrbitClass,
    double? AltitudeKm,
    double? VelocityKmS,
    double? InclinationDeg,
    double? DebrisRiskScore,
    string SourceName,
    string OperatorName,
    string Purpose,
    DateTime? ObservationTimestamp);
