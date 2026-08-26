using System;
using System.Collections.Generic;
using System.Text;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class VehicleTests
{
    // Her testte dört satır kurucu yazmamak için yardımcı metot
    private static Vehicle AracOlustur()
    {
        return new Vehicle(
            VehicleId.New(),
            new VehicleModel("Segway", 40),
            new GeoPoint(37.00, 35.32),
            new BatteryLevel(90));
    }

    [Fact]
    public void Yeni_Arac_Available_Durumunda_Baslamali()
    {
        var arac = AracOlustur();

        Assert.Equal(VehicleStatus.Available, arac.Status);
    }

    [Fact]
    public void Reserve_Available_Araci_Reserved_Yapmali()
    {
        // Arrange
        var arac = AracOlustur();

        // Act
        arac.Reserve();

        // Assert
        Assert.Equal(VehicleStatus.Reserved, arac.Status);
    }

    [Fact]
    public void Reserve_Iki_Kez_Cagrilirsa_DomainException_Firlatmali()
    {
        var arac = AracOlustur();
        arac.Reserve();

        // Zaten rezerve olan araç tekrar rezerve edilemez
        Assert.Throws<DomainException>(() => arac.Reserve());
    }

    [Fact]
    public void StartRide_Rezerve_Edilmemis_Aracta_DomainException_Firlatmali()
    {
        var arac = AracOlustur();

        // Available durumundan doğrudan sürüşe geçilemez, önce Reserve gerekir
        Assert.Throws<DomainException>(() => arac.StartRide());
    }

    [Fact]
    public void StartRide_Rezerve_Aractan_InRide_Yapmali()
    {
        var arac = AracOlustur();
        arac.Reserve();

        arac.StartRide();

        Assert.Equal(VehicleStatus.InRide, arac.Status);
    }

    [Fact]
    public void CompleteRide_Surusteki_Araci_Available_Yapmali()
    {
        var arac = AracOlustur();
        arac.Reserve();
        arac.StartRide();

        arac.CompleteRide();

        Assert.Equal(VehicleStatus.Available, arac.Status);
    }

    [Fact]
    public void CompleteRide_Suruste_Olmayan_Aracta_DomainException_Firlatmali()
    {
        var arac = AracOlustur();

        Assert.Throws<DomainException>(() => arac.CompleteRide());
    }

    [Fact]
    public void SendToMaintenance_Available_Aractan_Calismali()
    {
        var arac = AracOlustur();

        arac.SendToMaintenance();

        Assert.Equal(VehicleStatus.Maintenance, arac.Status);
    }

    [Fact]
    public void SendToMaintenance_Surusteki_Aractan_Da_Calismali()
    {
        var arac = AracOlustur();
        arac.Reserve();
        arac.StartRide();

        // Bakıma gönderme her durumdan serbest — kaza, arıza, batarya bitmesi
        arac.SendToMaintenance();

        Assert.Equal(VehicleStatus.Maintenance, arac.Status);
    }

    [Fact]
    public void Bakimdaki_Arac_Rezerve_Edilememeli()
    {
        var arac = AracOlustur();
        arac.SendToMaintenance();

        // Faz 1'in temel invariant'ı: bakımdaki araç kiralanamaz
        Assert.Throws<DomainException>(() => arac.Reserve());
    }

    [Fact]
    public void Yeni_Arac_VehicleRegisteredEvent_Uretmeli()
    {
        var arac = AracOlustur();

        var olay = Assert.Single(arac.DomainEvents);
        Assert.IsType<VehicleRegisteredEvent>(olay);
    }

    [Fact]
    public void Basarili_Gecis_StatusChanged_Olayi_Eklemeli()
    {
        var arac = AracOlustur();

        arac.Reserve();

        // Kayıt olayı + durum değişimi olayı
        Assert.Equal(2, arac.DomainEvents.Count);

        var sonOlay = Assert.IsType<VehicleStatusChangedEvent>(arac.DomainEvents.Last());
        Assert.Equal(VehicleStatus.Available, sonOlay.OldStatus);
        Assert.Equal(VehicleStatus.Reserved, sonOlay.NewStatus);
    }

    [Fact]
    public void Basarisiz_Gecis_Olay_Uretmemeli()
    {
        var arac = AracOlustur();

        Assert.Throws<DomainException>(() => arac.StartRide());

        // Reddedilen geçiş hiçbir iz bırakmamalı — sadece kayıt olayı kalmalı
        Assert.Single(arac.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_Sonrasi_Liste_Bosalmali()
    {
        var arac = AracOlustur();
        arac.Reserve();

        arac.ClearDomainEvents();

        Assert.Empty(arac.DomainEvents);
    }
}