using System.ComponentModel.DataAnnotations;

namespace Scootly.Mvc.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Parola")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Beni hatırla")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
