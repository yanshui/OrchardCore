using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OrchardCore.Autoroute.Models;
using OrchardCore.Autoroute.Services;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Data;
using YesSql.Indexes;

namespace OrchardCore.ContentManagement.Records
{
    public class AutoroutePartIndex : MapIndex
    {
        /// <summary>
        /// The container content item id.
        /// </summary>
        public string ContentItemId { get; set; }

        /// <summary>
        /// Route path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Whether this content item is published.
        /// </summary>
        public bool Published { get; set; }

        /// <summary>
        /// Whether this content item is latest.
        /// </summary>
        public bool Latest { get; set; }

        /// <summary>
        /// Only used if content item is contained in a container.
        /// </summary>
        public string ContainedContentItemId { get; set; }

        /// <summary>
        /// Only used if the content item is contained in a container.
        /// </summary>
        public string JsonPath { get; set; }
    }

    public class AutoroutePartIndexProvider : ContentHandlerBase, IIndexProvider, IScopedIndexProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HashSet<ContentItem> _removed = new HashSet<ContentItem>();
        private IContentManager _contentManager;
        private IContentDefinitionManager _contentDefinitionManager;

        public AutoroutePartIndexProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public override Task RemovedAsync(RemoveContentContext context)
        {
            if (context.NoActiveVersionLeft)
            {
                var part = context.ContentItem.As<AutoroutePart>();

                if (part != null)
                {
                    _removed.Add(context.ContentItem);
                }
            }

            return Task.CompletedTask;
        }

        public string CollectionName { get; set; }
        public Type ForType() => typeof(ContentItem);
        public void Describe(IDescriptor context) => Describe((DescribeContext<ContentItem>)context);

        public void Describe(DescribeContext<ContentItem> context)
        {
            context.For<AutoroutePartIndex>()
                .Map(async contentItem =>
                {
                    var part = contentItem.As<AutoroutePart>();

                    if (part == null)
                    {
                        return null;
                    }

                    // If the related content item was removed, a record is still added.
                    if (!contentItem.Published && !contentItem.Latest && !_removed.Contains(contentItem))
                    {
                        return null;
                    }

                    if (String.IsNullOrEmpty(part.Path))
                    {
                        return null;
                    }

                    // Lazy initialization because of ISession cyclic dependency
                    _contentDefinitionManager = _contentDefinitionManager ?? _serviceProvider.GetRequiredService<IContentDefinitionManager>();

                    // Search for AutoroutePart
                    var contentTypeDefinition = _contentDefinitionManager.GetTypeDefinition(contentItem.ContentType);

                    // Validate that the content definition contains an AutoroutePart.
                    // This prevents indexing parts that have been removed from the type definition, but are still present in the elements.
                    var removedFromTypeDefinition = false;
                    if (contentTypeDefinition == null || !contentTypeDefinition.Parts.Any(ctpd => ctpd.Name == nameof(AutoroutePart)))
                    {
                        // When the part has been removed enlist an update for after the session has been commited.
                        removedFromTypeDefinition = true;
                        contentItem.Remove<AutoroutePart>();
                        var autorouteEntries = _serviceProvider.GetRequiredService<IAutorouteEntries>();
                        await autorouteEntries.UpdateEntriesAsync();
                    }

                    var results = new List<AutoroutePartIndex>
                    {
                        // If the part is disabled, or removed, a record is still added but with a null path.
                        new AutoroutePartIndex
                        {
                            ContentItemId = contentItem.ContentItemId,
                            Path = !part.Disabled && !removedFromTypeDefinition ? part.Path : null,
                            Published = contentItem.Published,
                            Latest = contentItem.Latest
                        }
                    };

                    if (!part.RouteContainedItems || part.Disabled || _removed.Contains(contentItem) || removedFromTypeDefinition)
                    {
                        return results;
                    }

                    _contentManager ??= _serviceProvider.GetRequiredService<IContentManager>();

                    var containedContentItemsAspect = await _contentManager.PopulateAspectAsync<ContainedContentItemsAspect>(contentItem);

                    await PopulateContainedContentItemIndexesAsync(results, contentItem, containedContentItemsAspect, contentItem.Content, part.Path);

                    return results;
                });
        }

        private async Task PopulateContainedContentItemIndexesAsync(List<AutoroutePartIndex> results, ContentItem containerContentItem, ContainedContentItemsAspect containedContentItemsAspect, JObject content, string basePath)
        {
            foreach (var accessor in containedContentItemsAspect.Accessors)
            {
                var items = accessor.Invoke(content);

                foreach (JObject jItem in items)
                {
                    var contentItem = jItem.ToObject<ContentItem>();
                    var handlerAspect = await _contentManager.PopulateAspectAsync<RouteHandlerAspect>(contentItem);

                    if (!handlerAspect.Disabled)
                    {
                        var path = handlerAspect.Path;
                        if (!handlerAspect.Absolute)
                        {
                            path = (basePath.EndsWith('/') ? basePath : basePath + '/') + handlerAspect.Path;
                        }

                        results.Add(new AutoroutePartIndex
                        {
                            ContentItemId = containerContentItem.ContentItemId,
                            Path = path,
                            Published = containerContentItem.Published,
                            Latest = containerContentItem.Latest,
                            ContainedContentItemId = contentItem.ContentItemId,
                            JsonPath = jItem.Path
                        });
                    }

                    var itemBasePath = (basePath.EndsWith('/') ? basePath : basePath + '/') + handlerAspect.Path;
                    var childrenAspect = await _contentManager.PopulateAspectAsync<ContainedContentItemsAspect>(contentItem);

                    await PopulateContainedContentItemIndexesAsync(results, containerContentItem, childrenAspect, jItem, itemBasePath);
                }
            }
        }
    }
}
