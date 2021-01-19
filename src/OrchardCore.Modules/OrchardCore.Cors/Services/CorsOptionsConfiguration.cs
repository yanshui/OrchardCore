using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace OrchardCore.Cors.Services
{
    public class CorsOptionsConfiguration : IConfigureOptions<CorsOptions>
    {
        private readonly CorsService _corsService;
        private readonly ILogger<CorsOptionsConfiguration> _logger;

        public CorsOptionsConfiguration(CorsService corsService, ILogger<CorsOptionsConfiguration> logger)
        {
            _corsService = corsService;
            _logger = logger;
        }

        public void Configure(CorsOptions options)
        {
            var corsSettings = _corsService.GetSettingsAsync().GetAwaiter().GetResult();
            if (corsSettings?.Policies == null || !corsSettings.Policies.Any())
            {
                return;
            }

            foreach (var corsPolicy in corsSettings.Policies)
            {
                if (corsPolicy.AllowCredentials && corsPolicy.AllowAnyOrigin)
                {
                    _logger.LogWarning("Using AllowCredentials and AllowAnyOrigin at the same time is considered a security risk, the {PolicyName} policy will not be loaded.", corsPolicy.Name);
                    continue;
                }

                options.AddPolicy(corsPolicy.Name, configurePolicy =>
                {
                    if (corsPolicy.AllowAnyHeader)
                    {
                        configurePolicy.AllowAnyHeader();
                    }
                    else
                    {
                        configurePolicy.WithHeaders(corsPolicy.AllowedOrigins);
                    }

                    if (corsPolicy.AllowAnyMethod)
                    {
                        configurePolicy.AllowAnyMethod();
                    }
                    else
                    {
                        configurePolicy.WithMethods(corsPolicy.AllowedMethods);
                    }

                    if (corsPolicy.AllowAnyOrigin)
                    {
                        configurePolicy.AllowAnyOrigin();
                    }
                    else
                    {
                        configurePolicy.WithOrigins(corsPolicy.AllowedOrigins);
                    }

                    if (corsPolicy.AllowCredentials)
                    {
                        configurePolicy.AllowCredentials();
                    }
                    else
                    {
                        configurePolicy.DisallowCredentials();
                    }
                });

                if(corsPolicy.IsDefaultPolicy)
                {
                    options.DefaultPolicyName = corsPolicy.Name;
                }
            }

            if (options.DefaultPolicyName == null)
            {
                options.DefaultPolicyName = corsSettings.Policies.FirstOrDefault()?.Name;
            }
        }
    }
}
