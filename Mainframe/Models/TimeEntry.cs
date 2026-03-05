namespace Mainframe.Models;

public class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public Guid ChargeCodeId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? SubtaskId { get; set; }
    public decimal Hours { get; set; }
    public string Notes { get; set; } = "";
}
