using System;
using System.Globalization;
using Jellyfin.Plugin.JellyfinInvoice.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace JellyfinInvoice;

/// <summary>
/// Main plugin entry point for JellyfinInvoice.
/// Registers the plugin with Jellyfin and provides access to configuration.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Unique identifier for this plugin.
    /// </summary>
    private static readonly Guid PluginGuid = new("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths provided by Jellyfin.</param>
    /// <param name="xmlSerializer">XML serializer for configuration.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the singleton instance of the plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the name of the plugin displayed in Jellyfin.
    /// </summary>
    public override string Name => "Invoice Generator";

    /// <summary>
    /// Gets the unique identifier for this plugin.
    /// </summary>
    public override Guid Id => PluginGuid;

    /// <summary>
    /// Gets the description of the plugin.
    /// </summary>
    public override string Description => "Tracks user viewing activity and generates invoices.";

    /// <summary>
    /// Gets the web pages (configuration UI) for this plugin.
    /// </summary>
    /// <returns>Collection of plugin page info for the web interface.</returns>
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "Invoices",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.invoicesPage.html",
                MenuSection = "admin",
                MenuIcon = "receipt"
            }
        };
    }
}
