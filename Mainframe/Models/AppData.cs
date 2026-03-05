namespace Mainframe.Models;

public class AppData
{
    public string UserName { get; set; } = "";
    public List<ChargeCode> ChargeCodes { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
    public List<TimeEntry> TimeEntries { get; set; } = [];
}
