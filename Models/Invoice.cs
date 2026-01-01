using System;
using System.Collections.Generic;

namespace JellyfinInvoice.Models;

/// <summary>
/// Represents an invoice for a billing period.
/// Contains line items derived from viewing records.
/// </summary>
public sealed class Invoice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Invoice"/> class.
    /// </summary>
    /// <param name="id">Unique invoice identifier.</param>
    /// <param name="userId">The user this invoice is for.</param>
    /// <param name="periodStart">Start of billing period (UTC).</param>
    /// <param name="periodEnd">End of billing period (UTC).</param>
    /// <param name="currencyCode">ISO 4217 currency code.</param>
    public Invoice(
        Guid id,
        Guid userId,
        DateTime periodStart,
        DateTime periodEnd,
        string currencyCode)
    {
        Id = id;
        UserId = userId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        CurrencyCode = currencyCode;
        CreatedAt = DateTime.UtcNow;
        LineItems = new List<InvoiceLineItem>();
    }

    /// <summary>
    /// Gets the unique invoice identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the user ID this invoice belongs to.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the start of the billing period (UTC).
    /// </summary>
    public DateTime PeriodStart { get; }

    /// <summary>
    /// Gets the end of the billing period (UTC).
    /// </summary>
    public DateTime PeriodEnd { get; }

    /// <summary>
    /// Gets the ISO 4217 currency code.
    /// </summary>
    public string CurrencyCode { get; }

    /// <summary>
    /// Gets when this invoice was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the line items on this invoice.
    /// </summary>
    public List<InvoiceLineItem> LineItems { get; }

    /// <summary>
    /// Gets the total amount for this invoice.
    /// </summary>
    public decimal TotalAmount => CalculateTotal();

    /// <summary>
    /// Calculates the total from all line items.
    /// </summary>
    /// <returns>Sum of all line item amounts.</returns>
    private decimal CalculateTotal()
    {
        decimal total = 0m;
        foreach (var item in LineItems)
        {
            total += item.Amount;
        }
        return total;
    }

    /// <summary>
    /// Adds a line item to the invoice.
    /// </summary>
    /// <param name="lineItem">The line item to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if lineItem is null.</exception>
    public void AddLineItem(InvoiceLineItem lineItem)
    {
        if (lineItem == null)
        {
            throw new ArgumentNullException(nameof(lineItem));
        }
        LineItems.Add(lineItem);
    }
}

/// <summary>
/// Represents a single line item on an invoice.
/// </summary>
public sealed class InvoiceLineItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceLineItem"/> class.
    /// </summary>
    /// <param name="viewingRecordId">Reference to the source viewing record.</param>
    /// <param name="description">Sanitized description of the charge.</param>
    /// <param name="quantity">Hours watched.</param>
    /// <param name="unitPrice">Rate per hour.</param>
    public InvoiceLineItem(
        Guid viewingRecordId,
        string description,
        decimal quantity,
        decimal unitPrice)
    {
        ViewingRecordId = viewingRecordId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    /// <summary>
    /// Gets the ID of the viewing record this line item is based on.
    /// </summary>
    public Guid ViewingRecordId { get; }

    /// <summary>
    /// Gets the sanitized description of the charge.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the quantity (hours watched).
    /// </summary>
    public decimal Quantity { get; }

    /// <summary>
    /// Gets the unit price (rate per hour).
    /// </summary>
    public decimal UnitPrice { get; }

    /// <summary>
    /// Gets the total amount for this line item (Quantity * UnitPrice).
    /// </summary>
    public decimal Amount => Quantity * UnitPrice;
}
