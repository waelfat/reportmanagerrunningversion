using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using reportmangerv2.Data;
using reportmangerv2.ViewModels;
using reportmangerv2.Domain;
using reportmangerv2.Enums;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using reportmangerv2.Services;
using Oracle.ManagedDataAccess.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Options;
using System.Data;
using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using Org.BouncyCastle.Asn1.Esf;

namespace reportmangerv2.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly AppDbContext _context;
    private readonly ReportService _reportService;
    private readonly ILogger<ReportController> _logger;
    private readonly IWebHostEnvironment _env;

    public ReportController(AppDbContext context, ILogger<ReportController> logger, ReportService reportService, IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _reportService = reportService;
        _env = env;
    }

    [HttpGet]public async Task<IActionResult> Index()
{
    var reportHeirarchyQuery = @"
    SELECT *
    FROM (
        SELECT
            r.id AS ReportId,
            r.name AS ReportName,
            r.description AS ReportDescription,
            s.name AS SchemaName,
            LPAD(CAST(N' ' AS NVARCHAR2(10)), 2 * (LEVEL - 1)) || c.name AS CategoryName,
            r.isactive AS IsActive,
            TRUNC(r.createddate) AS CreatedDate,
            u.fullname AS CreatedByUserName,
            LEVEL AS CategoryLevel,
            c.id AS CategoryId,
            c.ParentCategoryId
        FROM RV2_Categories c
        LEFT JOIN RV2_Reports r ON r.categoryid = c.id
        LEFT JOIN rv2_schemas s ON r.schemaid = s.id
        LEFT JOIN rv2_aspnetusers u ON r.createdbyid = u.id
        START WITH c.ParentCategoryId IS NULL
        CONNECT BY PRIOR c.id = c.ParentCategoryId
        ORDER SIBLINGS BY c.name,r.createddate
    ) t
    WHERE ReportName IS NOT NULL
    ";

    // 1. Execute raw SQL and fetch to memory first
    var reportInfos = await _context.Database.SqlQueryRaw<ReportInfo>(reportHeirarchyQuery)
        .ToListAsync(); 

    // 2. Project to ViewModel in memory (LINQ to Objects)
    var reports = reportInfos.Select(r => new ReportViewModel
    {
        Id = r.ReportId,
        Name = r.ReportName,
        Description = r.ReportDescription,
        SchemaName = r.SchemaName, // See note below
        CategoryName = r.CategoryName,
        IsActive = r.IsActive,
        CreatedDate = r.CreatedDate,
        CreatedByUserName = r.CreatedByUserName ?? "N/A",
    }).ToList();

    return View(reports);
}
    [HttpGet]
    public async Task<IActionResult> create()
    {
        var availableSchemasSelectListItems = await _context.Schemas.Select(s => new SelectListItem { Text = s.Name, Value = s.Id }).ToListAsync();
        ViewBag.AvailableSchemas = availableSchemasSelectListItems;
        var categoriesHierarchyQuery = @"
       SELECT
  LPAD('--', 4*(LEVEL-1)) || c.name   AS Name
 --, LEVEL                                      AS CategoryLevel,
  ,c.id as Id
 -- ,  c.ParentCategoryId
FROM
  rv2_Categories c
   WHERE NAME<>'Favorites' 
START WITH
  c.ParentCategoryId IS NULL
CONNECT BY
  PRIOR c.id = c.parentcategoryid

ORDER SIBLINGS BY c.name";
        try
        {
            var categroiesHierarchy = await _context.Categories.FromSqlRaw(categoriesHierarchyQuery).Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToListAsync();
            ViewBag.CategoriesHierarchy = categroiesHierarchy;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, "error in fetching categories hierarchy {ex}", ex.Message);
            return BadRequest("error in fetching categories");
        }
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportModelView model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
            return BadRequest(string.Join(", ", errors));
        }

        var positions = model.Parameters.Select(p => p.Position).OrderBy(p => p).ToList();
        if (positions.Count != positions.Distinct().Count())
            return BadRequest("Parameter positions must be unique");
        if (positions.Any() && (positions.First() != 1 || positions.Last() != positions.Count))
            return BadRequest("Parameter positions must be sequential starting from 1");

        var schema = await _context.Schemas.SingleOrDefaultAsync(s => s.Id == model.SchemaId);
        if (schema is null) return NotFound("Schema Not Found");

        try
        {
            var report = new Report
            {
                Id = Guid.NewGuid().ToString(),
                Name = model.Name,
                Description = model.Description,
                CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new UnauthorizedAccessException() ,
                CreatedDate = DateTime.UtcNow,
                Schema = schema,
                ReportQuery = model.ReportQuery,
                IsActive = model.IsActive,
                CategoryId = model.CategoryId,
                ReportType = model.ReportType,
                ReportParameters = model.Parameters.Select(p => new ReportParameter
                {
                    Name = p.Name,
                    Description = p.Description,
                    Type = Enum.Parse<OracleDbType>(p.Type, true),
                    ViewControl =Enum.Parse<ViewControl>(p.ViewControl, true),// model.ReportType == ReportType.STOREDPROCEDURE ? ViewControl.TextBox : Enum.Parse<ViewControl>(p.ViewControl, true),
                    DefaultValue = p.DefaultValue?.Trim(),
                    Position = p.Position,
                    IsRequired = p.IsRequired,
                    DependsOn = p.DependsOn,
                    DependencyQuery = p.DependencyQuery,
                    Direction = Enum.TryParse<ParameterDirection>(p.Direction, out var dir) ? dir : ParameterDirection.Input
                }).ToList()
            };
            // get stored procedure output parameters
            if (model.ReportType == ReportType.STOREDPROCEDURE)//STOREDPROCEDURE
            {
                // get procedure parameters
                var procParams = await GetStroredProcedureParams(model.ReportQuery,schema.ConnectionString);
                // assign position for each parameter
                // get output parameters and put them first
                var outputParams = procParams.Where(p => p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.ReturnValue).ToList();
                int position = report.ReportParameters.Count+1;
                foreach (var param in outputParams)
                {
                    param.Position = position++;
                    param.ViewControl = ViewControl.TextBox;
                    param.Direction=ParameterDirection.Output;
                    report.ReportParameters.Add(param);
                }
                
            }

            if (model.ReportType == ReportType.SQLSTATEMENT)//STOREDPROCEDURE
            {
                var (validated, msg) = await Validate(report);
                if (!validated) return BadRequest(msg);
            }

            await _context.Reports.AddAsync(report);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Report created successfully with ID: {ReportId}", report.Id);
            return Ok(new { message = "Report created successfully", reportId = report.Id });
        }//ExecutionId: 12cb6bb9-5da6-4199-ab01-a9e385914691
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating report");
            return StatusCode(500, "An error occurred while creating the report");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var report = await _context.Reports
            .Include(r => r.ReportParameters)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return NotFound();

        var schema = await _context.Schemas.FindAsync(report.SchemaId);

        var viewModel = new EditReportViewModel
        {
            Id = report.Id,
            Name = report.Name,
            Description = report.Description,
            ReportQuery = report.ReportQuery,
            SchemaId = report.SchemaId,
            CategoryId = report.CategoryId,
            IsActive = report.IsActive,
            ReportType = report.ReportType,
            Parameters = report.ReportParameters.OrderBy(p => p.Position).Select(p => new EditReportParameterViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type.ToString(),
                ViewControl = p.ViewControl.ToString(),
                DefaultValue = p.DefaultValue?.Trim(),
                Position = p.Position,
                IsRequired = p.IsRequired,
                DependsOn = p.DependsOn,
                DependencyQuery = p.DependencyQuery,
                Direction = p.Direction.ToString()
            }).ToList()
        };

        // Load cascading data for Select parameters
        /*
        if (schema != null)
        {
            foreach (var param in viewModel.Parameters.Where(p => p.ViewControl == "Select" && !string.IsNullOrEmpty(p.DependencyQuery)))
            {
                try
                {
                    var data = new List<SelectOption>();
                    using (var connection = new OracleConnection(schema.ConnectionString))
                    {
                        await connection.OpenAsync();
                        using (var command = new OracleCommand(param.DependencyQuery, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                data.Add(new SelectOption
                                {
                                    Value = reader.GetString(0),
                                    Text = reader.FieldCount > 1 ? reader.GetString(1) : reader.GetString(0),
                                    ParentValue = reader.FieldCount > 2 ? reader.GetString(2) : null
                                });
                            }
                        }
                    }
                    param.DefaultValue = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading data for parameter {param.Name}");
                }
            }
        }
        */
        var schemas = await _context.Schemas.Select(s => new SelectListItem { Text = s.Name, Value = s.Id }).ToListAsync();
        ViewBag.AvailableSchemas = schemas;

        var categoriesHierarchyQuery = @"
       SELECT
  LPAD('--', 4*(LEVEL-1)) || c.name   AS Name
  ,c.id as Id
