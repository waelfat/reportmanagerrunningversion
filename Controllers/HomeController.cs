using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NCrontab;
using Oracle.ManagedDataAccess.Client;
using ReportManagerv2.Services;
using reportmangerv2.Data;
using reportmangerv2.Domain;
using reportmangerv2.Enums;
using reportmangerv2.Models;
using reportmangerv2.ViewModels;

namespace reportmangerv2.Controllers;

[Authorize]
public class HomeController : Controller
{
    // fake context list for reports and reportparameters
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly CurrentActiveExecutionsService _currentActiveExecutionsService;
    private readonly Channel<ExecutionRequest> _executionChannel;
    private readonly IWebHostEnvironment _env;


    public HomeController(ILogger<HomeController> logger, AppDbContext context, UserManager<ApplicationUser> userManager,
    CurrentActiveExecutionsService currentActiveExecutionsService, IWebHostEnvironment env, Channel<ExecutionRequest> executionChannel)
    {

        _logger = logger;
        _context = context;
        _userManager = userManager;
        _currentActiveExecutionsService = currentActiveExecutionsService;
        _executionChannel = executionChannel;
        _env = env;
    }
    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> ExecuteReport(string id)
    {
        //   var report = await _context.Reports.Include(r=>r.ReportParameters).Include(r=>r.Executions ).Where(r=>r.Id==id ).FirstOrDefaultAsync();
        //get report with parameters and last 10 executions for the user and favorite status for the user
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var reportwithExecutions = await _context.Reports.Include(r => r.ReportParameters).Include(r => r.Favorites).Where(r => r.Id == id).Select(r => new
        {
            r.Id,
            r.Name,
            r.Description,
            r.SchemaId,
            Executions = r.Executions.Where(e => e.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier)).OrderByDescending(e => e.ExecutionDate).Take(10),
            ReportParameters = r.ReportParameters.OrderBy(e => e.Position).Select(rp => new { rp.Name, rp.DefaultValue, rp.ViewControl, rp.IsRequired, rp.DependsOn, rp.DependencyQuery, rp.Type, rp.Direction }),
            IsFavorite = r.Favorites.Any(f => f.UserId == userId)
        })
        .FirstOrDefaultAsync();


        if (reportwithExecutions == null)
        {
            _logger.LogWarning($"Report with ID {id} not found");
            return NotFound();
        }

        ExecuteReportViewModel rpt = new ExecuteReportViewModel
        {
            Id = reportwithExecutions.Id,
            Name = reportwithExecutions.Name,
            Description = reportwithExecutions.Description,
            Parameters = reportwithExecutions.ReportParameters.Select(p => new ParameterViewModel
            {
                Name = p.Name,
                Value = p.DefaultValue ?? "",
                ViewControl = p.ViewControl,
                IsRequired = p.IsRequired,
                DependsOn = p.DependsOn,
                DependencyQuery = p.DependencyQuery,
                Direction = p.Direction,
                IsRefCursor = p.Type == OracleDbType.RefCursor,
                Type = p.Type
            }).ToList(),
            IsFavorite = reportwithExecutions.IsFavorite


        };
        // tokens for default values for params 
        // {FirstDayofTheMoth}
        //{LastDayofTheMoth}
        // {FirstDayofThePreviousMonth}
        //{LastDayofThePreviousMonth}
        rpt.Parameters.ForEach(p => ParameterViewModel.AdjustReportParameter(p));


