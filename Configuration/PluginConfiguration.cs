using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyfinInvoice.Configuration;

/// <summary>
/// Plugin configuration model.
/// Contains all user-configurable settings for the invoice plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// Sets secure default values.
    /// </summary>
    public PluginConfiguration()
    {
        // Secure defaults
        EnableTracking = false;
        CurrencyCode = "USD";
        DefaultRatePerHour = 0.00m;
        InvoicePeriodDays = 30;
        MaxTitleLength = 200;
        MaxDescriptionLength = 500;
    }

    /// <summary>
    /// Gets or sets a value indicating whether viewing tracking is enabled.
    /// Disabled by default - must be explicitly enabled.
    /// </summary>
    public bool EnableTracking { get; set; }

    /// <summary>
    /// Gets or sets the ISO 4217 currency code for invoices.
    /// </summary>
    public string CurrencyCode { get; set; }

    /// <summary>
    /// Gets or sets the default hourly rate for viewing charges.
    /// </summary>
    public decimal DefaultRatePerHour { get; set; }

    /// <summary>
    /// Gets or sets the invoice billing period in days.
    /// </summary>
    public int InvoicePeriodDays { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed length for media titles.
    /// Used by InputSanitizer for validation.
    /// </summary>
    public int MaxTitleLength { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed length for descriptions.
    /// Used by InputSanitizer for validation.
    /// </summary>
    public int MaxDescriptionLength { get; set; }
}
