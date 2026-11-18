using System;

namespace reportmangerv2.DTOs;

public class JobExecutionsViewModel
{
    public string Id { get; set; }
    public string ProcedureName { get; set; }
    public string UserName { get; set; }
    public string ExecutionStatus { get; set; }
    public bool EmailSent { get; set; }
    public DateTime ExecutionDate { get; set; }
    public TimeSpan Duration { get; set; }

}
