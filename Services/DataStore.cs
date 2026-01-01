using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using JellyfinInvoice.Models;
using JellyfinInvoice.Validation;
using Microsoft.Extensions.Logging;

namespace JellyfinInvoice.Services;

/// <summary>
/// Handles persistence of viewing records and invoices.
/// Uses file-based JSON storage with proper locking.
/// All data read from storage is re-validated before use.
/// </summary>
public sealed class DataStore : IDisposable
{
    private readonly string _dataDirectory;
    private readonly ILogger<DataStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    private const string ViewingRecordsFile = "viewing_records.json";
    private const string InvoicesFile = "invoices.json";

    /// <summary>
    /// Initializes a new instance of the <see cref="DataStore"/> class.
    /// </summary>
    /// <param name="dataDirectory">Directory for data storage (already validated).</param>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentException">Thrown if directory path is invalid.</exception>
    public DataStore(string dataDirectory, ILogger<DataStore> logger)
    {
        _dataDirectory = ValidateDirectoryPath(dataDirectory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        EnsureDirectoryExists();
    }

    /// <summary>
    /// Saves a viewing record to storage.
    /// </summary>
    /// <param name="record">The record to save (must already be sanitized).</param>
    /// <exception cref="ArgumentNullException">Thrown if record is null.</exception>
    public void SaveViewingRecord(ViewingRecord record)
    {
        if (record == null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        _lock.Wait();
        try
        {
            var records = LoadViewingRecordsInternal();
            records.Add(record);
            SaveViewingRecordsInternal(records);
            _logger.LogDebug("Saved viewing record {RecordId} for user {UserId}", record.Id, record.UserId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets all viewing records for a specific user within a date range.
    /// All returned data is re-validated.
    /// </summary>
    /// <param name="userId">The user ID to query.</param>
    /// <param name="startDate">Start of date range (UTC).</param>
    /// <param name="endDate">End of date range (UTC).</param>
    /// <returns>List of validated viewing records.</returns>
    public List<ViewingRecord> GetViewingRecords(Guid userId, DateTime startDate, DateTime endDate)
    {
        var validUserId = InputSanitizer.ValidateGuid(userId, nameof(userId));
        var validStart = InputSanitizer.ValidateDateTime(startDate, nameof(startDate));
        var validEnd = InputSanitizer.ValidateDateTime(endDate, nameof(endDate));

        _lock.Wait();
        try
        {
            var allRecords = LoadViewingRecordsInternal();
            var result = new List<ViewingRecord>();

            foreach (var record in allRecords)
            {
                if (record.UserId == validUserId &&
                    record.StartTime >= validStart &&
                    record.EndTime <= validEnd)
                {
                    // Re-validate data read from storage
                    var validatedRecord = ValidateRecordFromStorage(record);
                    if (validatedRecord != null)
                    {
                        result.Add(validatedRecord);
                    }
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Saves an invoice to storage.
    /// </summary>
    /// <param name="invoice">The invoice to save (must already be sanitized).</param>
    /// <exception cref="ArgumentNullException">Thrown if invoice is null.</exception>
    public void SaveInvoice(Invoice invoice)
    {
        if (invoice == null)
        {
            throw new ArgumentNullException(nameof(invoice));
        }

        _lock.Wait();
        try
        {
            var invoices = LoadInvoicesInternal();
            invoices.Add(invoice);
            SaveInvoicesInternal(invoices);
            _logger.LogInformation("Saved invoice {InvoiceId} for user {UserId}", invoice.Id, invoice.UserId);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets all invoices for a specific user.
    /// All returned data is re-validated.
    /// </summary>
    /// <param name="userId">The user ID to query.</param>
    /// <returns>List of validated invoices.</returns>
    public List<Invoice> GetInvoices(Guid userId)
    {
        var validUserId = InputSanitizer.ValidateGuid(userId, nameof(userId));

        _lock.Wait();
        try
        {
            var allInvoices = LoadInvoicesInternal();
            var result = new List<Invoice>();

            foreach (var invoice in allInvoices)
            {
                if (invoice.UserId == validUserId)
                {
                    var validatedInvoice = ValidateInvoiceFromStorage(invoice);
                    if (validatedInvoice != null)
                    {
                        result.Add(validatedInvoice);
                    }
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets a specific invoice by ID.
    /// </summary>
    /// <param name="invoiceId">The invoice ID.</param>
    /// <returns>The validated invoice, or null if not found.</returns>
    public Invoice? GetInvoice(Guid invoiceId)
    {
        var validId = InputSanitizer.ValidateGuid(invoiceId, nameof(invoiceId));

        _lock.Wait();
        try
        {
            var allInvoices = LoadInvoicesInternal();
            foreach (var invoice in allInvoices)
            {
                if (invoice.Id == validId)
                {
                    return ValidateInvoiceFromStorage(invoice);
                }
            }
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Deletes an invoice by ID.
    /// </summary>
    /// <param name="invoiceId">The invoice ID to delete.</param>
    /// <returns>True if the invoice was deleted, false if not found.</returns>
    public bool DeleteInvoice(Guid invoiceId)
    {
        var validId = InputSanitizer.ValidateGuid(invoiceId, nameof(invoiceId));

        _lock.Wait();
        try
        {
            var allInvoices = LoadInvoicesInternal();
            var originalCount = allInvoices.Count;
            allInvoices.RemoveAll(i => i.Id == validId);

            if (allInvoices.Count < originalCount)
            {
                SaveInvoicesInternal(allInvoices);
                _logger.LogInformation("Deleted invoice {InvoiceId}", validId);
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Validates a directory path is safe.
    /// </summary>
    private static string ValidateDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Data directory path cannot be empty.", nameof(path));
        }

        // Check for path traversal attempts
        var fullPath = Path.GetFullPath(path);
        if (fullPath.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid directory path.", nameof(path));
        }

        return fullPath;
    }

    /// <summary>
    /// Ensures the data directory exists.
    /// </summary>
    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_dataDirectory))
        {
            Directory.CreateDirectory(_dataDirectory);
            _logger.LogInformation("Created data directory: {Directory}", _dataDirectory);
        }
    }

    /// <summary>
    /// Loads viewing records from file.
    /// </summary>
    private List<ViewingRecord> LoadViewingRecordsInternal()
    {
        var filePath = Path.Combine(_dataDirectory, ViewingRecordsFile);
        return LoadFromFile<List<ViewingRecord>>(filePath) ?? new List<ViewingRecord>();
    }

    /// <summary>
    /// Saves viewing records to file.
    /// </summary>
    private void SaveViewingRecordsInternal(List<ViewingRecord> records)
    {
        var filePath = Path.Combine(_dataDirectory, ViewingRecordsFile);
        SaveToFile(filePath, records);
    }

    /// <summary>
    /// Loads invoices from file.
    /// </summary>
    private List<Invoice> LoadInvoicesInternal()
    {
        var filePath = Path.Combine(_dataDirectory, InvoicesFile);
        return LoadFromFile<List<Invoice>>(filePath) ?? new List<Invoice>();
    }

    /// <summary>
    /// Saves invoices to file.
    /// </summary>
    private void SaveInvoicesInternal(List<Invoice> invoices)
    {
        var filePath = Path.Combine(_dataDirectory, InvoicesFile);
        SaveToFile(filePath, invoices);
    }

    /// <summary>
    /// Loads data from a JSON file with error handling.
    /// </summary>
    private T? LoadFromFile<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON from {FilePath}", filePath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read file {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Saves data to a JSON file atomically.
    /// </summary>
    private void SaveToFile<T>(string filePath, T data)
    {
        var tempPath = filePath + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to save file {FilePath}", filePath);
            // Clean up temp file if it exists
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
    }

    /// <summary>
    /// Re-validates a viewing record loaded from storage.
    /// </summary>
    private ViewingRecord? ValidateRecordFromStorage(ViewingRecord record)
    {
        try
        {
            return new ViewingRecord(
                InputSanitizer.ValidateGuid(record.Id, "Id"),
                InputSanitizer.ValidateGuid(record.UserId, "UserId"),
                InputSanitizer.ValidateGuid(record.ItemId, "ItemId"),
                InputSanitizer.SanitizeString(record.ItemName, 200),
                record.ItemType,
                record.StartTime,
                record.EndTime,
                InputSanitizer.ValidateDurationTicks(record.DurationTicks, "DurationTicks")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid viewing record {RecordId} in storage, skipping", record.Id);
            return null;
        }
    }

    /// <summary>
    /// Re-validates an invoice loaded from storage.
    /// </summary>
    private Invoice? ValidateInvoiceFromStorage(Invoice invoice)
    {
        try
        {
            var validated = new Invoice(
                InputSanitizer.ValidateGuid(invoice.Id, "Id"),
                InputSanitizer.ValidateGuid(invoice.UserId, "UserId"),
                invoice.PeriodStart,
                invoice.PeriodEnd,
                InputSanitizer.ValidateCurrencyCode(invoice.CurrencyCode)
            );

            foreach (var item in invoice.LineItems)
            {
                var validItem = new InvoiceLineItem(
                    InputSanitizer.ValidateGuid(item.ViewingRecordId, "ViewingRecordId"),
                    InputSanitizer.SanitizeString(item.Description, 500),
                    InputSanitizer.ValidateDecimalRange(item.Quantity, 0, 1000, "Quantity"),
                    InputSanitizer.ValidateDecimalRange(item.UnitPrice, 0, 10000, "UnitPrice")
                );
                validated.AddLineItem(validItem);
            }

            return validated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid invoice {InvoiceId} in storage, skipping", invoice.Id);
            return null;
        }
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        _lock.Dispose();
    }
}
