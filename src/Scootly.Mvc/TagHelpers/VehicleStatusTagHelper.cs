using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Scootly.Mvc.TagHelpers;

[HtmlTargetElement("vehicle-status")]
public sealed class VehicleStatusTagHelper : TagHelper
{
    public string Status { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";

        var (cssClass, label) = Status switch
        {
            "Available" => ("bg-success", "Müsait"),
            "Reserved" => ("bg-warning text-dark", "Rezerve"),
            "InRide" => ("bg-primary", "Sürüşte"),
            "Maintenance" => ("bg-secondary", "Bakımda"),
            "Lost" => ("bg-danger", "Kayıp"),
            _ => ("bg-light text-dark", Status)
        };

        output.Attributes.SetAttribute("class", $"badge {cssClass}");
        output.Content.SetContent(label);
    }
}