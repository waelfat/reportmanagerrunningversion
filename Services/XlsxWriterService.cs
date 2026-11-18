using System;
using System.Globalization;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using reportmangerv2.Events;

namespace reportmangerv2.Services;


public interface IXlsxWriterService
{
    /// <summary>Write a plain SQL OracleDataReader to a single (multi-sheet-if-overflow) XLSX file.</summary>
    Task<(string filePath, long rowCount)> WriteQueryAsync(
        OracleDataReader reader, string outputDir, string baseFileName,string UserID,string executionID, CancellationToken ct);

    /// <summary>Write multiple OracleDataReaders (one per RefCursor) as separate sheets in one XLSX file.</summary>
    Task<(string filePath, long totalRows, int cursorCount)> WriteMultiCursorAsync(
        IEnumerable<(OracleDataReader reader, string sheetName)> cursors,
        string outputDir, string baseFileName, CancellationToken ct);
}
public class ColumnSchema
{
    public int Index { get; set; }
    public required string Name { get; set; }
    public required Type FieldType { get; set; }
    public bool IsOracleDecimal { get; set; }
    public bool IsOracleDate { get; set; }
    public bool IsDecimal { get; set; }
    public bool IsString { get; set; }
}
public class XlsxWriterService : IXlsxWriterService
{
    private const long MaxRowsPerSheet = 1_048_570;
    private IEventPublisher _eventPublisher;
    private const int ProgressReportInterval = 10000; // Report progress every 1000 rows
    private readonly ILogger<XlsxWriterService> _logger;
    private const int BufferSize = 4 * 1024 * 1024; 
  
    public XlsxWriterService(IEventPublisher eventPublisher, ILogger<XlsxWriterService> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
   
    }

    public async Task<(string filePath, long rowCount)> WriteQueryAsync(
        OracleDataReader reader, string outputDir, string baseFileName,string userID,string executionID, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, $"{baseFileName}.xlsx");
        
        // 1. SETUP STREAM WITH BUFFERING (Crucial for I/O performance)
       await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
       await using var bufferedStream = new BufferedStream(fileStream, BufferSize);