FROM
  RV2_Categories c
START WITH
  c.ParentCategoryId IS NULL
CONNECT BY
  PRIOR c.id = c.parentcategoryid
ORDER SIBLINGS BY c.name";

        var categoriesHierarchy = await _context.Categories.FromSqlRaw(categoriesHierarchyQuery)
            .Select(c => new SelectListItem { Text = c.Name, Value = c.Id.ToString() }).ToListAsync();
        ViewBag.CategoriesHierarchy = categoriesHierarchy;

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Edit([FromBody] EditReportViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
            return BadRequest(string.Join(", ", errors));
        }

        var positions = model.Parameters.Select(p => p.Position).OrderBy(p => p).ToList();
        if (positions.Count != positions.Distinct().Count())
            return BadRequest("Parameter positions must be unique");
        if (positions.Any() && (positions.First() != 1 || positions.Last() != positions.Count))
            return BadRequest("Parameter positions must be sequential starting from 1");

        try
        {
            var report = await _context.Reports
                .Include(r => r.ReportParameters)
                .FirstOrDefaultAsync(r => r.Id == model.Id);

            if (report == null) return NotFound();

            report.Name = model.Name;
            report.Description = model.Description;
            report.ReportQuery = model.ReportQuery;
            report.SchemaId = model.SchemaId;
            report.CategoryId = model.CategoryId;
            report.IsActive = model.IsActive;
            report.ReportType = model.ReportType;
            report.ModifiedDate = DateTime.UtcNow;

            report.ReportParameters.Clear();
            report.ReportParameters = model.Parameters.Select(p => new ReportParameter
            {
                Id = p.Id ?? Guid.NewGuid().ToString(),
                Name = p.Name,
                Description = p.Description,
                Type = Enum.Parse<OracleDbType>(p.Type, true),
                ViewControl = Enum.Parse<ViewControl>(p.ViewControl, true),// ReportType.STOREDPROCEDURE ? ViewControl.TextBox : Enum.Parse<ViewControl>(p.ViewControl, true),
                DefaultValue = p.DefaultValue,
                Position = p.Position,
                IsRequired = p.IsRequired,
                DependsOn = p.DependsOn,
                DependencyQuery = p.DependencyQuery,
                Direction = Enum.TryParse<ParameterDirection>(p.Direction, out var dir) ? dir : ParameterDirection.Input
            }).ToList();

            await _context.SaveChangesAsync();

            _logger.LogInformation("Report updated successfully with ID: {ReportId}", report.Id);
            return Ok(new { message = "Report updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating report");
            return StatusCode(500, "An error occurred while updating the report");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        var report = await _context.Reports
            .Include(r => r.Schema)
            .Include(r => r.Category)
            .Include(r => r.CreatedBy)
            .Include(r => r.ReportParameters)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return NotFound();

        var viewModel = new DeleteReportViewModel
        {
            Id = report.Id,
            Name = report.Name,
            Description = report.Description,
            SchemaName = report.Schema.Name,
            CategoryName = report.Category.Name,
            ParametersCount = report.ReportParameters.Count,
            CreatedDate = report.CreatedDate,
            CreatedByUserName = report.CreatedBy?.UserName ?? "N/A"
        };

        return View(viewModel);
    }

    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null) return NotFound();

        _context.Reports.Remove(report);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Report deleted successfully with ID: {ReportId}", id);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> GetReportById(string id)
    {
        var report = await _context.Reports
            .Include(r => r.ReportParameters)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            _logger.LogWarning($"Report with ID {id} not found");
            return NotFound();
        }
        ExecuteReportViewModel rpt = new ExecuteReportViewModel
        {
            Id = report.Id,
            Name = report.Name,
            Description = report.Description,
            Parameters = report.ReportParameters.Select(p => new ParameterViewModel { Name = p.Name, Value = p.DefaultValue ?? "" }).ToList()
        };

        var executions = await _context.Executions
            .Where(e => e.ReportId == report.Id)
            .Include(e => e.User)
            .OrderByDescending(e => e.ExecutionDate)
            .Select(e => new reportmangerv2.ViewModels.ExecutionSummaryViewModel
            {
                Id = e.Id,
                ExecutionDate = e.ExecutionDate,
                Status = e.ExecutionStatus.ToString(),
                User = e.User != null ? e.User.UserName : null,
                ResultFilePath = e.ResultFilePath,
                Duration = e.Duration,
                // add parameters to view it as tooltip on execution row
                Parameters = e.ExecutionParameters.Select(ep => new reportmangerv2.ViewModels.ParameterSummaryViewModel
                {
                    Name = ep.Name!,
                    Value = ep.Value!
                }).ToList()

            }).ToListAsync();

        rpt.Executions = executions;

       // _logger.LogInformation($"Report with ID {id} retrieved successfully");
        return PartialView("_ExecuteReport", rpt);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadExecutionResult(string id)
    {
        var exec = await _context.Executions.FindAsync(id);
        if (exec == null) return NotFound();
        if (exec.ExecutionStatus != ExecutionStatus.Completed) return NotFound();
        if (exec.ExecutionDate < DateTime.Now.AddDays(-7)) return NotFound();
        if (string.IsNullOrEmpty(exec.ResultFilePath)) return NotFound();
        var absultepath = Path.Combine(_env.ContentRootPath, exec.ResultFilePath);
        if (!System.IO.File.Exists(absultepath)) return NotFound();
        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        var contentType = provider.TryGetContentType(absultepath, out var ct) ? ct : "application/octet-stream";
        //log user and execution details
        _logger.LogInformation($"User {User.Identity?.Name} downloaded execution result for Execution ID {id} at {DateTime.UtcNow}. File path: {absultepath}");
        return PhysicalFile(absultepath, contentType, Path.GetFileName(absultepath));
    }

    [HttpPost]
    public async Task<IActionResult> GetProcedureParameters([FromBody] reportmangerv2.DTOs.GetProcedureParametersRequest request)
    {
        if (string.IsNullOrEmpty(request?.ProcedureName) || string.IsNullOrEmpty(request?.SchemaId))
            return BadRequest("Procedure name and schema are required");

        var schema = await _context.Schemas.FindAsync(request.SchemaId);
        if (schema == null) return NotFound("Schema not found");

        try
        {
            using var conn = new OracleConnection(schema.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new OracleCommand(@"
                SELECT ARGUMENT_NAME, DATA_TYPE, IN_OUT, POSITION
                FROM ALL_ARGUMENTS
                WHERE UPPER(OBJECT_NAME) = UPPER(:procName)
                  AND ARGUMENT_NAME IS NOT NULL AND SUBPROGRAM_ID = 1
                ORDER BY POSITION", conn);
            cmd.Parameters.Add(new OracleParameter("procName", request.ProcedureName));
            using var reader = await cmd.ExecuteReaderAsync();
            var parameters = new List<object>();
            while (await reader.ReadAsync())
            {
                parameters.Add(new
                {
                    name = reader.GetString(0),
                    type = MapOracleType(reader.GetString(1)),
                    direction = reader.GetString(2) switch { "OUT" => "Output", "IN/OUT" => "InputOutput", _ => "Input" },
                    position = reader.GetInt32(3)
                });
            }
            return Ok(parameters);
        }
        catch (OracleException ex)
        {
            _logger.LogError(ex, "Error fetching procedure parameters");
            return BadRequest(ex.Message);
        }
    }
    private async Task<List<ReportParameter>> GetStroredProcedureParams(string procedureName, string schemaConnectionString)
    {
        
        using var conn = new OracleConnection(schemaConnectionString);
        await conn.OpenAsync();
        using var cmd = new OracleCommand(@"
                SELECT ARGUMENT_NAME, DATA_TYPE, IN_OUT, POSITION
                FROM ALL_ARGUMENTS
                WHERE UPPER(OBJECT_NAME) = UPPER(:procName)
                  AND ARGUMENT_NAME IS NOT NULL AND SUBPROGRAM_ID = 1 
                ORDER BY POSITION", conn);

        cmd.Parameters.Add(new OracleParameter("procName", procedureName));
                using var reader = await cmd.ExecuteReaderAsync();
                var parameters = new List<ReportParameter>();
                while (await reader.ReadAsync())
                {
                    parameters.Add(new ReportParameter
                    {
                        Name = reader.GetString(0),
                        Type = Enum.Parse<OracleDbType>(MapOracleType(reader.GetString(1)), true),
                        Direction = reader.GetString(2) switch { "OUT" => ParameterDirection.Output, "IN/OUT" => ParameterDirection.InputOutput, _ => ParameterDirection.Input },
                        Position = reader.GetInt32(3)
                    });
                }
                return parameters;

    }
    private static string MapOracleType(string oracleTypeName) => oracleTypeName.ToUpper() switch
    {
        "NUMBER" or "FLOAT" => "Decimal",
        "DATE" => "Date",
        "TIMESTAMP" => "TimeStamp",
        "CLOB" or "NCLOB" => "Clob",
        "REF CURSOR" => "RefCursor",
        _ => "Varchar2"
    };

    public async Task<(bool, string)> ValidateByExecution(Report report)
    {
        if (report.ReportParameters.Count == 0 || report.Id is null || string.IsNullOrEmpty(report.ReportQuery)) return (false, "Error in parameters or query");
        using var conn = new OracleConnection(report.Schema.ConnectionString);
        conn.Open();
        using var cmd = new OracleCommand(report.ReportQuery, conn);
        foreach (var p in report.ReportParameters)
        {
            var oracleParam = new OracleParameter { ParameterName = p.Name, Direction = ParameterDirection.Input };
            switch (p.Type)
            {
                case OracleDbType.Date: oracleParam.OracleDbType = OracleDbType.Date; oracleParam.Value = DateTime.Now; break;
                case OracleDbType.Decimal: oracleParam.OracleDbType = OracleDbType.Decimal; oracleParam.Value = 0m; break;
                default: oracleParam.OracleDbType = OracleDbType.Varchar2; oracleParam.Value = string.Empty; break;
            }
            cmd.Parameters.Add(oracleParam);
        }
        try
        {
            cmd.ExecuteReader(CommandBehavior.SchemaOnly);
            return (true, string.Empty);
        }
        catch (OracleException ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<(bool, string)> Validate(Report report)
    {
        if (report.ReportParameters.Count == 0 || report.Id is null || string.IsNullOrEmpty(report.ReportQuery)) return (false, "Error in parameters or query");
        using var conn = new OracleConnection(report.Schema.ConnectionString);
        conn.Open();
        using var cmd = new OracleCommand(report.ReportQuery, conn);
        foreach (var p in report.ReportParameters)
        {
            var oracleParam = new OracleParameter { ParameterName = p.Name, Direction = ParameterDirection.Input };
            switch (p.Type)
            {
                case OracleDbType.Date: oracleParam.OracleDbType = OracleDbType.Date; oracleParam.Value = DateTime.Now; break;
                case OracleDbType.Decimal: oracleParam.OracleDbType = OracleDbType.Decimal; oracleParam.Value = 0m; break;
                default: oracleParam.OracleDbType = OracleDbType.Varchar2; oracleParam.Value = string.Empty; break;
            }
            cmd.Parameters.Add(oracleParam);
        }
        try
        {
            cmd.Prepare();
            return (true, string.Empty);
        }
        catch (OracleException ex)
        {
            return (false, ex.Message);
        }
    }
    
}
public class ReportInfo
{
    public string ReportId { get; set; } = string.Empty;
    public string? ReportName { get; set; } = string.Empty;
    public string? ReportDescription { get; set; } //= string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string? CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedByUserName { get; set; } = string.Empty;
  //  public ReportType ReportType { get; set; }
}
