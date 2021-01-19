using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Users
{
    public class CustomUserSettingsPermissions : IPermissionProvider
    {
        // This permission is never checked it is only used as a template.
        private static readonly Permission ManageOwnCustomUserSettings = new Permission("ManageOwnCustomUserSettings_{0}", "Manage Own Custom User Settings - {0}", new[] { Permissions.ManageUsers });

        private readonly IContentDefinitionManager _contentDefinitionManager;

        public CustomUserSettingsPermissions(IContentDefinitionManager contentDefinitionManager)
        {
            _contentDefinitionManager = contentDefinitionManager;
        }

        public Task<IEnumerable<Permission>> GetPermissionsAsync()
            => Task.FromResult(_contentDefinitionManager.ListTypeDefinitions()
                .Where(x => x.GetSettings<ContentTypeSettings>().Stereotype == "CustomUserSettings")
                .Select(type => CreatePermissionForType(type)));

        public IEnumerable<PermissionStereotype> GetDefaultStereotypes() => Enumerable.Empty<PermissionStereotype>();

        public static Permission CreatePermissionForType(ContentTypeDefinition type)
            => new Permission(
                    String.Format(ManageOwnCustomUserSettings.Name, type.Name),
                    String.Format(ManageOwnCustomUserSettings.Description, type.DisplayName),
                    ManageOwnCustomUserSettings.ImpliedBy
                );
    }
}
