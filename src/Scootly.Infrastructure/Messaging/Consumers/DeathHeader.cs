using System.Collections;
using System.Text;

namespace Scootly.Infrastructure.Messaging.Consumers;

/// <summary>
/// RabbitMQ'nun <c>x-death</c> başlığını okur. Başlık, (kuyruk, sebep) çifti başına bir kayıt tutar ve
/// aynı çift tekrar yaşandığında kaydın <c>count</c> alanını artırır; bu yüzden deneme sayısı listenin
/// uzunluğu değil, ilgili kaydın <c>count</c> değeridir.
/// </summary>
public static class DeathHeader
{
    public const string HeaderName = "x-death";

    public static long GetRejectionCount(IDictionary<string, object?>? headers, string queueName)
    {
        if (headers is null || !headers.TryGetValue(HeaderName, out var raw) || raw is not IEnumerable entries)
            return 0;

        long total = 0;

        foreach (var entry in entries)
        {
            if (entry is not IDictionary<string, object?> table)
                continue;

            if (AsString(Get(table, "queue")) != queueName)
                continue;

            if (AsString(Get(table, "reason")) != "rejected")
                continue;

            total += Get(table, "count") switch
            {
                long l => l,
                int i => i,
                _ => 0
            };
        }

        return total;
    }

    private static object? Get(IDictionary<string, object?> table, string key)
        => table.TryGetValue(key, out var value) ? value : null;

    private static string? AsString(object? value) => value switch
    {
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string s => s,
        _ => null
    };
}
