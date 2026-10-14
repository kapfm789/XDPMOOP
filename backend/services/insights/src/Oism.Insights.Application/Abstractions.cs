namespace Oism.Insights.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
