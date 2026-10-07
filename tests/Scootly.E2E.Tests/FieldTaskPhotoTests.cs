using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Scootly.Domain.FieldOps;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;

namespace Scootly.E2E.Tests;

/// <summary>
/// Saha görevi fotoğrafı uçtan uca: gerçek Mvc süreci + geçici Postgres + canlı S3 uyumlu depo.
/// SCOOTLY_LIVE_STORAGE_ENDPOINT / _ACCESS_KEY / _SECRET_KEY tanımlı değilse atlanır.
/// </summary>
[Collection(E2ECollection.Name)]
[Trait("Category", "E2E")]
public sealed class FieldTaskPhotoTests(E2EFixture fixture)
{
    private const string SkipReason = "Canlı depo ortam değişkenleri tanımlı değil.";

    private static bool StorageConfigured() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ENDPOINT"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ACCESS_KEY"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_SECRET_KEY"));

    private static byte[] FakeJpeg(int length)
    {
        var bytes = new byte[length];
        new Random(7).NextBytes(bytes);
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private async Task<(FieldTask Task, string OperatorEmail)> SeedAssignedTaskAsync()
    {
        var vehicle = TestData.NewVehicle(brand: $"E2E-{Guid.NewGuid().ToString("N")[..6]}");
        var fieldTask = new FieldTask(Guid.NewGuid(), vehicle.Id, FieldTaskType.Inspection, DateTime.UtcNow);
        var fieldOperator = await fixture.Factory.CreateUserClientAsync(ScootlyRoles.FieldOperator);
        fieldTask.Assign(fieldOperator.UserId, DateTime.UtcNow);

        await fixture.Factory.WithDbContextAsync(async db =>
        {
            db.Vehicles.Add(vehicle);
            db.Set<FieldTask>().Add(fieldTask);
            await db.SaveChangesAsync();
        });

        return (fieldTask, fieldOperator.Email);
    }

    private async Task<FieldTask> SeedCompletedTaskWithPhotoKeyAsync()
    {
        var vehicle = TestData.NewVehicle(brand: $"E2E-{Guid.NewGuid().ToString("N")[..6]}");
        var fieldTask = new FieldTask(Guid.NewGuid(), vehicle.Id, FieldTaskType.Inspection, DateTime.UtcNow);
        var operatorId = Guid.NewGuid();
        fieldTask.Assign(operatorId, DateTime.UtcNow);
        fieldTask.Complete(operatorId, DateTime.UtcNow, "Tamam", $"field-tasks/{fieldTask.Id:N}/{Guid.NewGuid():N}.jpg");

        await fixture.Factory.WithDbContextAsync(async db =>
        {
            db.Vehicles.Add(vehicle);
            db.Set<FieldTask>().Add(fieldTask);
            await db.SaveChangesAsync();
        });

        return fieldTask;
    }

    private Task<FieldTask> LoadAsync(Guid id) =>
        fixture.Factory.WithDbContextAsync(db => db.Set<FieldTask>().AsNoTracking().SingleAsync(t => t.Id == id));

    [Fact]
    public async Task Operator_completes_task_with_photo_and_opens_it_through_a_short_lived_link()
    {
        Assert.SkipUnless(StorageConfigured(), SkipReason);

        var endpoint = Environment.GetEnvironmentVariable("SCOOTLY_LIVE_STORAGE_ENDPOINT")!.TrimEnd('/');
        var (task, email) = await SeedAssignedTaskAsync();
        var jpeg = FakeJpeg(4096);

        var status = 0;
        string? location = null;
        string? cacheControl = null;

        await fixture.RunAsync(nameof(Operator_completes_task_with_photo_and_opens_it_through_a_short_lived_link), async page =>
        {
            await page.LoginAsync(email);
            await page.GotoAsync("/FieldTasks");

            var openRow = page.Locator("tr", new PageLocatorOptions { HasText = task.Id.ToString()[..8] });
            await openRow.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "arac.jpg", MimeType = "image/jpeg", Buffer = jpeg });
            await openRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Tamamla" }).ClickAsync();

            var doneRow = page.Locator("tr", new PageLocatorOptions { HasText = task.Id.ToString()[..8] });
            var link = doneRow.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "A\u00e7" });
            await Assertions.Expect(link).ToBeVisibleAsync();

            var href = await link.GetAttributeAsync("href");
            Assert.NotNull(href);

            var response = await page.Context.APIRequest.GetAsync(
                new Uri(new Uri(fixture.BaseUrl), href).ToString(),
                new APIRequestContextOptions { MaxRedirects = 0 });

