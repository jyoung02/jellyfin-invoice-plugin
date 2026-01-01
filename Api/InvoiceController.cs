using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using JellyfinInvoice.Models;
using JellyfinInvoice.Services;
using JellyfinInvoice.Validation;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JellyfinInvoice.Api;

/// <summary>
/// API controller for invoice operations.
/// All endpoints require authentication and validate all inputs.
/// </summary>
[ApiController]
[Route("Invoice")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public class InvoiceController : ControllerBase
{
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly ILogger<InvoiceController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceController"/> class.
    /// </summary>
    /// <param name="invoiceGenerator">Invoice generation service.</param>
    /// <param name="logger">Logger instance.</param>
    public InvoiceController(
        InvoiceGenerator invoiceGenerator,
        ILogger<InvoiceController> logger)
    {
        _invoiceGenerator = invoiceGenerator ?? throw new ArgumentNullException(nameof(invoiceGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all invoices for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>List of invoices.</returns>
    [HttpGet("User/{userId}")]
    [ProducesResponseType(typeof(List<Invoice>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<List<Invoice>> GetUserInvoices([FromRoute] string userId)
    {
        var validatedUserId = ValidateUserIdParameter(userId);
        if (validatedUserId == null)
        {
            return BadRequest("Invalid user ID format.");
        }

        LogRequest("GetUserInvoices", validatedUserId.Value);

        var invoices = _invoiceGenerator.GetUserInvoices(validatedUserId.Value);
        return Ok(invoices);
    }

    /// <summary>
    /// Gets a specific invoice by ID.
    /// </summary>
    /// <param name="invoiceId">The invoice ID.</param>
    /// <returns>The invoice.</returns>
    [HttpGet("{invoiceId}")]
    [ProducesResponseType(typeof(Invoice), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Invoice> GetInvoice([FromRoute] string invoiceId)
    {
        var validatedId = ValidateInvoiceIdParameter(invoiceId);
        if (validatedId == null)
        {
            return BadRequest("Invalid invoice ID format.");
        }

        LogRequest("GetInvoice", validatedId.Value);

        var invoice = _invoiceGenerator.GetInvoice(validatedId.Value);
        if (invoice == null)
        {
            return NotFound();
        }

        return Ok(invoice);
    }

    /// <summary>
    /// Generates an invoice for the current billing period.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The generated invoice.</returns>
    [HttpPost("Generate/{userId}")]
    [ProducesResponseType(typeof(Invoice), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<Invoice> GenerateInvoice([FromRoute] string userId)
    {
        var validatedUserId = ValidateUserIdParameter(userId);
        if (validatedUserId == null)
        {
            return BadRequest("Invalid user ID format.");
        }

        LogRequest("GenerateInvoice", validatedUserId.Value);

        var invoice = _invoiceGenerator.GenerateCurrentPeriodInvoice(validatedUserId.Value);
        if (invoice == null)
        {
            return NoContent();
        }

        return Ok(invoice);
    }

    /// <summary>
    /// Generates an invoice for a custom date range.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="request">The date range request.</param>
    /// <returns>The generated invoice.</returns>
    [HttpPost("Generate/{userId}/Range")]
    [ProducesResponseType(typeof(Invoice), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<Invoice> GenerateInvoiceForRange(
        [FromRoute] string userId,
        [FromBody] DateRangeRequest request)
    {
        var validatedUserId = ValidateUserIdParameter(userId);
        if (validatedUserId == null)
        {
            return BadRequest("Invalid user ID format.");
        }

        var validatedRange = ValidateDateRange(request);
        if (validatedRange == null)
        {
            return BadRequest("Invalid date range.");
        }

        LogRequest("GenerateInvoiceForRange", validatedUserId.Value);

        var invoice = _invoiceGenerator.GenerateInvoice(
            validatedUserId.Value,
            validatedRange.Value.Start,
            validatedRange.Value.End);

        if (invoice == null)
        {
            return NoContent();
        }

        return Ok(invoice);
    }

    /// <summary>
    /// Validates user ID from route parameter.
    /// </summary>
    private Guid? ValidateUserIdParameter(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Empty user ID received");
            return null;
        }

        try
        {
            var sanitized = InputSanitizer.SanitizeString(userId, 50);
            return InputSanitizer.ValidateGuid(sanitized, "userId");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid user ID format: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Validates invoice ID from route parameter.
    /// </summary>
    private Guid? ValidateInvoiceIdParameter(string? invoiceId)
    {
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            _logger.LogWarning("Empty invoice ID received");
            return null;
        }

        try
        {
            var sanitized = InputSanitizer.SanitizeString(invoiceId, 50);
            return InputSanitizer.ValidateGuid(sanitized, "invoiceId");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid invoice ID format: {InvoiceId}", invoiceId);
            return null;
        }
    }

    /// <summary>
    /// Validates date range from request body.
    /// </summary>
    private (DateTime Start, DateTime End)? ValidateDateRange(DateRangeRequest? request)
    {
        if (request == null)
        {
            _logger.LogWarning("Null date range request");
            return null;
        }

        try
        {
            var start = InputSanitizer.ValidateDateTime(request.StartDate, "StartDate");
            var end = InputSanitizer.ValidateDateTime(request.EndDate, "EndDate");

            if (end <= start)
            {
                _logger.LogWarning("End date must be after start date");
                return null;
            }

            // Limit range to 1 year
            if ((end - start).TotalDays > 365)
            {
                _logger.LogWarning("Date range exceeds maximum (365 days)");
                return null;
            }

            return (start, end);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid date range");
            return null;
        }
    }

    /// <summary>
    /// Logs API request for auditing.
    /// </summary>
    private void LogRequest(string action, Guid targetId)
    {
        _logger.LogInformation(
            "API request: {Action} for {TargetId} from {RemoteIp}",
            action,
            targetId,
            HttpContext.Connection.RemoteIpAddress);
    }
}

/// <summary>
/// Request model for custom date range invoice generation.
/// </summary>
public class DateRangeRequest
{
    /// <summary>
    /// Gets or sets the start date of the range.
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the range.
    /// </summary>
    [Required]
    public DateTime EndDate { get; set; }
}
