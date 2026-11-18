namespace reportmangerv2.ViewModels;

public class ScheduleReportViewModel
{
    public required string ReportId { get; set; }
    public required string CronExpression { get; set; }
    public  DateTime? SelectedDate { get; set; }
    public List<ParameterViewModel> Parameters { get; set; } = [];
}
