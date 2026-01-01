using System;
using System.Collections.Generic;
using JellyfinInvoice.Configuration;
using JellyfinInvoice.Models;
using JellyfinInvoice.Validation;
using Microsoft.Extensions.Logging;

namespace JellyfinInvoice.Services;

/// <summary>
/// Generates invoices from viewing records.
/// Calculates charges based on configured rates.
/// </summary>
public sealed class InvoiceGenerator
{
    private readonly DataStore _dataStore;
    private readonly ILogger<InvoiceGenerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceGenerator"/> class.
    /// </summary>
    /// <param name="dataStore">Data storage service.</param>
    /// <param name="logger">Logger instance.</param>
    public InvoiceGenerator(DataStore dataStore, ILogger<InvoiceGenerator> logger)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates an invoice for a user for a specific period.
    /// </summary>
    /// <param name="userId">The user ID to generate invoice for.</param>
    /// <param name="periodStart">Start of billing period (UTC).</param>
    /// <param name="periodEnd">End of billing period (UTC).</param>
    /// <returns>The generated invoice, or null if no viewing records exist.</returns>
    public Invoice? GenerateInvoice(Guid userId, DateTime periodStart, DateTime periodEnd)
    {
        var validUserId = InputSanitizer.ValidateGuid(userId, nameof(userId));
        var validStart = ValidatePeriodStart(periodStart);
        var validEnd = ValidatePeriodEnd(periodEnd, validStart);

        var config = GetConfiguration();
        var records = GetViewingRecords(validUserId, validStart, validEnd);

        if (records.Count == 0)
        {
            _logger.LogInformation(
                "No viewing records for user {UserId} in period {Start} to {End}",
                validUserId, validStart, validEnd);
            return null;
        }

        var invoice = CreateInvoice(validUserId, validStart, validEnd, config);
        AddLineItems(invoice, records, config);

        SaveInvoice(invoice);

        return invoice;
    }

    /// <summary>
    /// Generates an invoice for the current billing period.
    /// </summary>
    /// <param name="userId">The user ID to generate invoice for.</param>
    /// <returns>The generated invoice, or null if no viewing records exist.</returns>
    public Invoice? GenerateCurrentPeriodInvoice(Guid userId)
    {
        var config = GetConfiguration();
        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddDays(-config.InvoicePeriodDays);

        return GenerateInvoice(userId, periodStart, periodEnd);
    }

    /// <summary>
    /// Gets all invoices for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>List of invoices.</returns>
    public List<Invoice> GetUserInvoices(Guid userId)
    {
        var validUserId = InputSanitizer.ValidateGuid(userId, nameof(userId));
        return _dataStore.GetInvoices(validUserId);
    }

    /// <summary>
    /// Gets a specific invoice by ID.
    /// </summary>
    /// <param name="invoiceId">The invoice ID.</param>
    /// <returns>The invoice, or null if not found.</returns>
    public Invoice? GetInvoice(Guid invoiceId)
    {
        var validId = InputSanitizer.ValidateGuid(invoiceId, nameof(invoiceId));
        return _dataStore.GetInvoice(validId);
    }

    /// <summary>
    /// Validates period start date.
    /// </summary>
    private static DateTime ValidatePeriodStart(DateTime periodStart)
    {
        if (periodStart == default)
        {
            throw new ArgumentException("Period start cannot be default.", nameof(periodStart));
        }

        return periodStart.Kind == DateTimeKind.Utc
            ? periodStart
            : DateTime.SpecifyKind(periodStart, DateTimeKind.Utc);
    }

    /// <summary>
    /// Validates period end date.
    /// </summary>
    private static DateTime ValidatePeriodEnd(DateTime periodEnd, DateTime periodStart)
    {
        if (periodEnd == default)
        {
            throw new ArgumentException("Period end cannot be default.", nameof(periodEnd));
        }

        var validEnd = periodEnd.Kind == DateTimeKind.Utc
            ? periodEnd
            : DateTime.SpecifyKind(periodEnd, DateTimeKind.Utc);

        if (validEnd <= periodStart)
        {
            throw new ArgumentException("Period end must be after period start.", nameof(periodEnd));
        }

        return validEnd;
    }

    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    private static PluginConfiguration GetConfiguration()
    {
        return Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    /// <summary>
    /// Gets viewing records for the period.
    /// </summary>
    private List<ViewingRecord> GetViewingRecords(Guid userId, DateTime start, DateTime end)
    {
        return _dataStore.GetViewingRecords(userId, start, end);
    }

    /// <summary>
    /// Creates a new invoice.
    /// </summary>
    private Invoice CreateInvoice(
        Guid userId,
        DateTime periodStart,
        DateTime periodEnd,
        PluginConfiguration config)
    {
        var currencyCode = InputSanitizer.ValidateCurrencyCode(config.CurrencyCode);

        return new Invoice(
            id: Guid.NewGuid(),
            userId: userId,
            periodStart: periodStart,
            periodEnd: periodEnd,
            currencyCode: currencyCode
        );
    }

    /// <summary>
    /// Adds line items to invoice from viewing records.
    /// </summary>
    private void AddLineItems(Invoice invoice, List<ViewingRecord> records, PluginConfiguration config)
    {
        var rate = GetHourlyRate(config);

        foreach (var record in records)
        {
            var lineItem = CreateLineItem(record, rate, config);
            invoice.AddLineItem(lineItem);
        }

        _logger.LogDebug(
            "Added {Count} line items to invoice {InvoiceId}",
            records.Count, invoice.Id);
    }

    /// <summary>
    /// Gets the validated hourly rate.
    /// </summary>
    private static decimal GetHourlyRate(PluginConfiguration config)
    {
        return InputSanitizer.ValidateDecimalRange(
            config.DefaultRatePerHour,
            min: 0m,
            max: 10000m,
            paramName: "DefaultRatePerHour");
    }

    /// <summary>
    /// Creates a line item from a viewing record.
    /// </summary>
    private static InvoiceLineItem CreateLineItem(
        ViewingRecord record,
        decimal hourlyRate,
        PluginConfiguration config)
    {
        var description = FormatDescription(record, config);
        var hours = CalculateHours(record);

        return new InvoiceLineItem(
            viewingRecordId: record.Id,
            description: description,
            quantity: hours,
            unitPrice: hourlyRate
        );
    }

    /// <summary>
    /// Formats the line item description.
    /// </summary>
    private static string FormatDescription(ViewingRecord record, PluginConfiguration config)
    {
        var itemName = InputSanitizer.SanitizeString(record.ItemName, config.MaxTitleLength);
        var dateStr = record.StartTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var description = $"{itemName} - {dateStr}";
        return InputSanitizer.SanitizeString(description, config.MaxDescriptionLength);
    }

    /// <summary>
    /// Calculates billable hours from a viewing record.
    /// </summary>
    private static decimal CalculateHours(ViewingRecord record)
    {
        var hours = record.DurationHours;

        // Round to 2 decimal places
        hours = Math.Round(hours, 2, MidpointRounding.AwayFromZero);

        // Ensure non-negative
        return InputSanitizer.ValidateDecimalRange(hours, 0m, 24m, "Hours");
    }

    /// <summary>
    /// Saves the invoice to storage.
    /// </summary>
    private void SaveInvoice(Invoice invoice)
    {
        _dataStore.SaveInvoice(invoice);
        _logger.LogInformation(
            "Generated invoice {InvoiceId} for user {UserId}: {ItemCount} items, total {Total} {Currency}",
            invoice.Id,
            invoice.UserId,
            invoice.LineItems.Count,
            invoice.TotalAmount,
            invoice.CurrencyCode);
    }
}
