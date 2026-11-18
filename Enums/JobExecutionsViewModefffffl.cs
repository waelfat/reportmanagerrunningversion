using System;

namespace reportmangerv2.Enums;

public class JobExecutionsViewModel
{
    public string Id { get; set; }
    public string ReportName { get; set; }
    public string UserName { get; set; }
    public string ExecutionStatus { get; set; }
    public bool EmailSent { get; set; }

}
