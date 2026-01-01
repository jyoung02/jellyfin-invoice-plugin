# Jellyfin Invoice Generator Plugin

[![Version](https://img.shields.io/badge/version-1.2.3-blue.svg)](https://github.com/jyoung02/jellyfin-invoice-plugin/releases)
[![Jellyfin](https://img.shields.io/badge/Jellyfin-10.9+-purple.svg)](https://jellyfin.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

A Jellyfin plugin that tracks user viewing activity and generates invoices based on media consumption. Designed for media libraries that need to bill users based on their viewing time.

## What It Does

This plugin monitors playback events in Jellyfin and creates detailed invoices for users based on their watching habits:

1. **Tracks Viewing Sessions** - Listens to playback start/stop events and records each viewing session with user, media item, duration, and timestamps
2. **Calculates Charges** - Applies configurable hourly rates to viewing time
3. **Generates Invoices** - Creates itemized invoices showing what was watched, for how long, and the total cost
4. **Provides REST API** - Exposes endpoints to retrieve and generate invoices programmatically

## Features

- **Automatic Playback Tracking** - Hooks into Jellyfin's playback events (sessions under 30 seconds are excluded)
- **Configurable Billing** - Set your own hourly rate, currency, and billing period
- **Custom Date Ranges** - Generate invoices for any time period (up to 365 days)
- **Invoice Viewer UI** - View and generate invoices directly from Jellyfin's dashboard
- **Thread-Safe Storage** - JSON-based persistence with atomic file operations
- **Security-First Design** - Comprehensive input validation against injection attacks
- **Web Configuration UI** - Configure the plugin directly from Jellyfin's dashboard

## Installation

1. Download the latest release from the [Releases](https://github.com/jyoung02/jellyfin-invoice-plugin/releases) page
2. Extract `JellyfinInvoice.dll` to your Jellyfin plugins directory:
   - **Windows**: `C:\ProgramData\Jellyfin\Server\plugins\JellyfinInvoice`
   - **Linux**: `/var/lib/jellyfin/plugins/JellyfinInvoice`
   - **Docker**: `/config/plugins/JellyfinInvoice`
3. Restart Jellyfin Server
4. Navigate to **Dashboard > Plugins** to verify installation

## Configuration

Access the plugin settings via **Dashboard > Plugins > Invoice Generator**:

| Setting | Description | Default |
|---------|-------------|---------|
| Enable Tracking | Turn viewing tracking on/off | `false` |
| Currency Code | ISO 4217 currency code (e.g., USD, EUR, GBP) | `USD` |
| Default Rate Per Hour | Hourly rate for viewing charges | `0.00` |
| Invoice Period Days | Default billing period length | `30` |
| Max Title Length | Maximum characters for media titles | `200` |
| Max Description Length | Maximum characters for descriptions | `500` |

## Viewing Invoices

Access the invoice viewer from **Dashboard > Plugins > Invoice Generator**, then click **Open Invoice Viewer**:

1. Select a user from the dropdown
2. View all invoices with billing period, total amount, and creation date
3. Click any invoice to see detailed line items:
   - Description (media title and date)
   - Hours watched
   - Hourly rate
   - Line item amount
4. Click **Generate Invoice** to create an invoice for the current billing period

## API Endpoints

All endpoints require authentication and return JSON.

### Get User Invoices
```
GET /Invoice/User/{userId}
```
Returns all invoices for the specified user.

### Get Invoice
```
GET /Invoice/{invoiceId}
```
Returns a specific invoice by ID.

### Generate Current Period Invoice
```
POST /Invoice/Generate/{userId}
```
Generates an invoice for the current billing period based on configured `InvoicePeriodDays`.

### Generate Custom Range Invoice
```
POST /Invoice/Generate/{userId}/Range
Content-Type: application/json

{
  "startDate": "2024-01-01T00:00:00Z",
  "endDate": "2024-01-31T23:59:59Z"
}
```
Generates an invoice for a custom date range (max 365 days).

## Building from Source

### Requirements
- .NET 8.0 SDK
- Jellyfin Server 10.9+

### Build Commands
```bash
# Build the plugin
dotnet build

# Run tests
dotnet test

# Create release build
dotnet publish -c Release
```

The compiled plugin will be at `bin/Release/net8.0/publish/JellyfinInvoice.dll`.

## Project Structure

```
├── Plugin.cs                    # Plugin entry point
├── Configuration/
│   ├── PluginConfiguration.cs   # Settings model
│   ├── configPage.html          # Settings web UI
│   └── invoicesPage.html        # Invoice viewer UI
├── Models/
│   ├── ViewingRecord.cs         # Playback session data
│   └── Invoice.cs               # Invoice and line items
├── Services/
│   ├── ViewingTracker.cs        # Playback event listener
│   ├── InvoiceGenerator.cs      # Billing calculations
│   └── DataStore.cs             # JSON persistence
├── Validation/
│   └── InputSanitizer.cs        # Input validation
├── Api/
│   └── InvoiceController.cs     # REST endpoints
└── Tests/
    └── *.cs                     # Unit tests
```

## Security

This plugin implements multiple security measures:

- **Input Validation** - All external data is sanitized before use
- **Injection Prevention** - Regex-based detection of SQL, XSS, and path traversal attacks
- **Authentication Required** - All API endpoints require Jellyfin authentication
- **Atomic File Operations** - Data writes use temp files with move-on-success pattern
- **Thread Safety** - Concurrent access protected with SemaphoreSlim

## License

This project is provided as-is for personal use with Jellyfin media server.
