namespace Mainframe.Models;

public class ChargeCodeSummary
{
    public string Name { get; set; } = "";
    public decimal TotalHours { get; set; }
    public List<ProjectHoursSummary> Projects { get; set; } = [];
}

public class ProjectHoursSummary
{
    public string Name { get; set; } = "";
    public decimal TotalHours { get; set; }
}

public class ProjectSummary
{
    public string Name { get; set; } = "";
    public decimal TotalHours { get; set; }
    public List<TaskSummary> Tasks { get; set; } = [];
}

public class TaskSummary
{
    public string Name { get; set; } = "";
    public decimal TotalHours { get; set; }
    public List<SubtaskSummary> Subtasks { get; set; } = [];
}

public class SubtaskSummary
{
    public string Name { get; set; } = "";
    public decimal TotalHours { get; set; }
}

public class DailySummary
{
    public DateOnly Date { get; set; }
    public string DisplayDate => Date.ToString("ddd, MMM d yyyy");
    public decimal TotalHours { get; set; }
    public List<DailyEntryDetail> Entries { get; set; } = [];
}

public class DailyEntryDetail
{
    public string ChargeCode { get; set; } = "";
    public string Project { get; set; } = "";
    public string Task { get; set; } = "";
    public string Subtask { get; set; } = "";
    public decimal Hours { get; set; }
    public string Notes { get; set; } = "";
}
