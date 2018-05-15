using System.Linq;
using System.Threading.Tasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Menu.Models;
using YesSql;
using YesSql.Services;

namespace OrchardCore.Menu.Handlers
{
    public class MenuItemListPartMenuItemHandler : ContentHandlerBase
    {
        private readonly IContentDefinitionManager _contentDefinitionManager;
        private readonly ISession _session;

        public MenuItemListPartMenuItemHandler(IContentDefinitionManager contentDefinitionManager, ISession session)
        {
            _session = session;
            _contentDefinitionManager = contentDefinitionManager;
        }

        // Removes the deleted content item from the MenuItems list of any MenuItemsListPart referencing that content item.
        public override async Task RemovedAsync(RemoveContentContext context)
        {
            var contentDefinition = _contentDefinitionManager.GetTypeDefinition(context.ContentItem.ContentType);

            // We are only interested in MenuItem content items.
            if (contentDefinition.GetSettings<ContentTypeSettings>().Stereotype != "MenuItem")
            {
                return;
            }

            var menuItemsListPartDefinition = _contentDefinitionManager.GetPartDefinition(nameof(MenuItemsListPart));

            if (menuItemsListPartDefinition == null)
            {
                return;
            }

            // Get all content types that have the MenuItemsListPart attached.
            var menuItemListTypesQuery =
                from typeDefinition in _contentDefinitionManager.ListTypeDefinitions()
                where typeDefinition.Parts.Any(x => x.PartDefinition == menuItemsListPartDefinition)
                select typeDefinition.Name;

            var menuItemListTypes = menuItemListTypesQuery.ToList();

            // Get all content items with a content type that has the MenuItemsListPart attached.
            var query = _session.Query<ContentItem, ContentItemIndex>(x => x.ContentType.IsIn(menuItemListTypes));
            var menuItemLists = await query.ListAsync();

            // Remove the deleted content item from each menu item list.
            foreach (var menuItemList in menuItemLists)
            {
                var part = menuItemList.As<MenuItemsListPart>();

                part.MenuItems.Remove(context.ContentItem);
            }
        }
    }
}