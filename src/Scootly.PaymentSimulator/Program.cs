using Scootly.PaymentSimulator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient("webhooks", client => client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddOptions<WebhookOptions>()
    .BindConfiguration(WebhookOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<FailureInjector>();
builder.Services.AddSingleton<IdempotencyStore>();
builder.Services.AddSingleton<WebhookSender>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPaymentEndpoints();
app.MapGet("/health/live", () => Results.Ok());

app.Run();
