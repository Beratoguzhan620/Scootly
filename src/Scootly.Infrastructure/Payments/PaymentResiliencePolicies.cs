using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace Scootly.Infrastructure.Payments;

public static class PaymentResiliencePolicies
{
    /// <summary>
    /// Yalnızca geçici hatalar yeniden denenir ve devre kesiciye sayılır (ağ hatası, zaman aşımı, 5xx, 408, 429).
    /// 402 gibi iş retleri kalıcıdır: yeniden denenmez, devreyi de açmaz. POST isteklerinin tekrar
    /// denenmesi, her istekte gönderilen idempotency anahtarı sayesinde güvenlidir.
    /// </summary>
    public static bool IsTransient(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException or TimeoutRejectedException } => true,
        { Result: { } response } => IsTransientStatusCode(response.StatusCode),
        _ => false
    };

    public static bool IsTransientStatusCode(HttpStatusCode statusCode)
        => (int)statusCode >= 500 || statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;

    public static IHttpClientBuilder AddPaymentResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("payment-pipeline", (pipelineBuilder, context) =>
        {
            var options = context.ServiceProvider.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;

            pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));

            pipelineBuilder.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome))
            });

            pipelineBuilder.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome))
            });

            pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
        });

        return builder;
    }
}
