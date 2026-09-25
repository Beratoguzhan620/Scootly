using RabbitMQ.Client;

namespace Scootly.Infrastructure.Messaging;

public sealed class RabbitMqConnectionProvider : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMqConnectionProvider(string hostName, string userName, string password)
    {
        _factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };
    }

    public async Task<IConnection> GetConnectionAsync()
    {
        if (_connection is not null && _connection.IsOpen)
            return _connection;

        await _lock.WaitAsync();
        try
        {
            if (_connection is not null && _connection.IsOpen)
                return _connection;

            _connection = await _factory.CreateConnectionAsync();
            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}