namespace EventDrivenDemo.Functions.Lib;

public class FileRow
{
    public required string Name { get; set; }

    public required string Type {get; set; }  

    public required string Location { get; set; }

    public string? Notes { get; set; }

    public required DateTime DateEntered { get; set; }
}