        using var doc = SpreadsheetDocument.Create(bufferedStream, SpreadsheetDocumentType.Workbook);
        var wbPart = doc.AddWorkbookPart();
        wbPart.Workbook = new Workbook();
        var sheets = wbPart.Workbook.AppendChild(new Sheets());
        uint sheetId = 1;
        long totalRows = 0;
        int subSheet = 1;
        var (wsPart, writer) = CreateSheet(wbPart, sheets, "Sheet1", sheetId++);
        long rowsInSheet = 0;
        //notity start of writing
        _ = _eventPublisher.PublishAsync(new ReportProgressEvent
        {
            ExecutionId = executionID, // You would replace this with the actual execution ID
            UserId = userID,
            ProgressRowsNumber = 0
           
        }, ct);
        var columns = new List<ColumnSchema>(reader.FieldCount);
for (int i = 0; i < reader.FieldCount; i++)
{
    var type = reader.GetFieldType(i);
    columns.Add(new ColumnSchema
    {
        Index = i,
        Name = reader.GetName(i),
        FieldType = type,
        IsOracleDecimal = type == typeof(OracleDecimal),
        IsOracleDate = type == typeof(OracleDate) || type == typeof(OracleTimeStamp),
        IsDecimal = type == typeof(decimal),
        IsString = type == typeof(string)
    });
}
        WriteHeaderRow(writer, columns);
try{
        while (await reader.ReadAsync(ct))
        {
           
             if (rowsInSheet >= MaxRowsPerSheet)
    {
        CloseSheet(writer);
        subSheet++;
        (wsPart, writer) = CreateSheet(wbPart, sheets, $"Sheet{subSheet}", sheetId++);
        
        // Rewrite header for new sheet
        WriteHeaderRow(writer,columns);
        rowsInSheet = 0;
    }
            WriteDataRowV2(writer,reader, columns);
            rowsInSheet++;
            totalRows++;
            // publish progress after writing each 1000 rows
            if (totalRows % ProgressReportInterval == 0) 
            {
                    //  await
                    _ = _eventPublisher.PublishAsync(new ReportProgressEvent
                    {
                        ExecutionId = executionID, // You would replace this with the actual execution ID
                        UserId = userID,
                        ProgressRowsNumber = (int)totalRows
                    }, ct);
                
                
            }
            if (totalRows % 100000 == 0)
            {
                GC.Collect();
            }
            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation($"Cancellation requested for execution ID {executionID} by user {userID}. Stopping Excel writing.");
                CloseSheet(writer);
                wbPart.Workbook.Save();
                // Optionally delete the incomplete file
                if (File.Exists(filePath))
                    File.Delete(filePath);
                ct.ThrowIfCancellationRequested();
            }
        }}catch(OperationCanceledException)
        {
            // Handle cancellation if needed (e.g., log it)
            _logger.LogInformation($"Excel writing cancelled for execution ID {executionID} by user {userID}.");
             CloseSheet(writer);
                wbPart.Workbook.Save();
                 if (File.Exists(filePath))
                    File.Delete(filePath);
                    throw; // Rethrow to let caller know it was cancelled
            //return (filePath, totalRows); // Return the file path and rows written so far, or handle as desired
        }catch(Exception ex)
        {
            // Handle other exceptions if needed (e.g., log it)
            _logger.LogError(ex.Message, "Error while writing Excel file.");
            CloseSheet(writer);
            wbPart.Workbook.Save();
            return ("",0); // Return null or handle as desired
        }

        CloseSheet(writer);
        wbPart.Workbook.Save();
        _ = _eventPublisher.PublishAsync(new ReportProgressEvent
                    {
                        ExecutionId = executionID, // You would replace this with the actual execution ID
                        UserId = userID,
                        ProgressRowsNumber = (int)totalRows
                    }, ct);
        return (filePath, totalRows);
    }

    public async Task<(string filePath, long totalRows, int cursorCount)> WriteMultiCursorAsync(
        IEnumerable<(OracleDataReader reader, string sheetName)> cursors,
        string outputDir, string baseFileName, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, $"{baseFileName}.xlsx");

        using var doc = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);
        var wbPart = doc.AddWorkbookPart();
        wbPart.Workbook = new Workbook();
        var sheets = wbPart.Workbook.AppendChild(new Sheets());

        uint sheetId = 1;
        long totalRows = 0;
        int cursorCount = 0;

        foreach (var (reader, sheetBaseName) in cursors)
        {
             var columns = new List<ColumnSchema>(reader.FieldCount);
for (int i = 0; i < reader.FieldCount; i++)
{
    var type = reader.GetFieldType(i);
    columns.Add(new ColumnSchema
    {
        Index = i,
        Name = reader.GetName(i),
        FieldType = type,
        IsOracleDecimal = type == typeof(OracleDecimal),
        IsOracleDate = type == typeof(OracleDate) || type == typeof(OracleTimeStamp),
        IsDecimal = type == typeof(decimal),
        IsString = type == typeof(string)
    });
}
            cursorCount++;
            var safeName = TruncateSheetName(sheetBaseName);
            int subSheet = 1;

            var (_, writer) = CreateSheet(wbPart, sheets, safeName, sheetId++);
            long rowsInSheet = 0;
            WriteHeaderRow(writer, columns);

            while (await reader.ReadAsync(ct))
            {
                if (rowsInSheet >= MaxRowsPerSheet)
                {
                    CloseSheet(writer);
                    subSheet++;
                    var continueName = TruncateSheetName($"{safeName} ({subSheet})");
                    (_, writer) = CreateSheet(wbPart, sheets, continueName, sheetId++);
                    WriteHeaderRow(writer, columns);
                    rowsInSheet = 0;
                }
                WriteDataRow(writer, reader);
                rowsInSheet++;
                totalRows++;
                //publish progress after writing each 1000 rows
                

            }
            CloseSheet(writer);
        }
      
        wbPart.Workbook.Save();
        return (filePath, totalRows, cursorCount);
    }

    // ── Helpers ────────────────────────────────────────────────

    private static (WorksheetPart, OpenXmlWriter) CreateSheet(
        WorkbookPart wbPart, Sheets sheets, string name, uint sheetId)
    {
        var wsPart = wbPart.AddNewPart<WorksheetPart>();
        var writer = OpenXmlWriter.Create(wsPart);
        writer.WriteStartElement(new Worksheet());
        writer.WriteStartElement(new SheetData());

        var sheet = new Sheet
        {
            Id   = wbPart.GetIdOfPart(wsPart),
            SheetId = sheetId,
            Name = name
        };
        sheets.Append(sheet);
        return (wsPart, writer);
    }

    private static void CloseSheet(OpenXmlWriter writer)
    {
        if (writer == null) return;
        writer.WriteEndElement(); // SheetData
        writer.WriteEndElement(); // Worksheet
        writer.Dispose();
    }

    private static void WriteHeaderRow(OpenXmlWriter writer,List<ColumnSchema> columns)
    {
        if (writer == null) return;
        writer.WriteStartElement(new Row());
    foreach (var col in columns)
{
    writer.WriteStartElement(new Cell { DataType = CellValues.InlineString });
    writer.WriteElement(new InlineString(new Text(col.Name)));
    writer.WriteEndElement();
}
writer.WriteEndElement();
        //publish progress after writing header
        


    }

   /* private static void WriteDataRow(OpenXmlWriter writer, OracleDataReader reader)
    {
        writer.WriteStartElement(new Row());
        for (int i = 0; i < reader.FieldCount; i++)
        {
            //var rowVal = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i)) ?? string.Empty;
              string rowVal = string.Empty;

        if (!reader.IsDBNull(i))
        {
            var value = reader.GetValue(i);
            rowVal = value?.ToString() ?? string.Empty;
        }
            var val=CleanInvalidXmlChars(rowVal);
            writer.WriteStartElement(new Cell { DataType = CellValues.InlineString });
            writer.WriteElement(new InlineString(new Text(val)));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }
*/

