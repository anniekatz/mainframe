namespace Mainframe.Models;

public class ChargeCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";

    public override string ToString() => string.IsNullOrWhiteSpace(Description)
        ? Code
        : $"{Code} - {Description}";
}
