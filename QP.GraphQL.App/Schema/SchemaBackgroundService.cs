using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace QP.GraphQL.App.Schema
{
    public class SchemaBackgroundService : BackgroundService
    {
        private readonly SchemaSettings _settings;
        private readonly ILogger<SchemaBackgroundService> _logger;
        private readonly ISchemaFactory _factory;

        public SchemaBackgroundService(ISchemaFactory factory, IOptions<SchemaSettings> options, ILogger<SchemaBackgroundService> logger)
        {
            _factory = factory;
            _settings = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_settings.SchemaAutoReload)
            {
                _logger.LogInformation($"Start schema auto reload service every {_settings.SchemaReloadInterval}");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_settings.SchemaReloadInterval, stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                    }

                    _logger.LogInformation("Run schema auto reload");
                    
                    try
                    {
                        _factory.ReloadSchema();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while auto reload schema");
                    }
                }
            }
        }
    }
}
