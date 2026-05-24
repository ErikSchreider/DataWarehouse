namespace SpaceTrafficETL.Models;

public sealed record ExasolImportResult(
    string TableName,
    string CsvPath,
    long RowCount);