            status = response.Status;
            response.Headers.TryGetValue("location", out location);
            response.Headers.TryGetValue("cache-control", out cacheControl);
        });

        Assert.Equal(302, status);
        Assert.Equal("no-store", cacheControl);
        Assert.NotNull(location);
        Assert.StartsWith($"{endpoint}/scootly-e2e-photos/field-tasks/{task.Id:N}/", location!);
        Assert.Contains("X-Amz-Signature=", location!);
        Assert.Contains("X-Amz-Expires=60", location!);

        using var http = new HttpClient();
        using var photo = await http.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        Assert.True((await photo.Content.ReadAsByteArrayAsync()).AsSpan().SequenceEqual(jpeg));

        var saved = await LoadAsync(task.Id);
        Assert.Equal(FieldTaskStatus.Completed, saved.Status);
        Assert.NotNull(saved.PhotoObjectKey);
        Assert.Matches($"^field-tasks/{task.Id:N}/[0-9a-f]{{32}}\\.jpg$", saved.PhotoObjectKey!);
    }

    [Fact]
    public async Task Anonymous_request_to_photo_is_sent_to_login_not_to_storage()
    {
        Assert.SkipUnless(StorageConfigured(), SkipReason);

        var task = await SeedCompletedTaskWithPhotoKeyAsync();
        var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = fixture.BaseUrl });

        try
        {
            var response = await context.APIRequest.GetAsync(
                $"{fixture.BaseUrl}/FieldTasks/Photo/{task.Id}",
                new APIRequestContextOptions { MaxRedirects = 0 });

            response.Headers.TryGetValue("location", out var location);

            Assert.Equal(302, response.Status);
            Assert.Contains("/Account/Login", location);
            Assert.DoesNotContain("X-Amz-Signature", location);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task Driver_role_is_not_redirected_to_storage()
    {
        Assert.SkipUnless(StorageConfigured(), SkipReason);

        var task = await SeedCompletedTaskWithPhotoKeyAsync();
        var driver = await fixture.Factory.CreateDriverClientAsync();

        var status = 0;
        string? location = null;

        await fixture.RunAsync(nameof(Driver_role_is_not_redirected_to_storage), async page =>
        {
            await page.LoginAsync(driver.Email);

            var response = await page.Context.APIRequest.GetAsync(
                $"{fixture.BaseUrl}/FieldTasks/Photo/{task.Id}",
                new APIRequestContextOptions { MaxRedirects = 0 });

            status = response.Status;
            response.Headers.TryGetValue("location", out location);
        });

        Assert.True(status is 302 or 403, $"Beklenmeyen durum kodu: {status}");
        Assert.DoesNotContain("X-Amz-Signature", location ?? string.Empty);
    }

    [Fact]
    public async Task Executable_content_named_jpg_is_rejected_and_task_stays_assigned()
    {
        Assert.SkipUnless(StorageConfigured(), SkipReason);

        var (task, email) = await SeedAssignedTaskAsync();
        byte[] exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00];

        await fixture.RunAsync(nameof(Executable_content_named_jpg_is_rejected_and_task_stays_assigned), async page =>
        {
            await page.LoginAsync(email);
            await page.GotoAsync("/FieldTasks");

            var row = page.Locator("tr", new PageLocatorOptions { HasText = task.Id.ToString()[..8] });
            await row.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "arac.jpg", MimeType = "image/jpeg", Buffer = exe });
            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Tamamla" }).ClickAsync();

            await Assertions.Expect(page.GetByText("JPEG veya PNG")).ToBeVisibleAsync();
        });

        var saved = await LoadAsync(task.Id);
        Assert.Equal(FieldTaskStatus.Assigned, saved.Status);
        Assert.Null(saved.PhotoObjectKey);
    }

    [Fact]
    public async Task Photo_over_five_megabytes_is_rejected_with_a_message_and_task_stays_assigned()
    {
        Assert.SkipUnless(StorageConfigured(), SkipReason);

        var (task, email) = await SeedAssignedTaskAsync();
        var large = FakeJpeg((5 * 1024 * 1024) + (512 * 1024));

        await fixture.RunAsync(nameof(Photo_over_five_megabytes_is_rejected_with_a_message_and_task_stays_assigned), async page =>
        {
            await page.LoginAsync(email);
            await page.GotoAsync("/FieldTasks");

            var row = page.Locator("tr", new PageLocatorOptions { HasText = task.Id.ToString()[..8] });
            await row.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "buyuk.jpg", MimeType = "image/jpeg", Buffer = large });
            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Tamamla" }).ClickAsync();

            await Assertions.Expect(page.GetByText("5 MB")).ToBeVisibleAsync();
        });

        var saved = await LoadAsync(task.Id);
        Assert.Equal(FieldTaskStatus.Assigned, saved.Status);
        Assert.Null(saved.PhotoObjectKey);
    }
}
