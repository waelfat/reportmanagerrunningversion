using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using reportmangerv2.Data;
using reportmangerv2.Enums;

namespace reportmangerv2.Domain;

public class Report
{
    [Key]

    public string Id { get; set; }=Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public  string? Description { get; set; }
    public required string ReportQuery { get; set; }
    public  DateTime CreatedDate { get; set; }=DateTime.Now;
    public DateTime? ModifiedDate { get; set; }
    [ForeignKey("CreatedById")]
    public string CreatedById { get; set; }
    public  ApplicationUser CreatedBy { get; set; }
    [ForeignKey("SchemaId")]
    public string SchemaId { get; set; }
    public Schema Schema { get; set; }
    public ICollection<ReportParameter> ReportParameters { get; set; }=new List<ReportParameter>();
    public bool IsActive { get; set; } = true;
    public ReportType ReportType { get; set; } = ReportType.SQLSTATEMENT;
    
    [ForeignKey("CategoryId")]
    public string CategoryId { get; set; }

    public Category Category { get; set; }
    public ICollection<Execution> Executions { get; set; } = new List<Execution>();
    public ICollection<Favorites> Favorites { get; set; } = new List<Favorites>();

    

    /// <summary>
    /// Validates the SQL statement by preparing it with Oracle.
    /// Throws OracleException if invalid.
    /// </summary>
    


    
    

}
