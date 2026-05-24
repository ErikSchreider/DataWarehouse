using System.Globalization;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class TleParserService(ILogger<TleParserService> logger) : ITleParserService
{
    public async Task<IReadOnlyList<TleObject>> ParseFileAsync(string path, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var objects = new List<TleObject>();

        for (var index = 0; index < lines.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            var objectName = lines[index].Trim();
            if (index + 2 >= lines.Length)
            {
                logger.LogWarning("Skipping incomplete TLE object at line {LineNumber} in {Path}", index + 1, path);
                break;
            }

            var line1 = lines[index + 1].TrimEnd();
            var line2 = lines[index + 2].TrimEnd();

            if (!IsValidTleTriplet(line1, line2))
            {
                logger.LogWarning("Skipping invalid TLE object near line {LineNumber} in {Path}", index + 1, path);
                index++;
                continue;
            }

            objects.Add(ParseObject(objectName, line1, line2));
            index += 3;
        }

        logger.LogInformation("Parsed {ObjectCount} TLE objects from {Path}", objects.Count, path);
        return objects;
    }

    private static bool IsValidTleTriplet(string line1, string line2)
    {
        return line1.Length >= 32
            && line2.Length >= 63
            && line1.StartsWith("1 ", StringComparison.Ordinal)
            && line2.StartsWith("2 ", StringComparison.Ordinal)
            && line1.Substring(2, 5) == line2.Substring(2, 5);
    }

    private static TleObject ParseObject(string objectName, string line1, string line2)
    {
        var noradId = int.Parse(line1.AsSpan(2, 5), CultureInfo.InvariantCulture);
        var classification = line1.Substring(7, 1);
        var internationalDesignator = line1.Substring(9, 8).Trim();
        var epochYear = int.Parse(line1.AsSpan(18, 2), CultureInfo.InvariantCulture);
        var epochDay = double.Parse(line1.Substring(20, 12), CultureInfo.InvariantCulture);
        var inclination = double.Parse(line2.Substring(8, 8), CultureInfo.InvariantCulture);
        var eccentricity = double.Parse($"0.{line2.Substring(26, 7)}", CultureInfo.InvariantCulture);
        var meanMotion = double.Parse(line2.Substring(52, 11), CultureInfo.InvariantCulture);

        return new TleObject(
            objectName,
            noradId,
            classification,
            internationalDesignator,
            epochYear,
            epochDay,
            ToEpochDateTime(epochYear, epochDay),
            inclination,
            eccentricity,
            meanMotion,
            line1,
            line2,
            DateTimeOffset.UtcNow);
    }

    private static DateTimeOffset ToEpochDateTime(int twoDigitYear, double epochDay)
    {
        var fullYear = twoDigitYear >= 57 ? 1900 + twoDigitYear : 2000 + twoDigitYear;
        var wholeDays = Math.Truncate(epochDay);
        var fractionalDay = epochDay - wholeDays;

        return new DateTimeOffset(fullYear, 1, 1, 0, 0, 0, TimeSpan.Zero)
            .AddDays(wholeDays - 1)
            .AddTicks((long)Math.Round(fractionalDay * TimeSpan.TicksPerDay));
    }
}
