using System.Data;
using Oracle.ManagedDataAccess.Client;
using reportmangerv2.Domain;
using reportmangerv2.Enums;

namespace reportmangerv2.ViewModels;

public class ParameterViewModel
{
    public required string Name { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
    public string? DisplayName { get; set; } 
    public bool IsRequired { get; set; }=false;
    public ViewControl ViewControl { get; set; }=ViewControl.TextBox;
    public string? DependsOn { get; set; }
    public string? DependencyQuery { get; set; }
    public int? Position {get;set;}
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    // public string DefaultValue {get;set;}="";
    public bool IsRefCursor { get; set; } = false;
    public OracleDbType Type { get; set; } = OracleDbType.Varchar2;
    public static ParameterViewModel FromReportParameter(ReportParameter p)
    {
        var pvm= new ParameterViewModel
        {
               Name = p.Name,
                Value =  p.DefaultValue ?? "",
                ViewControl = p.ViewControl,
                IsRequired = p.IsRequired,
                DependsOn = p.DependsOn,
                DependencyQuery = p.DependencyQuery,
                Direction = p.Direction,
                IsRefCursor = p.Type == OracleDbType.RefCursor
        };
        if(p.Type==OracleDbType.Date && !string.IsNullOrEmpty(pvm.Value))
        {
            if (DateTime.TryParse(pvm.Value, out DateTime dateValue))
            {
                pvm.Value = dateValue.ToString("dd/MM/yyyy");
                return pvm;
            }
              pvm.Value = pvm.Value.ToLower().Replace("{firstdayofmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayofmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)).ToString("dd/MM/yyyy"))
            .Replace("{firstdayoflastmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayoflastmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1).ToString("dd/MM/yyyy"))
            .Replace("{firstdayofyear}", new DateTime(DateTime.Now.Year, 1, 1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayofyear}", new DateTime(DateTime.Now.Year, 12, 31).ToString("dd/MM/yyyy"))
            .Replace("{firstdayoflastyear}", new DateTime(DateTime.Now.Year - 1, 1, 1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayoflastyear}", new DateTime(DateTime.Now.Year - 1, 12, 31).ToString("dd/MM/yyyy"));
            if (DateTime.TryParse(pvm.Value, out dateValue))
            {
                pvm.Value = dateValue.ToString("dd/MM/yyyy");
            }else
            {
                pvm.Value = "";
            }
            return pvm;
        }
        return pvm;
       
      
        
    }
    public static ParameterViewModel AdjustReportParameter(ParameterViewModel p)
    {
        if(p.Type==OracleDbType.Date && !string.IsNullOrEmpty(p.Value))
        {
            if (DateTime.TryParse(p.Value, out DateTime dateValue))
            {
                p.Value = dateValue.ToString("dd/MM/yyyy");
                return p;
            }
              p.Value = p.Value.ToLower().Replace("{firstdayofmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayofmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)).ToString("dd/MM/yyyy"))
            .Replace("{firstdayoflastmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayoflastmonth}", new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1).ToString("dd/MM/yyyy"))
            .Replace("{firstdayofyear}", new DateTime(DateTime.Now.Year, 1, 1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayofyear}", new DateTime(DateTime.Now.Year, 12, 31).ToString("dd/MM/yyyy"))
            .Replace("{firstdayoflastyear}", new DateTime(DateTime.Now.Year - 1, 1, 1).ToString("dd/MM/yyyy"))
            .Replace("{lastdayoflastyear}", new DateTime(DateTime.Now.Year - 1, 12, 31).ToString("dd/MM/yyyy"));
            if (DateTime.TryParse(p.Value, out dateValue))
            {
                p.Value = dateValue.ToString("dd/MM/yyyy");
            }else
            {
                p.Value = "";
            }
        }
        return p;
    }
}

