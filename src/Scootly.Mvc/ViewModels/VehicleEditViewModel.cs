using System.ComponentModel.DataAnnotations;
using Scootly.Domain.Fleet;

namespace Scootly.Mvc.ViewModels;

public sealed class VehicleCreateViewModel
{
    [Required(ErrorMessage = "Marka zorunludur.")]
    [StringLength(VehicleModel.BrandMaxLength, ErrorMessage = "Marka en fazla {1} karakter olabilir.")]
    [Display(Name = "Marka")]
    public string Brand { get; set; } = string.Empty;

    [Range(1, VehicleModel.MaxRangeKm, ErrorMessage = "Menzil 1-{2} km arasında olmalı.")]
    [Display(Name = "Menzil (km)")]
    public int RangeKm { get; set; }

    [Range(-90, 90, ErrorMessage = "Enlem -90 ile 90 arasında olmalı.")]
    [Display(Name = "Enlem")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Boylam -180 ile 180 arasında olmalı.")]
    [Display(Name = "Boylam")]
    public double Longitude { get; set; }

    [Range(0, 100, ErrorMessage = "Batarya yüzdesi 0-100 arasında olmalı.")]
    [Display(Name = "Batarya (%)")]
    public int BatteryPercentage { get; set; }
}

public sealed class VehicleEditViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Marka zorunludur.")]
    [StringLength(VehicleModel.BrandMaxLength, ErrorMessage = "Marka en fazla {1} karakter olabilir.")]
    [Display(Name = "Marka")]
    public string Brand { get; set; } = string.Empty;

    [Range(1, VehicleModel.MaxRangeKm, ErrorMessage = "Menzil 1-{2} km arasında olmalı.")]
    [Display(Name = "Menzil (km)")]
    public int RangeKm { get; set; }
}