using System;
using JellyfinInvoice.Models;
using Xunit;

namespace JellyfinInvoice.Tests;

/// <summary>
/// Unit tests for data models.
/// </summary>
public class ModelsTests
{
    #region ViewingRecord Tests

    [Fact]
    public void ViewingRecord_Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var itemName = "Test Movie";
        var startTime = DateTime.UtcNow.AddHours(-2);
        var endTime = DateTime.UtcNow;
        var durationTicks = TimeSpan.FromHours(2).Ticks;

        var record = new ViewingRecord(
            id, userId, itemId, itemName,
            startTime, endTime, durationTicks);

        Assert.Equal(id, record.Id);
        Assert.Equal(userId, record.UserId);
        Assert.Equal(itemId, record.ItemId);
        Assert.Equal(itemName, record.ItemName);
        Assert.Equal(startTime, record.StartTime);
        Assert.Equal(endTime, record.EndTime);
        Assert.Equal(durationTicks, record.DurationTicks);
    }

    [Fact]
    public void ViewingRecord_Duration_CalculatesCorrectly()
    {
        var durationTicks = TimeSpan.FromMinutes(90).Ticks;
        var record = new ViewingRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test",
            DateTime.UtcNow.AddMinutes(-90), DateTime.UtcNow, durationTicks);

        Assert.Equal(TimeSpan.FromMinutes(90), record.Duration);
    }

    [Fact]
    public void ViewingRecord_DurationHours_CalculatesCorrectly()
    {
        var durationTicks = TimeSpan.FromHours(1.5).Ticks;
        var record = new ViewingRecord(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test",
            DateTime.UtcNow.AddHours(-1.5), DateTime.UtcNow, durationTicks);

        Assert.Equal(1.5m, record.DurationHours);
    }

    #endregion

    #region Invoice Tests

    [Fact]
    public void Invoice_Constructor_SetsAllProperties()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow.AddDays(-30);
        var periodEnd = DateTime.UtcNow;
        var currencyCode = "USD";

        var invoice = new Invoice(id, userId, periodStart, periodEnd, currencyCode);

        Assert.Equal(id, invoice.Id);
        Assert.Equal(userId, invoice.UserId);
        Assert.Equal(periodStart, invoice.PeriodStart);
        Assert.Equal(periodEnd, invoice.PeriodEnd);
        Assert.Equal(currencyCode, invoice.CurrencyCode);
        Assert.Empty(invoice.LineItems);
    }

    [Fact]
    public void Invoice_AddLineItem_AddsToCollection()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "USD");

        var lineItem = new InvoiceLineItem(
            Guid.NewGuid(), "Test Item", 1.5m, 10m);

        invoice.AddLineItem(lineItem);

        Assert.Single(invoice.LineItems);
        Assert.Contains(lineItem, invoice.LineItems);
    }

    [Fact]
    public void Invoice_AddLineItem_NullThrowsException()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "USD");

        Assert.Throws<ArgumentNullException>(() =>
            invoice.AddLineItem(null!));
    }

    [Fact]
    public void Invoice_TotalAmount_CalculatesCorrectly()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "USD");

        invoice.AddLineItem(new InvoiceLineItem(Guid.NewGuid(), "Item 1", 1m, 10m));
        invoice.AddLineItem(new InvoiceLineItem(Guid.NewGuid(), "Item 2", 2m, 5m));
        invoice.AddLineItem(new InvoiceLineItem(Guid.NewGuid(), "Item 3", 0.5m, 20m));

        // 1*10 + 2*5 + 0.5*20 = 10 + 10 + 10 = 30
        Assert.Equal(30m, invoice.TotalAmount);
    }

    [Fact]
    public void Invoice_TotalAmount_EmptyReturnsZero()
    {
        var invoice = new Invoice(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, "USD");

        Assert.Equal(0m, invoice.TotalAmount);
    }

    #endregion

    #region InvoiceLineItem Tests

    [Fact]
    public void InvoiceLineItem_Constructor_SetsAllProperties()
    {
        var recordId = Guid.NewGuid();
        var description = "Test Description";
        var quantity = 1.5m;
        var unitPrice = 10m;

        var item = new InvoiceLineItem(recordId, description, quantity, unitPrice);

        Assert.Equal(recordId, item.ViewingRecordId);
        Assert.Equal(description, item.Description);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal(unitPrice, item.UnitPrice);
    }

    [Fact]
    public void InvoiceLineItem_Amount_CalculatesCorrectly()
    {
        var item = new InvoiceLineItem(Guid.NewGuid(), "Test", 2.5m, 4m);
        Assert.Equal(10m, item.Amount);
    }

    [Fact]
    public void InvoiceLineItem_Amount_ZeroQuantity_ReturnsZero()
    {
        var item = new InvoiceLineItem(Guid.NewGuid(), "Test", 0m, 10m);
        Assert.Equal(0m, item.Amount);
    }

    [Fact]
    public void InvoiceLineItem_Amount_ZeroPrice_ReturnsZero()
    {
        var item = new InvoiceLineItem(Guid.NewGuid(), "Test", 5m, 0m);
        Assert.Equal(0m, item.Amount);
    }

    #endregion
}
