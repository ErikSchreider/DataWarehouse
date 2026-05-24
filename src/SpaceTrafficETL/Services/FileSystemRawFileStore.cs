using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class FileSystemRawFileStore(IOptions<SpaceTrafficOptions> options, ILogger<FileSystemRawFileStore> logger)
    : IRawFileStore
{
    public async Task<RawDataset> SaveAsync(DownloadedFile file, CancellationToken cancellationToken)
    {
        var downloadedAt = file.DownloadedAt;
        var safeName = ToSafePathSegment(file.Source.Name);
        var rawDirectory = Path.Combine(
            options.Value.DataDirectories.StorageRoot,
            options.Value.DataDirectories.Raw,
            safeName,
            downloadedAt.Year.ToString("0000"),
            downloadedAt.Month.ToString("00"),
            downloadedAt.Day.ToString("00"));

        Directory.CreateDirectory(rawDirectory);

        var fileName = $"{downloadedAt:yyyyMMddTHHmmssfffZ}_{safeName}{file.Source.RawFileExtension}";
        var rawFilePath = Path.Combine(rawDirectory, fileName);

        await using var output = File.Create(rawFilePath);
        await file.Content.CopyToAsync(output, cancellationToken);

        logger.LogInformation("Stored raw dataset {SourceName} at {RawFilePath}", file.Source.Name, rawFilePath);

        return new RawDataset(
            file.Source.Name,
            file.Source.Kind,
            file.Source.Url,
            downloadedAt,
            file.ContentType,
            rawFilePath);
    }

    private static string ToSafePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}
