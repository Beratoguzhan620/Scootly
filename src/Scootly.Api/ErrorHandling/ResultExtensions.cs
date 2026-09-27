using Microsoft.AspNetCore.Mvc;
using Scootly.Domain.Common;

namespace Scootly.Api.ErrorHandling;

public static class ResultExtensions
{
    /// <summary>Başarısız bir <see cref="Result"/>'ı, hata türüne uygun durum koduyla ProblemDetails yanıtına çevirir.</summary>
    public static IActionResult ToProblem(this ControllerBase controller, Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Başarılı bir sonuç hata yanıtına çevrilemez.");

        var (statusCode, title) = result.ErrorType switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Bulunamadı"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Yetkisiz işlem"),
            _ => (StatusCodes.Status409Conflict, "İşlem gerçekleştirilemedi")
        };

        return controller.Problem(detail: result.Error, statusCode: statusCode, title: title);
    }

    public static IActionResult BadRequestProblem(this ControllerBase controller, string error)
        => controller.Problem(detail: error, statusCode: StatusCodes.Status400BadRequest, title: "Geçersiz istek");
}