        // Load all select data for cascading parameters
        var schema = await _context.Schemas.FindAsync(reportwithExecutions.SchemaId);
        if (schema != null)
        {
            // if (rpt.Parameters.Any(p => p.ViewControl == ViewControl.Select && !string.IsNullOrEmpty(p.DependencyQuery)))
            // {
            //     await LoadSelectDataForCascadingParameters(rpt, schema);
            // }
            using (var connection = new OracleConnection(schema.ConnectionString))
            {

                foreach (var param in rpt.Parameters.Where(p => p.ViewControl == ViewControl.Select && !string.IsNullOrEmpty(p.DependencyQuery)))
                {
                    try
                    {
                        if (connection.State != ConnectionState.Open)
                        {
                            await connection.OpenAsync();
                        }
                        var data = new List<SelectOption>();
                        using (var command = new OracleCommand(param.DependencyQuery, connection))
                        {
                            using (var reader = await command.ExecuteReaderAsync())
                            {

                                if (reader.HasRows)
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        data.Add(new SelectOption
                                        {
                                            Value = reader.GetString(0),
                                            Text = reader.FieldCount > 1 ? reader.GetString(1) + " - " + reader.GetString(0) : reader.GetString(0),
                                            ParentValue = reader.FieldCount > 2
                                        ? (reader.IsDBNull(2) ? "" : reader.GetString(2))
                                        : ""
                                        });
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"No data returned for parameter {param.Name} using query: SELECT {param.Name}, {param.Name} FROM DUAL");

                                }
                            }
                        }

                        data = data.OrderBy(d => d.Value).ToList();
                        // convert value to be in this format Value - Text if text is not empty and not equal to value

                        param.Value = JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                        _logger.LogInformation($"Loaded {data.Count} options for parameter {param.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error loading data for parameter {param.Name}");
                    }
                }
            }

            #region oldcode for loading select data for cascading parameters
            /*
            foreach (var param in rpt.Parameters.Where(p => p.ViewControl == ViewControl.Select && !string.IsNullOrEmpty(p.DependencyQuery)))
            {
                try
                {
                    var data = new List<SelectOption>();
                    using (var connection = new OracleConnection(schema.ConnectionString))
                    {
                        await connection.OpenAsync();
                        using (var command = new OracleCommand(param.DependencyQuery, connection))
                        {
                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    data.Add(new SelectOption
                                    {
                                        // Use ternary operator to check for nulls at each index
                                        Value = reader.IsDBNull(0) ? "" : reader.GetString(0),

                                        Text = reader.FieldCount > 1
                                         ? (reader.IsDBNull(1) ? "" : reader.GetString(1))
                                         : (reader.IsDBNull(0) ? "" : reader.GetString(0)),

                                        ParentValue = reader.FieldCount > 2
                                        ? (reader.IsDBNull(2) ? "" : reader.GetString(2))
                                        : ""
                                    });
                                }
                            }
                        }
                    }
                    //sort data by Value
                    data = data.OrderBy(d => d.Value).ToList();
                    // convert value to be in this format Value - Text if text is not empty and not equal to value
                    data = data.Select(d => new SelectOption
                    {
                        Value = d.Value,
                        Text = string.IsNullOrEmpty(d.Text) || d.Text == d.Value ? d.Value : $"{d.Value} - {d.Text}",
                        ParentValue = d.ParentValue
                    }).ToList();
                    param.Value = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                    _logger.LogInformation($"Loaded {data.Count} options for parameter {param.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading data for parameter {param.Name}");
                }
            }
        */
            #endregion
        }



        ViewBag.FullName = User.FindFirstValue("FullName");
        ViewBag.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.SchemaId = reportwithExecutions.SchemaId;

        // Get active executions for this user
        var userActiveExecutions = reportwithExecutions.Executions
            .Where(e => (e.ExecutionStatus == ExecutionStatus.Running || e.ExecutionStatus == ExecutionStatus.Pending) &&
                       _currentActiveExecutionsService.IsExecutionActive(e.Id))
            .Select(e => e.Id)
            .ToList();
        ViewBag.ActiveExecutions = userActiveExecutions;
        var reportExecutions = reportwithExecutions.Executions.Where(e => e.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier)).Select(e => new ExecutionSummaryViewModel
        {

            Id = e.Id,
            ExecutionDate = e.ExecutionDate,
            Status = e.ExecutionStatus.ToString(),
            Duration = e.Duration,
            ResultFilePath = e.ResultFilePath,
            User = e.UserId,
            Parameters = e.ExecutionParameters.Select(ep => new ParameterSummaryViewModel
            {
                Name = ep.Name!,
                Value = ep.Value!}).ToList()



        }).ToList();
        //ensure pending and running executions are in active executions otherwise make them failed
        foreach (var execution in reportExecutions.Where(e => e.Status == "Pending" || e.Status == "Running"))
        {
            if (!_currentActiveExecutionsService.IsExecutionActive(execution.Id))
            {
                var executionEntity = await _context.Executions.FindAsync(execution.Id);
                if (executionEntity != null)
                {
                    //if execution has fileresult set it completed otherwise make it failed
                    if (!string.IsNullOrEmpty(executionEntity.ResultFilePath) && System.IO.File.Exists(executionEntity.ResultFilePath))
                    {
                        //if the result file exists then mark as completed otherwise failed


                        executionEntity.ExecutionStatus = ExecutionStatus.Completed;
                        //edit in the main list reportExecutions
                        reportExecutions.First(e => e.Id == execution.Id).Status = "Completed";
                    }
                    else
                    {
                        executionEntity.ExecutionStatus = ExecutionStatus.Failed;
                        reportExecutions.First(e => e.Id == execution.Id).Status = "Failed";

                    }
                    await _context.SaveChangesAsync();
                }
            }
        }
        rpt.Executions = reportExecutions;
        //  _logger.LogInformation($"Report with ID {id} retrieved successfully");
        return PartialView("_ExecuteReport", rpt);
    }

    [HttpPost]

    public async Task<IActionResult> Execute(ExecuteReportViewModel model)
    {
        if (string.IsNullOrEmpty(model?.Id))
        {
            return BadRequest(new { error = "Report id is required" });
        }

        // var report = await _context.Reports.Include(r=>r.Schema).Include(r=>r.ReportParameters).FirstOrDefaultAsync(r=>r.Id==model.Id);
        var report = await _context.Reports.Where(r => r.Id == model.Id).Select(r => new { r.Id, r.Name, r.Schema.ConnectionString, r.ReportQuery, r.ReportType, parameters = r.ReportParameters.OrderBy(p => p.Position).Select(p => new { p.Name, p.DefaultValue, p.Type, p.Position, p.IsRequired }) }).FirstOrDefaultAsync();
        if (report == null)
        {
            return NotFound(new { error = "Report not found" });
        }

        //ensure required parameters are provided
        var missingParameters = new List<string>();
        foreach (var param in report.parameters)
        {
            var providedParam = model.Parameters.FirstOrDefault(p => p.Name == param.Name);
            if (param.IsRequired && param.Type != Oracle.ManagedDataAccess.Client.OracleDbType.RefCursor
                && (providedParam == null || string.IsNullOrEmpty(providedParam.Value)))
            {
                missingParameters.Add(param.Name);
            }
        }
        if (missingParameters.Any())
        {
            return BadRequest(new { error = $"Missing required parameters: {string.Join(", ", missingParameters)}" });
        }

        // For stored procedures load full parameter info including direction
        List<(string Name, OracleDbType Type, ParameterDirection Direction, int Position, string? DefaultValue)> fullParams;
        if (report.ReportType == ReportType.STOREDPROCEDURE)
        {
            var dbParams = await _context.ReportParameters
                .Where(p => p.ReportId == report.Id)
                .OrderBy(p => p.Position)
                .Select(p => new { p.Name, p.Type, p.Direction, p.Position, p.DefaultValue })
                .ToListAsync();
            fullParams = dbParams.Select(p => (p.Name, p.Type, p.Direction, p.Position, p.DefaultValue)).ToList();
        }
        else
        {
            fullParams = report.parameters.Select(p => (p.Name, p.Type, ParameterDirection.Input, p.Position, p.DefaultValue)).ToList();
        }

        var exec = new Execution
        {
            ReportId = report.Id,
            ExecutionStatus = ExecutionStatus.Pending,
            ExecutionType = ExecutionType.Report,

            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous",

            ExecutionParameters = fullParams.Select(p => new ExecutionParameter
            {
                Name = p.Name,
                Value = model.Parameters.FirstOrDefault(mp => mp.Name == p.Name)?.Value?.Trim() ?? p.DefaultValue,
                Type = p.Type,
                Direction = p.Direction
            }).ToList()
        };
        // convert date/decimal values
        foreach (var param in exec.ExecutionParameters)
        {
            if (param.Type == OracleDbType.Date && DateTime.TryParse(param.Value, out DateTime dateValue))
                param.Value = dateValue.ToString("dd/MM/yyyy");
            if (param.Type == OracleDbType.Decimal && decimal.TryParse(param.Value, out decimal decValue))
                param.Value = decValue.ToString();
        }

        _context.Executions.Add(exec);
        await _context.SaveChangesAsync();
        var execType = report.ReportType == ReportType.STOREDPROCEDURE
            ? ExecutionRequesType.StoredProcedure
            : ExecutionRequesType.SqlStatement;
        try
        {
            await _executionChannel.Writer.WriteAsync(new ExecutionRequest
            {
                Id = report.Id,
                ConnectionString = report.ConnectionString,
                ReportTitle = report.Name,
                SQLStatement = report.ReportQuery,
                UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous",
                Type = execType,
                ExecutionId = exec.Id,
                ExecutionType = exec.ExecutionType

            });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error executing report {report.Id}");
            exec.ExecutionStatus = ExecutionStatus.Failed;
            await _context.SaveChangesAsync();
            return StatusCode(500, new { error = "Internal server error" });
        }

        _logger.LogInformation($"Execution {exec.Id} created for report {report.Id}");
        /*
       return Json(new { success = true, executionId = exec.Id });
      */
        var username = User.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";
        var reportjob = report.Name;
        var res= Json(new
        {
            success = true,
            executionId = exec.Id,
            UserName = username,
            ReportJob = reportjob,
            Status = "Running",
            Started = DateTime.Now.ToString("dd/mm/yyyy hh:mm:ss"),
            Duration = "",
            Parameters = exec.ExecutionParameters.Select(ep => new ParameterSummaryViewModel
            {
                Name = ep.Name!,
                Value = ep.Value!
            }).ToList()
        });
        return res;

    }
    [HttpPost]
    public async Task<IActionResult> ScheduleReport([FromBody] ScheduleReportViewModel model)
    {
        //check if the model is not valid
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = "Invalid model state" });
        }
        //check if selected date is provided
        if (model.SelectedDate == null)
        {
            return BadRequest(new { error = "scheduled date is required" });
        }
        if (string.IsNullOrEmpty(model?.ReportId) || string.IsNullOrEmpty(model?.CronExpression))
        {
            return BadRequest(new { error = "Report ID and cron expression are required" });
        }
        if (model.Parameters.Count == 0)
        {
            return BadRequest(new { error = "Parameters are required" });
        }
        // ensure scheduled time is in the future and after the current time plus 2 minutes to allow for processing time
        if (model.SelectedDate <= DateTime.Now.AddMinutes(2))
        {
            return BadRequest(new { error = "Scheduled time must be at least 2 minutes in the future" });
        }

        //ensure parameters have values and names and values are not empty
        foreach (var param in model.Parameters)
        {
            if (string.IsNullOrEmpty(param.Name) || string.IsNullOrEmpty(param.Value))
            {
                return BadRequest(new { error = "Parameter names and values are required" });
            }
        }

        var report = await _context.Reports.Include(r => r.ReportParameters).Where(r => r.Id == model.ReportId)
            .Select(r => new { r.Id, r.Name, r.SchemaId, r.ReportQuery, r.ReportType, parameters = r.ReportParameters.Select(p => new { p.Name, p.DefaultValue, p.Type,p.Direction }) }).FirstOrDefaultAsync();
        ;

        if (report == null)
        {
            return NotFound(new { error = "Report not found" });
        }
        // ensure the report has parameters and the provided parameters match the report parameters and types
        foreach (var param in report.parameters.Where(p => p.Type != OracleDbType.RefCursor))
        {
            var providedParam = model.Parameters.FirstOrDefault(p => p.Name == param.Name);
            if (providedParam == null)
            {
                return BadRequest(new { error = $"Missing parameter: {param.Name}" });
            }
            //check if the value can be converted to the correct type
            try
            {
                if (param.Type == OracleDbType.Date)
                {
                    DateTime.Parse(providedParam.Value);
                }
                else if (param.Type == OracleDbType.Decimal)
                {
                    decimal.Parse(providedParam.Value);
                }
            }
            catch (Exception)
            {
                return BadRequest(new { error = $"Invalid value for parameter: {param.Name}" });
            }
        }

        var job = new ScheduledJob
        {
            ProcedureName = report.ReportType==ReportType.SQLSTATEMENT? report.Name:report.ReportQuery,

            JobType = report.ReportType == ReportType.SQLSTATEMENT ? JobType.SqlStatement : JobType.StoredProcedure,
            CronExpression = model.CronExpression,
            IsActive = true,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Anonymous",
            Description = $"Scheduled job for report {report.Name}",
            JobStatus = ExecutionStatus.Pending,
            // NextRunAt = CrontabSchedule.Parse(model.CronExpression).GetNextOccurrence(DateTime.Now.AddMinutes(2)),
            //assing nextrunat to selecteddate
            NextRunAt = model.SelectedDate,// > DateTime.Now ? model.SelectedDate : CrontabSchedule.Parse(model.CronExpression).GetNextOccurrence(DateTime.Now.AddMinutes(2)),
            ScheduledType = Enums.ScheduledType.Once,
            SchemaId = report.SchemaId,
            SqlStatement = report.ReportQuery,
            Parameters = model.Parameters?.Select(p => new ScheduledJobParameter
            {
                Name = p.Name,
                Value = p.Value,
                Direction = ParameterDirection.Input,
                Type = report.parameters.FirstOrDefault(rp => rp.Name == p.Name)?.Type ?? OracleDbType.Varchar2
            }).ToList() ?? new List<ScheduledJobParameter>()
        };
        // add parameters of type output and refcursor to the job parameters from the report parameters
        if (report.ReportType == ReportType.STOREDPROCEDURE)
        {
            foreach (var reportParam in report.parameters.Where(p => p.Type == OracleDbType.RefCursor || p.Direction == ParameterDirection.Output))
            {
                if (!job.Parameters.Any(p => p.Name == reportParam.Name))
                {
                    job.Parameters.Add(new ScheduledJobParameter
                    {
                        Name = reportParam.Name,
                        Value = null,
                        Direction = ParameterDirection.Output,
                        Type = reportParam.Type
                    });
                }
            }
        }
        var exec = new Execution
        {
            ReportId = report.Id,
            ExecutionStatus = ExecutionStatus.Scheduled,
            ExecutionType = ExecutionType.ScheduledQuery,
            ScheduledJobId = job.Id,
            ExecutionDate = model.SelectedDate ?? DateTime.Now,
            // get id of the user
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous",


            ExecutionParameters = model.Parameters.Select(p => new ExecutionParameter { Name = p.Name, Value = p.Value, Direction = ParameterDirection.Input, Type = report.parameters.FirstOrDefault(rp => rp.Name == p.Name)?.Type ?? OracleDbType.Varchar2 }).ToList()
        };
        _context.Executions.Add(exec);

        _context.Add(job);

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Report {model.ReportId} scheduled with cron expression {model.CronExpression}");
        var res= Json(new { executionId = exec.Id, scheduledTime = job.NextRunAt?.ToString("yyyy-MM-ddTHH:mm:ss") ,parameters = exec.ExecutionParameters.Select(ep => new ParameterSummaryViewModel
        {
            Name = ep.Name!,
            Value = ep.Value!
        }).ToList()});
        return res;
    }
    [HttpPost]
    public async Task<IActionResult> CancelExecution(string executionId)
    {

        if (string.IsNullOrEmpty(executionId))
        {
            return BadRequest(new { error = "Execution ID is required" });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var execution = await _context.Executions.Include(e => e.ScheduledJob).FirstOrDefaultAsync(e => e.Id == executionId);

        if (execution == null)
        {
            return NotFound(new { error = "Execution not found" });
        }
        //check if the execution is scheduled 
        if (execution.ExecutionType == ExecutionType.ScheduledQuery)
        {
            return await CancelScheduled(execution);
        }
        if (execution.ExecutionStatus != ExecutionStatus.Running && execution.ExecutionStatus != ExecutionStatus.Pending)
        {
            return BadRequest(new { error = "Execution cannot be cancelled" });
        }

        // Cancel the execution in the service
        _currentActiveExecutionsService.CancelExecution(executionId);

        _logger.LogInformation($"Execution {executionId} cancelled by user {userId}");
        return Json(new { success = true });
    }
    [HttpPost]
    private async Task<IActionResult> CancelScheduled(Execution execution)
    {
        //get job from executionid


        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        //  var execution = await _context.Executions.Include(e=>e.ScheduledJob).FirstOrDefaultAsync(e => e.Id == executionId) ;

        if (execution == null)
        {
            return NotFound(new { error = "Execution not found" });
        }

        if (execution.ScheduledJob == null)
        {
            return NotFound(new { error = "Scheduled job not found" });
        }
        // Cancel the scheduled job 
        // if user isnot the creator or is not admin
        if (!(User.IsInRole("Admin") || execution.ScheduledJob.CreatedById != userId))
        {
            return Forbid();
        }
        if (execution.ExecutionStatus == ExecutionStatus.Completed || execution.ExecutionStatus == ExecutionStatus.Failed)
        {
            return BadRequest(new { error = "Scheduled execution finished " });
        }

        execution.ScheduledJob.IsActive = false;
        execution.ScheduledJob.JobStatus = ExecutionStatus.Cancelled;
        execution.ExecutionStatus = ExecutionStatus.Cancelled;

        await _context.SaveChangesAsync();
        //check if the exection is currently running
        if (_currentActiveExecutionsService.IsExecutionActive(execution.Id))
        {
            _currentActiveExecutionsService.CancelExecution(execution.Id);
        }

        return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> DeleteExecution(string executionId)
    {
        if (string.IsNullOrEmpty(executionId))
        {
            return BadRequest(new { error = "Execution ID is required" });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var execution = await _context.Executions.Include(e => e.ScheduledJob).FirstOrDefaultAsync(e => e.Id == executionId && e.UserId == userId);

        if (execution == null)
        {
            return NotFound(new { error = "Execution not found" });
        }

        // Delete the execution
        if (execution.ExecutionStatus == ExecutionStatus.Running)
        {
            return BadRequest(new { error = "Running executions cannot be deleted" });
        }
        if (execution.ExecutionType == ExecutionType.ScheduledQuery)
        {
            if (execution.ScheduledJob != null && execution.ScheduledJob.CreatedById != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }
            if (execution.ScheduledJob != null)
            {
                _context.ScheduledJobs.Remove(execution.ScheduledJob);
            }
        }
        _context.Executions.Remove(execution);

        await _context.SaveChangesAsync();

        // Delete the result file if it exists
        if (!string.IsNullOrEmpty(execution.ResultFilePath) && System.IO.File.Exists(execution.ResultFilePath))
        {
            try
            {


                System.IO.File.Delete(execution.ResultFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting file {execution.ResultFilePath}");
            }
        }


        _logger.LogInformation($"Execution {executionId} deleted by user {userId}");
        return Json(new { success = true });
    }
    [HttpGet]
    public async Task<IActionResult> DownloadExecutionResult(string id)
    {
        var exec = await _context.Executions.FindAsync(id);
        if (exec == null) return NotFound();
        if (exec.ExecutionStatus != ExecutionStatus.Completed) return NotFound();
        // if (exec.ExecutionDate < DateTime.Now.AddDays(-7)) return NotFound();
        if (string.IsNullOrEmpty(exec.ResultFilePath)) return NotFound();
        // create list of files paths
        var filePaths = exec.ResultFilePath.Split(',').ToList();
        //check if the file exists
        if (filePaths.Count > 1)
        {
            // Create a zip file
            var zipPath = Path.Combine(_env.ContentRootPath, "Reports", $"report_{exec.Id}.zip");
            if (System.IO.File.Exists(zipPath))
            {
                System.IO.File.Delete(zipPath);
            }
            using (var zip = new ZipArchive(System.IO.File.Create(zipPath), ZipArchiveMode.Create))
            {
                foreach (var path in filePaths)
                {
                    var absPath = Path.Combine(_env.ContentRootPath, path);
                    if (System.IO.File.Exists(absPath))
                    {
                        zip.CreateEntryFromFile(absPath, Path.GetFileName(absPath));
                    }
                }
            }
            // delete the old files
            foreach (var path in filePaths)
            {
                var absPath = Path.Combine(_env.ContentRootPath, path);
                if (System.IO.File.Exists(absPath))
                {
                    System.IO.File.Delete(absPath);
                }
            }

            // update the execution record
            exec.ResultFilePath = $"Reports\\{Path.GetFileName(zipPath)}";
            await _context.SaveChangesAsync();
            // return the zip file
            var zipContentType = "application/zip";
            // log user and execution details
            _logger.LogInformation($"User {User.Identity?.Name} downloaded execution result for Execution ID {id} at {DateTime.UtcNow}. File path: {zipPath}");
            return PhysicalFile(zipPath, zipContentType, Path.GetFileName(zipPath));
        }
        var absultepath = Path.Combine(_env.ContentRootPath, exec.ResultFilePath);

        //E:\projects\dotnetprojects\vscodeprojects\reportmanagerv2\reportmangerv2\wwwroot\Reports\report_Monthly Financial Report_20260306_232739.xlsx

        if (!System.IO.File.Exists(absultepath)) return NotFound();
        var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        var contentType = provider.TryGetContentType(absultepath, out var ct) ? ct : "application/octet-stream";
        _logger.LogInformation($"User {User.Identity?.Name} downloaded execution result for Execution ID {id} at {DateTime.UtcNow}. File path: {absultepath}");

        return PhysicalFile(absultepath, contentType,
            Path.GetFileName(absultepath)

        );
    }

    [HttpPost]
    public async Task<IActionResult> GetCascadingData([FromBody] CascadingDataRequest request)
    {
        if (string.IsNullOrEmpty(request?.SchemaId) || string.IsNullOrEmpty(request?.Query))
        {
            return BadRequest(new { error = "Schema ID and query are required" });
        }

        var schema = await _context.Schemas.FindAsync(request.SchemaId);
        if (schema == null)
        {
            return NotFound(new { error = "Schema not found" });
        }

        try
        {
            var query = request.Query;
            if (!string.IsNullOrEmpty(request.ParentValue))
            {
                query = query.Replace(":parentValue", $"'{request.ParentValue}'");
            }

            var data = new List<SelectOption>();
            using (var connection = new OracleConnection(schema.ConnectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand(query, connection))
                {
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
            }

            return Json(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching cascading data");
            return StatusCode(500, new { error = "Error fetching data" });
        }
    }
    [HttpPost]
    public async Task<IActionResult> ToggleFavorite(string reportId)
    {
        if (string.IsNullOrEmpty(reportId))
        {
            return BadRequest(new { error = "Report ID is required" });
        }
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }
        var existingFavorite = await _context.Favorites.FirstOrDefaultAsync(f => f.ReportId == reportId && f.UserId == userId);
        if (existingFavorite != null)
        {
            _context.Favorites.Remove(existingFavorite);
            await _context.SaveChangesAsync();
            return Json(new { success = true, isFavorite = false });
        }
        var favorite = new Favorites
        {
            ReportId = reportId,
            UserId = userId
        };
        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync();
        return Json(new { success = true, isFavorite = true });
    }
    // get current user active executions
    [HttpGet]
    public IActionResult GetActiveExecutions()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            return Unauthorized();
        }
        var activeExecutions = _currentActiveExecutionsService.GetActiveExecutionsForUser(userId);
        if (activeExecutions == null || !activeExecutions.Any())
        {
            //return not found

            return NotFound(new { error = "No active executions found" });

        }

        return Json(activeExecutions);
    }




    //[HttpPost]
    // public IActionResult AddToFavorites(string reportId)
    // {
    //     if (string.IsNullOrEmpty(reportId))
    //     {
    //         return BadRequest(new { error = "Report ID is required" });
    //     }
    //     var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //     if (userId == null)
    //     {
    //         return Unauthorized();
    //     }
    //     var existingFavorite = _context.Favorites.FirstOrDefault(f => f.ReportId == reportId && f.UserId == userId);
    //     if (existingFavorite != null)
    //     {
    //         return BadRequest(new { error = "Report is already in favorites" });
    //     }
    //     var favorite = new Favorites
    //     {
    //         ReportId = reportId,
    //         UserId = userId
    //     };
    //     _context.Favorites.Add(favorite);
    //     _context.SaveChanges();
    //     return Json(new { success = true });
    // }
    // //remove from favorites
    // [HttpPost]
    // public IActionResult RemoveFromFavorites(string reportId)
    // {
    //     if (string.IsNullOrEmpty(reportId))
    //     {
    //         return BadRequest(new { error = "Report ID is required" });
    //     }
    //     var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //     if (userId == null)        {
    //         return Unauthorized();  
    //     }
    //     var existingFavorite = _context.Favorites.FirstOrDefault(f => f.ReportId == reportId && f.UserId == userId);
    //     if (existingFavorite == null)        {
    //         return BadRequest(new { error = "Report is not in favorites" });
    //     }
    //     _context.Favorites.Remove(existingFavorite);
    //     _context.SaveChanges();
    //     return Json(new { success = true });
    // }

}

public class CascadingDataRequest
{
    public string SchemaId { get; set; }
    public string Query { get; set; }
    public string ParentValue { get; set; }
}

public class SelectOption
{
    public string Value { get; set; }
    public string Text { get; set; }
    public string ParentValue { get; set; }
}
