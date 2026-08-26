using System;
using System.Collections.Generic;
using System.Text;

using Scootly.Domain.Common;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class TestEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public class TestAggregate : AggregateRoot
{
    public TestAggregate(Guid id) : base(id) { }

    // protected metodu teste açmak için ince bir kapı
    public void OlayEkle() => AddDomainEvent(new TestEvent());
}

public class AggregateRootTests
{
    [Fact]
    public void Yeni_Aggregate_Bos_Olay_Listesiyle_Baslamali()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void AddDomainEvent_Olayi_Listeye_Eklemeli()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());

        aggregate.OlayEkle();

        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_Listeyi_Bosaltmali()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.OlayEkle();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }
}
