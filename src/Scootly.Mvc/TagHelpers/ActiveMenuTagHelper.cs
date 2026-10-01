using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Scootly.Mvc.TagHelpers;

[HtmlTargetElement(Attributes = "active-controller")]
public sealed class ActiveMenuTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public string ActiveController { get; set; } = string.Empty;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("active-controller");

        var currentController = ViewContext.RouteData.Values["controller"]?.ToString();

        if (string.Equals(currentController, ActiveController, StringComparison.OrdinalIgnoreCase))
        {
            output.Attributes.SetAttribute("class", MergeClass(output, "active"));
        }
    }

    private static string MergeClass(TagHelperOutput output, string newClass)
    {
        var existing = output.Attributes["class"]?.Value?.ToString();
        return string.IsNullOrWhiteSpace(existing) ? newClass : $"{existing} {newClass}";
    }
}