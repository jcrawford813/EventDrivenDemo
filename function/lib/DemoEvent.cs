namespace EventDrivenDemo.Functions.Lib;

public class DemoEvent<T> where T : class
{
    public required string EventName { get; set; } 

    public required DateTime EventDate { get; set; }

    public required Guid Source { get; set; }

    public required int CorrelationId { get; set; }

    public required T EventData { get; set; }
}