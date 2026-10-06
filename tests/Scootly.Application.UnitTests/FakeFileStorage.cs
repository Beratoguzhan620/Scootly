using Scootly.Application.Abstractions;

namespace Scootly.Application.UnitTests;

internal sealed class FakeFileStorage : IFileStorage
{
    public bool IsEnabled { get; set; } = true;

    public bool FailOnPut { get; set; }

    public List<(string Key, string ContentType, int Length)> Puts { get; } = [];

    public List<string> Deletes { get; } = [];

    public Task PutAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
    {
        if (FailOnPut)
            throw new InvalidOperationException("depolama hatasi");

        Puts.Add((objectKey, contentType, content.Length));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        Deletes.Add(objectKey);
        return Task.CompletedTask;
    }

    public string CreateDownloadUrl(string objectKey, TimeSpan lifetime) => "http://fake/" + objectKey;
}
