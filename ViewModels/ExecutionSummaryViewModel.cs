using System;

namespace reportmangerv2.ViewModels;

public class ExecutionSummaryViewModel
{
    public string Id { get; set; } = null!;
    public DateTime ExecutionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? User { get; set; }
    public string? ResultFilePath { get; set; }
    public TimeSpan Duration { get; set; }
    public List<ParameterSummaryViewModel> Parameters { get; set; } = new();
}
public class ParameterSummaryViewModel
{
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
}
