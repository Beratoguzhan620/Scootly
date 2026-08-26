using Scootly.Domain.Common;
using Xunit;

namespace Scootly.Domain.UnitTests;

// Bu sınıf sadece test amaçlı — gerçek projede kullanılmayacak
public class TestEntity : Entity
{
    public TestEntity(Guid id) : base(id) { }
}

public class EntityTests
{
    [Fact]
    public void Ayni_Id_Ile_Iki_Entity_Esit_Olmali()
    {
        // Arrange (hazırlık): önce test verisini kur
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act (uygula): test edilecek davranışı çağır
        var sonuc = entity1.Equals(entity2);

        // Assert (doğrula): beklediğin sonucu kontrol et
        Assert.True(sonuc);
    }

    [Fact]
    public void Farkli_Id_Ile_Iki_Entity_Esit_Olmamali()
    {
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        var sonuc = entity1.Equals(entity2);

        Assert.False(sonuc);
    }

    [Fact]
    public void Ayni_Id_Ile_Iki_Entity_Ayni_HashCode_Uretmeli()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    }

    public class BaskaTestEntity : Entity
    {
        public BaskaTestEntity(Guid id) : base(id) { }
    }

    [Fact]
    public void Ayni_Id_Farkli_Tip_Esit_Olmamali()
    {
        var id = Guid.NewGuid();

        Assert.False(new TestEntity(id).Equals(new BaskaTestEntity(id)));
    }
}