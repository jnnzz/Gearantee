using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;

namespace ASI.Basecode.WebApp.TagHelpers
{
    [HtmlTargetElement("label", Attributes = "asp-for")]
    public sealed class RequiredLabelTagHelper : TagHelper
    {
        [HtmlAttributeName("asp-for")]
        public ModelExpression For { get; set; }

        // Append after MVC has supplied the label's display name.
        public override int Order => 1000;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!For.Metadata.IsRequired || For.Metadata.ModelType == typeof(bool) ||
                Nullable.GetUnderlyingType(For.Metadata.ModelType) == typeof(bool)) return;

            output.Content.AppendHtml("<span class=\"required-marker\" data-required-marker><span aria-hidden=\"true\"> *</span><span class=\"sr-only\"> (required)</span></span>");
        }
    }
}
