namespace Mainframe.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<ProjectTask> Tasks { get; set; } = [];

    public override string ToString() => Name;
}