private static void WriteDataRow(OpenXmlWriter writer, OracleDataReader reader)
{
    writer.WriteStartElement(new Row());
    for (int i = 0; i < reader.FieldCount; i++)
    {
        if (reader.IsDBNull(i))
        {
            writer.WriteStartElement(new Cell());
            writer.WriteEndElement();
            continue;
        }

        Type fieldType = reader.GetFieldType(i);
        
        // Handle decimal columns
        if (fieldType == typeof(OracleDecimal))
        {
            decimal decimalValue = reader.GetOracleDecimal(i).Value;
            string stringValue = decimalValue.ToString(CultureInfo.InvariantCulture);
            string cleanedValue = CleanInvalidXmlChars(stringValue);
            writer.WriteStartElement(new Cell { DataType = CellValues.InlineString });
            writer.WriteElement(new InlineString(new Text(cleanedValue)));
            writer.WriteEndElement();
        }
        // Handle date columns (OracleDate / OracleTimeStamp)
        else if (fieldType == typeof(OracleDate) || fieldType == typeof(OracleTimeStamp))
        {
            DateTime dateValue;
            if (fieldType == typeof(OracleDate))
                dateValue = reader.GetOracleDate(i).Value;
            else
                dateValue = reader.GetOracleTimeStamp(i).Value;

            double excelDate = dateValue.ToOADate();
            writer.WriteStartElement(new Cell { DataType = CellValues.Date });
            writer.WriteElement(new CellValue(excelDate.ToString(CultureInfo.InvariantCulture)));
            writer.WriteEndElement();
        }
         else if (fieldType == typeof(Decimal))
        {
            OracleDecimal decimalValue = reader.GetOracleDecimal(i);
            // if (!decimalValue.IsNull)
            //     {
            //         decimal value=(decimal)OracleDecimal.SetPrecision(decimalValue,2);
            //     }
            //decimal decimalVal = decimalValue.IsNull ? 0 : (decimal)OracleDecimal.SetPrecision(decimalValue, 2);
            //Console.WriteLine($"Original: {decimalValue}, Rounded: {decimalVal}");
            string stringValue = decimalValue.IsNull ? string.Empty:((decimal)OracleDecimal.SetPrecision(decimalValue,4)).ToString(CultureInfo.InvariantCulture);
            decimal decimalValRounded = decimalValue.IsNull ? 0 : Math.Round((decimal)OracleDecimal.SetPrecision(decimalValue, 4), 2);
           // string cleanedValue = CleanInvalidXmlChars(stringValue);
            writer.WriteStartElement(new Cell { DataType = CellValues.Number });
            //write decimal value
            writer.WriteElement(new CellValue(decimalValRounded.ToString(CultureInfo.InvariantCulture)));

              
            //writer.WriteElement(new InlineString(new Text(cleanedValue)));
            writer.WriteEndElement();
        }
        // Handle all other types (string, int, etc.)
        else
        {
            object rawValue = reader.GetValue(i);
            string stringValue = rawValue.ToString() ?? string.Empty;
            string cleanedValue = CleanInvalidXmlChars(stringValue);
            writer.WriteStartElement(new Cell { DataType = CellValues.InlineString });
            writer.WriteElement(new InlineString(new Text(cleanedValue)));
            writer.WriteEndElement();
        }
    }
    writer.WriteEndElement();
}


