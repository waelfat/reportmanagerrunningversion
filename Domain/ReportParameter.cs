using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using reportmangerv2.Enums;

namespace reportmangerv2.Domain;

public class ReportParameter
{
    [Key]
    public string Id {get;set;}=Guid.NewGuid().ToString();
    public string Name {get;set;}
    public string? Description {get;set;}
    public OracleDbType Type {get;set;}=OracleDbType.Varchar2;
    public ViewControl ViewControl {get;set;}=ViewControl.TextBox;
    public string? DefaultValue {get;set;}
    [Range(1, int.MaxValue)]
    public int Position {get;set;}
    public bool IsRequired {get;set;}
    public string? DependsOn {get;set;}
    public string? DependencyQuery {get;set;}
    public ParameterDirection Direction {get;set;}=ParameterDirection.Input;
    [ForeignKey("ReportId")]
    public string ReportId {get;set;}
    public Report Report {get;set;}
    // doto : add property to ditermine if we will collect statistics for this parameter or not while writing it to xlsx file
    // doto : add property for the function to be used to collect statistics for this parameter while writing it to xlsx file
}