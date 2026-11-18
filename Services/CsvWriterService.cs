using Oracle.ManagedDataAccess.Client;
using reportmangerv2.Events;


namespace reportmangerv2.Services;

public interface ICsvWriterService
{
    /// <summary>Write a plain SQL OracleDataReader to a single (multi-sheet-if-overflow) XLSX file.</summary>
    Task<(string filePath, long rowCount)> WriteQueryAsync(
        OracleDataReader reader, string outputDir, string baseFileName,string UserID,string executionID, CancellationToken ct);

    /// <summary>Write multiple OracleDataReaders (one per RefCursor) as separate sheets in one XLSX file.</summary>
    Task<(string filePath, long totalRows, int cursorCount)> WriteMultiCursorAsync(
        IEnumerable<(OracleDataReader reader, string sheetName)> cursors,
        string outputDir, string baseFileName, CancellationToken ct);
}
public class CsvWriterService : ICsvWriterService
{
   
    private IEventPublisher _eventPublisher;
    private const int ProgressReportInterval = 10000; // Report progress every 1000 rows
    private readonly ILogger<XlsxWriterService> _logger;
    public CsvWriterService(IEventPublisher eventPublisher, ILogger<XlsxWriterService> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }
  
public async Task<(string filePath, long rowCount)> WriteQueryAsync(OracleDataReader reader, string outputDir, string baseFileName, string UserID, string executionID, CancellationToken ct)
    {
       Directory.CreateDirectory(outputDir);
        // filePath = Path.Combine(outputDir, $"{baseFileName}.csv");
         var   filePath = GetUniqueFilePath(outputDir, baseFileName, ".csv");
        using var writer = new StreamWriter(filePath);
        var columnNames = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToArray();
        writer.WriteLine(string.Join(",", columnNames));
        long rowCount = 0;
        while (reader.Read())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            var line = string.Join(";", values.Select(v =>RepairValue(v.ToString()??"") ?? ""));
           
            
            
            writer.WriteLine(line);
            rowCount++;
               if (rowCount % ProgressReportInterval == 0)
            {
                    //  await
                    _ = _eventPublisher.PublishAsync(new ReportProgressEvent
                    {
                        ExecutionId = executionID, // You would replace this with the actual execution ID
                        UserId = UserID,
                        ProgressRowsNumber = (int)rowCount
                    }, ct);
                
                
            }
            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("CSV writing cancelled.");
                // if file exists delete it
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

               return ("",0);
            }
        }
        return (filePath, rowCount);

    }
    public Task<(string filePath, long totalRows, int cursorCount)> WriteMultiCursorAsync(IEnumerable<(OracleDataReader reader, string sheetName)> cursors, string outputDir, string baseFileName, CancellationToken ct)
    {
        throw new NotImplementedException();
        
        
    }
    private string GetUniqueFilePath(string directory, string baseFileName, string extension)
    {
        var filePath = Path.Combine(directory, $"{baseFileName}{extension}");
        int counter = 1;
        while (File.Exists(filePath))
        {
            filePath = Path.Combine(directory, $"{baseFileName}_{counter}{extension}");
            counter++;
        }
        return filePath;
    }
    private string RepairValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        // Replace newlines with spaces
        value = value.Replace("\r", " ").Replace("\n", " ");
        // Escape double quotes by doubling them
        value = value.Replace("\"", " ");
        // Surround the value with double quotes if it contains a comma, semicolon, or double quote
       
        value = $"\"{value}\"";
   
        return value;
    }

    
}