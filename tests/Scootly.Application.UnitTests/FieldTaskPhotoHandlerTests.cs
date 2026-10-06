using Scootly.Application.FieldOps;
using Scootly.Application.FieldOps.Commands;
using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;
using Xunit;

namespace Scootly.Application.UnitTests;

public class FieldTaskPhotoHandlerTests
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] ExeBytes = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    private sealed class Setup
    {
        public required FieldTaskCommandHandler Handler { get; init; }

        public required FieldTask Entity { get; init; }

        public required Guid OperatorId { get; init; }

        public required FakeFileStorage Storage { get; init; }
    }

    private static Setup Arrange()
    {
        var fieldTasks = new InMemoryFieldTaskRepository();
        var operatorId = Guid.NewGuid();
        var entity = fieldTasks.Store(new FieldTask(Guid.NewGuid(), Guid.NewGuid(), FieldTaskType.BatteryReplacement, TestClock.Now));
        entity.Assign(operatorId, TestClock.Now);

        var storage = new FakeFileStorage();
        var handler = new FieldTaskCommandHandler(fieldTasks, new FakeUnitOfWork(), new FakeClock(), storage);

        return new Setup { Handler = handler, Entity = entity, OperatorId = operatorId, Storage = storage };
    }

    [Fact]
    public async Task Fotografli_Tamamlama_Nesneyi_Yukler_Ve_Anahtari_Kaydeder()
    {
        var s = Arrange();

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, "Tamam", JpegBytes));

        Assert.True(result.IsSuccess);
        Assert.Equal(FieldTaskStatus.Completed, s.Entity.Status);
        Assert.Single(s.Storage.Puts);
        Assert.Equal("image/jpeg", s.Storage.Puts[0].ContentType);
        Assert.Equal(JpegBytes.Length, s.Storage.Puts[0].Length);
        Assert.Equal(s.Storage.Puts[0].Key, s.Entity.PhotoObjectKey);
    }

    [Fact]
    public async Task Nesne_Adi_Sunucuda_Uretilir_Ve_Beklenen_Bicimdedir()
    {
        var s = Arrange();

        await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, null, JpegBytes));

        Assert.Matches(@"^field-tasks/[0-9a-f]{32}/[0-9a-f]{32}\.jpg$", s.Entity.PhotoObjectKey!);
        Assert.Contains(s.Entity.Id.ToString("N"), s.Entity.PhotoObjectKey!);
    }

    [Fact]
    public async Task Fotografsiz_Tamamlama_Depolamaya_Dokunmaz()
    {
        var s = Arrange();
        s.Storage.IsEnabled = false;

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, "Tamam"));

        Assert.True(result.IsSuccess);
        Assert.Null(s.Entity.PhotoObjectKey);
        Assert.Empty(s.Storage.Puts);
    }

    [Fact]
    public async Task Sahte_Fotograf_Reddedilir_Ve_Gorev_Tamamlanmaz()
    {
        var s = Arrange();

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, null, ExeBytes));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(FieldTaskStatus.Assigned, s.Entity.Status);
        Assert.Empty(s.Storage.Puts);
    }

    [Fact]
    public async Task Buyuk_Fotograf_Reddedilir()
    {
        var s = Arrange();
        var big = new byte[FieldTaskPhotoRules.MaxBytes + 1];
        big[0] = 0xFF;
        big[1] = 0xD8;
        big[2] = 0xFF;

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, null, big));

        Assert.False(result.IsSuccess);
        Assert.Equal(FieldTaskStatus.Assigned, s.Entity.Status);
        Assert.Empty(s.Storage.Puts);
    }

    [Fact]
    public async Task Depolama_Kapaliyken_Fotograf_Reddedilir()
    {
        var s = Arrange();
        s.Storage.IsEnabled = false;

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, null, JpegBytes));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Equal(FieldTaskStatus.Assigned, s.Entity.Status);
    }

    [Fact]
    public async Task Atanmamis_Operator_Icin_Yukleme_Yapilmaz()
    {
        var s = Arrange();

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, Guid.NewGuid(), null, JpegBytes));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
        Assert.Empty(s.Storage.Puts);
    }

    [Fact]
    public async Task Yukleme_Hatasinda_Hata_Doner_Ve_Nesne_Kaydedilmez()
    {
        var s = Arrange();
        s.Storage.FailOnPut = true;

        var result = await s.Handler.Handle(new CompleteFieldTaskCommand(s.Entity.Id, s.OperatorId, null, JpegBytes));

        Assert.False(result.IsSuccess);
        Assert.Empty(s.Storage.Puts);
    }
}
