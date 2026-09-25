namespace Scootly.Application.Abstractions;

public interface IOutboxWriter
{
    void Write<T>(string eventType, T payload);
}