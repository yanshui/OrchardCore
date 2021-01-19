using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json.Linq;
using OrchardCore.Apis.GraphQL;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentFields.ViewModels;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Liquid;
using OrchardCore.Shortcodes.Services;
using Shortcodes;

namespace OrchardCore.ContentFields.GraphQL
{
    public class HtmlFieldQueryObjectType : ObjectGraphType<HtmlField>
    {
        public HtmlFieldQueryObjectType(IStringLocalizer<HtmlFieldQueryObjectType> S)
        {
            Name = "HtmlBodyPart";
            Description = S["Content stored as HTML."];

            Field<StringGraphType>()
                .Name("html")
                .Description(S["the HTML content"])
                .ResolveLockedAsync(RenderHtml);
        }

        private static async Task<object> RenderHtml(ResolveFieldContext<HtmlField> ctx)
        {
            var serviceProvider = ctx.ResolveServiceProvider();
            var shortcodeService = serviceProvider.GetRequiredService<IShortcodeService>();
            var contentDefinitionManager = serviceProvider.GetRequiredService<IContentDefinitionManager>();

            var jObject = ctx.Source.Content as JObject;
            // The JObject.Path is consistent here even when contained in a bag part.
            var jsonPath = jObject.Path;
            var paths = jsonPath.Split('.');
            var partName = paths[0];
            var fieldName = paths[1];
            var contentTypeDefinition = contentDefinitionManager.GetTypeDefinition(ctx.Source.ContentItem.ContentType);
            var contentPartDefinition = contentTypeDefinition.Parts.FirstOrDefault(x => string.Equals(x.Name, partName));
            var contentPartFieldDefintion = contentPartDefinition.PartDefinition.Fields.FirstOrDefault(x => string.Equals(x.Name, fieldName));
            var settings = contentPartFieldDefintion.GetSettings<HtmlFieldSettings>();

            var html = ctx.Source.Html;

            if (!settings.SanitizeHtml)
            {
                var model = new EditHtmlFieldViewModel()
                {
                    Html = ctx.Source.Html,
                    Field = ctx.Source,
                    Part = ctx.Source.ContentItem.Get<ContentPart>(partName),
                    PartFieldDefinition = contentPartFieldDefintion
                };
                var liquidTemplateManager = serviceProvider.GetRequiredService<ILiquidTemplateManager>();
                var htmlEncoder = serviceProvider.GetService<HtmlEncoder>();

                html = await liquidTemplateManager.RenderAsync(html, htmlEncoder, model,
                    scope => scope.SetValue("ContentItem", ctx.Source.ContentItem));
            }

            return await shortcodeService.ProcessAsync(html,
                new Context
                {
                    ["ContentItem"] = ctx.Source.ContentItem,
                    ["PartFieldDefinition"] = contentPartFieldDefintion
                });
        }
    }
}