private static void WriteDataRowV2(OpenXmlWriter writer, OracleDataReader reader,List<ColumnSchema> columns)
{
       writer.WriteStartElement(new Row());
    
    // Iterate through pre-computed columns (Guarantees sequential access order!)
    foreach (var col in columns)
    {
        if (reader.IsDBNull(col.Index))
        {
            writer.WriteStartElement(new Cell());
            writer.WriteEndElement();
            continue;
        }

       if (col.IsOracleDecimal || col.IsDecimal)
{
    var oracleDecimal = reader.GetOracleDecimal(col.Index);
    if (oracleDecimal.IsNull)
    {
        writer.WriteStartElement(new Cell());
        writer.WriteEndElement();
    }
    else
    {
        // FIX: Extract the direct decimal value without changing its precision/digits
        decimal val = Math.Round(oracleDecimal.Value,2);  
        
        // Round to 2 decimal places safely in .NET
        decimal rounded = Math.Round(val, 2);
        
        writer.WriteStartElement(new Cell { DataType = CellValues.Number });
        writer.WriteElement(new CellValue(rounded.ToString(CultureInfo.InvariantCulture)));
        writer.WriteEndElement();
    }
}

        else if (col.IsOracleDate)
        {
            DateTime dateValue = col.FieldType == typeof(OracleDate) 
                ? reader.GetOracleDate(col.Index).Value 
                : reader.GetOracleTimeStamp(col.Index).Value;

            double excelDate = dateValue.ToOADate();
            writer.WriteStartElement(new Cell { DataType = CellValues.Date });
            writer.WriteElement(new CellValue(excelDate.ToString(CultureInfo.InvariantCulture)));
            writer.WriteEndElement();
        }
        else
        {
            // OPTIMIZATION: Avoid GetValue() boxing overhead for strings
            string stringValue = col.IsString 
                ? (reader.GetString(col.Index) ?? string.Empty) 
                : (reader.GetValue(col.Index)?.ToString() ?? string.Empty);
                
            string cleanedValue = CleanInvalidXmlChars(stringValue);
            
            writer.WriteStartElement(new Cell { DataType = CellValues.InlineString });
            writer.WriteElement(new InlineString(new Text(cleanedValue)));
            writer.WriteEndElement();
        }
    }
    writer.WriteEndElement(); // End Row


}


/*
private static void WriteDataRow(OpenXmlWriter writer, OracleDataReader reader)
{
    writer.WriteStartElement(new Row());
    for (int i = 0; i < reader.FieldCount; i++)
    {
        if (reader.IsDBNull(i))
        {
            writer.WriteStartElement(new Cell { DataType = CellValues.String });
            writer.WriteElement(new CellValue(string.Empty));
            writer.WriteEndElement();
            continue;
        }

        object value = reader.GetValue(i);

        if (value is DateTime dt)
        {
            // Excel stores dates as OADate (double)
            double oaDate = dt.ToOADate();

            writer.WriteStartElement(new Cell { DataType = CellValues.Number });
            writer.WriteElement(new CellValue(oaDate.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            writer.WriteEndElement();
        }
        else
        {
            var val = CleanInvalidXmlChars(value.ToString()??"");
            writer.WriteStartElement(new Cell { DataType = CellValues.InlineString });
            writer.WriteElement(new InlineString(new Text(val)));
            writer.WriteEndElement();
        }
    }
    writer.WriteEndElement();
}*/

    private static string TruncateSheetName(string name)
    {
        // Excel sheet names: max 31 chars, no special chars
        var clean = string.Concat(name.Where(c => !"\\/*?:[]\n\r".Contains(c)));
        return clean.Length > 31 ? clean[..31] : clean;
    }
    private static string CleanInvalidXmlChars(string text)
{
    if (string.IsNullOrEmpty(text)) return text;

    // Keep only valid XML characters (Arabic letters are fine)
    return new string(text.Where(ch => XmlConvert.IsXmlChar(ch)).ToArray());
}

}

