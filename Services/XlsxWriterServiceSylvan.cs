using Oracle.ManagedDataAccess.Client;
using reportmangerv2.Events;
using Sylvan.Data.Excel;

namespace reportmangerv2.Services;
public class XlsxWriterServiceSylvan:
    IXlsxWriterService
{
    private readonly ILogger<XlsxWriterServiceSylvan> _logger;
    private readonly int BufferSize = 81920; // 80 KB buffer for file writing

    public XlsxWriterServiceSylvan(ILogger<XlsxWriterServiceSylvan> logger)
    {
        _logger = logger;
    }

    public Task<(string filePath, long totalRows, int cursorCount)> WriteMultiCursorAsync(IEnumerable<(OracleDataReader reader, string sheetName)> cursors, string outputDir, string baseFileName, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    

public async Task<(string filePath, long rowCount)> WriteQueryAsync(
    OracleDataReader reader, string outputDir, string baseFileName, string userID, string executionID, CancellationToken ct)
{
    Directory.CreateDirectory(outputDir);
    var filePath = Path.Combine(outputDir, $"{baseFileName}.xlsx");
    
    // Excel max rows is 1,048,576. We use 1,000,000 to be safe and leave room for headers.
    const int MAX_ROWS_PER_SHEET = 1000000; 

    long totalRows = 0;
    int sheetIndex = 1;
    bool hasMoreData = true;

    // FileStream is strictly for writing directly to disk
    await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    await using var bufferedStream = new BufferedStream(fileStream, BufferSize);

    // Create Sylvan Excel Writer
    using var edw = ExcelDataWriter.Create(bufferedStream,ExcelWorkbookType.ExcelXml);

    while (hasMoreData)
    {
        ct.ThrowIfCancellationRequested();

        string sheetName = sheetIndex == 1 ? "Sheet1" : $"Sheet{sheetIndex}";
        
        // Wrap the reader to cap it at 1 million rows
        using var boundedReader = new BoundedDataReader(reader, MAX_ROWS_PER_SHEET);

        // Publish start of sheet progress
      

        // Sylvan streams data row-by-row directly to the disk stream
        var result = edw.Write(boundedReader, sheetName);
        
        totalRows += result.RowsWritten;
        
      

        // If we read exactly MAX_ROWS_PER_SHEET, there might be more data left in the reader.
        // If we read less, we have reached the end of the OracleDataReader.
        hasMoreData = result.RowsWritten >= MAX_ROWS_PER_SHEET; 
        
        // Safety check: if the underlying reader is closed or has no more rows, stop.
        if (reader.IsClosed || !reader.HasRows) hasMoreData = false;
        
        sheetIndex++;
    }

    return (filePath, totalRows);
}
}