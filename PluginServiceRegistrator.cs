using JellyfinInvoice.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace JellyfinInvoice;

/// <summary>
/// Registers plugin services with the Jellyfin dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers services for the Invoice Generator plugin.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="serverApplicationHost">The server application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost serverApplicationHost)
    {
        // Register DataStore as singleton (shared state for all services)
        serviceCollection.AddSingleton<DataStore>(sp =>
        {
            var appPaths = sp.GetRequiredService<IApplicationPaths>();
            var dataDir = Path.Combine(appPaths.PluginConfigurationsPath, "JellyfinInvoice");
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DataStore>>();
            return new DataStore(dataDir, logger);
        });

        // Register InvoiceGenerator
        serviceCollection.AddSingleton<InvoiceGenerator>();

        // Register ViewingTracker as hosted service (starts automatically)
        serviceCollection.AddHostedService<ViewingTracker>();
    }
}
