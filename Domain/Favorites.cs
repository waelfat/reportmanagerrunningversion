using System;
using System.ComponentModel.DataAnnotations.Schema;
using reportmangerv2.Data;

namespace reportmangerv2.Domain;

public class Favorites
{
   [ForeignKey("ApplicationUser")]
    public required string UserId { get; set; }
    public ApplicationUser ApplicationUser { get; set; }
    [ForeignKey("ReportId")]
    public required string ReportId { get; set; }
    public Report Report { get; set; }

}
