using System.Globalization;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Parsers;

public sealed class TleParser(ILogger<TleParser> logger) : ITleParser
{
    public async Task<IReadOnlyList<TleRecord>> ParseFileAsync(string path, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        var records = new List<TleRecord>();

        for (var index = 0; index < lines.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
                continue;
            }

            var satelliteName = lines[index].Trim();
            if (index + 2 >= lines.Length)
            {
                logger.LogWarning("Skipping incomplete TLE record at line {LineNumber} in {Path}", index + 1, path);
                break;
            }

            var line1 = lines[index + 1].TrimEnd();
            var line2 = lines[index + 2].TrimEnd();

            if (!IsTleLinePair(line1, line2))
            {
                logger.LogWarning("Skipping invalid TLE record near line {LineNumber} in {Path}", index + 1, path);
                index++;
                continue;
            }

            records.Add(ParseRecord(satelliteName, line1, line2));
            index += 3;
        }

        logger.LogInformation("Parsed {RecordCount} TLE records from {Path}", records.Count, path);
        return records;
    }

    private static bool IsTleLinePair(string line1, string line2)
    {
        return line1.Length >= 32
            && line2.Length >= 7
            && line1.StartsWith("1 ", StringComparison.Ordinal)
            && line2.StartsWith("2 ", StringComparison.Ordinal)
            && line1.Substring(2, 5) == line2.Substring(2, 5);
    }

    private static TleRecord ParseRecord(string satelliteName, string line1, string line2)
    {
        var noradId = int.Parse(line1.AsSpan(2, 5), CultureInfo.InvariantCulture);
        var classification = line1.Substring(7, 1);
        var designatorYear = int.Parse(line1.AsSpan(9, 2), CultureInfo.InvariantCulture);
        var designatorLaunchNumber = int.Parse(line1.AsSpan(11, 3), CultureInfo.InvariantCulture);
        var designatorPiece = line1.Substring(14, 3).Trim();
        var epochYear = int.Parse(line1.AsSpan(18, 2), CultureInfo.InvariantCulture);
        var epochDay = double.Parse(line1.Substring(20, 12), CultureInfo.InvariantCulture);

        return new TleRecord(
            satelliteName,
            noradId,
            classification,
            designatorYear,
            designatorLaunchNumber,
            designatorPiece,
            epochYear,
            epochDay,
            line1,
            line2,
            DateTimeOffset.UtcNow);
    }
}
