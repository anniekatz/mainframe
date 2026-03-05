namespace Mainframe.Models;

public class Subtask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public override string ToString() => Name;
}
