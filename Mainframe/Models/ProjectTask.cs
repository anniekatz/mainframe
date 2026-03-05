namespace Mainframe.Models;

public class ProjectTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Subtask> Subtasks { get; set; } = [];

    public override string ToString() => Name;
}
