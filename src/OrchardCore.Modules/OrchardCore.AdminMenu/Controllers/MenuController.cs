using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Admin;
using OrchardCore.AdminMenu.ViewModels;
using OrchardCore.AdminMenu.Services;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;

namespace OrchardCore.AdminMenu.Controllers
{
    [Admin]
    public class MenuController : Controller
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IAdminMenuService _adminMenuService;
        private readonly ISiteService _siteService;
        private readonly INotifier _notifier;
        private readonly IStringLocalizer S;
        private readonly IHtmlLocalizer H;
        private readonly dynamic New;
        private readonly ILogger _logger;

        public MenuController(
            IAuthorizationService authorizationService,
            IAdminMenuService adminMenuService,
            ISiteService siteService,
            IShapeFactory shapeFactory,
            INotifier notifier,
            IStringLocalizer<MenuController> stringLocalizer,
            IHtmlLocalizer<MenuController> htmlLocalizer,
            ILogger<MenuController> logger)
        {
            _authorizationService = authorizationService;
            _adminMenuService = adminMenuService;
            _siteService = siteService;
            New = shapeFactory;
            _notifier = notifier;
            S = stringLocalizer;
            H = htmlLocalizer;
            _logger = logger;
        }

        public async Task<IActionResult> List(ContentOptions options, PagerParameters pagerParameters)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            var siteSettings = await _siteService.GetSiteSettingsAsync();
            var pager = new Pager(pagerParameters, siteSettings.PageSize);

            var adminMenuList = (await _adminMenuService.GetAdminMenuListAsync()).AdminMenu;

            if (!string.IsNullOrWhiteSpace(options.Search))
            {
                adminMenuList = adminMenuList.Where(x => x.Name.Contains(options.Search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var count = adminMenuList.Count();

            var startIndex = pager.GetStartIndex();
            var pageSize = pager.PageSize;
            IEnumerable<Models.AdminMenu> results = new List<Models.AdminMenu>();

            //todo: handle the case where there is a deserialization exception on some of the presets.
            // load at least the ones without error. Provide a way to delete the ones on error.
            try
            {
                results = adminMenuList
                .Skip(startIndex)
                .Take(pageSize)
                .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when retrieving the list of admin menus.");
                _notifier.Error(H["Error when retrieving the list of admin menus."]);
            }

            // Maintain previous route data when generating page links
            var routeData = new RouteData();
            routeData.Values.Add("Options.Search", options.Search);

            var pagerShape = (await New.Pager(pager)).TotalItemCount(count).RouteData(routeData);

            var model = new AdminMenuListViewModel
            {
                AdminMenu = results.Select(x => new AdminMenuEntry { AdminMenu = x }).ToList(),
                Options = options,
                Pager = pagerShape
            };

            model.Options.ContentsBulkAction = new List<SelectListItem>() {
                new SelectListItem() { Text = S["Delete"], Value = nameof(ContentsBulkAction.Remove) }
            };

            return View(model);
        }

        [HttpPost, ActionName("List")]
        [FormValueRequired("submit.Filter")]
        public ActionResult IndexFilterPOST(AdminMenuListViewModel model)
        {
            return RedirectToAction("List", new RouteValueDictionary {
                { "Options.Search", model.Options.Search }
            });
        }

        public async Task<IActionResult> Create()
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            var model = new AdminMenuCreateViewModel();

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(AdminMenuCreateViewModel model)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                var tree = new Models.AdminMenu { Name = model.Name };

                await _adminMenuService.SaveAsync(tree);

                return RedirectToAction(nameof(List));
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            var adminMenuList = await _adminMenuService.GetAdminMenuListAsync();
            var adminMenu = _adminMenuService.GetAdminMenuById(adminMenuList, id);

            if (adminMenu == null)
            {
                return NotFound();
            }

            var model = new AdminMenuEditViewModel
            {
                Id = adminMenu.Id,
                Name = adminMenu.Name
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(AdminMenuEditViewModel model)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            var adminMenuList = await _adminMenuService.LoadAdminMenuListAsync();
            var adminMenu = _adminMenuService.GetAdminMenuById(adminMenuList, model.Id);

            if (adminMenu == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                adminMenu.Name = model.Name;

                await _adminMenuService.SaveAsync(adminMenu);

                _notifier.Success(H["Admin menu updated successfully."]);

                return RedirectToAction(nameof(List));
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            var adminMenuList = await _adminMenuService.LoadAdminMenuListAsync();
            var adminMenu = _adminMenuService.GetAdminMenuById(adminMenuList, id);

            if (adminMenu == null)
            {
                _notifier.Error(H["Can't find the admin menu."]);
                return RedirectToAction(nameof(List));
            }

            var removed = await _adminMenuService.DeleteAsync(adminMenu);

            if (removed == 1)
            {
                _notifier.Success(H["Admin menu deleted successfully."]);
            }
            else
            {
                _notifier.Error(H["Can't delete the admin menu."]);
            }

            return RedirectToAction(nameof(List));
        }

        [HttpPost, ActionName("List")]
        [FormValueRequired("submit.BulkAction")]
        public async Task<ActionResult> IndexPost(ViewModels.ContentOptions options, IEnumerable<string> itemIds)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            if (itemIds?.Count() > 0)
            {
                var adminMenuList = (await _adminMenuService.GetAdminMenuListAsync()).AdminMenu;
                var checkedContentItems = adminMenuList.Where(x => itemIds.Contains(x.Id));
                switch (options.BulkAction)
                {
                    case ContentsBulkAction.None:
                        break;
                    case ContentsBulkAction.Remove:
                        foreach (var item in checkedContentItems)
                        {
                            var adminMenu = adminMenuList.FirstOrDefault(x => String.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                            await _adminMenuService.DeleteAsync(adminMenu);
                        }
                        _notifier.Success(H["Admin menus successfully removed."]);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return RedirectToAction("List");
        }

        [HttpPost]
        public async Task<IActionResult> Toggle(string id)
        {
            if (!await _authorizationService.AuthorizeAsync(User, Permissions.ManageAdminMenu))
            {
                return Forbid();
            }

            var adminMenuList = await _adminMenuService.LoadAdminMenuListAsync();
            var adminMenu = _adminMenuService.GetAdminMenuById(adminMenuList, id);

            if (adminMenu == null)
            {
                return NotFound();
            }

            adminMenu.Enabled = !adminMenu.Enabled;

            await _adminMenuService.SaveAsync(adminMenu);

            _notifier.Success(H["Admin menu toggled successfully."]);

            return RedirectToAction(nameof(List));
        }
    }
}
