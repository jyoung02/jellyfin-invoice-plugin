# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Jellyfin plugin that tracks user viewing activity and generates invoices based on consumption.

## Build & Development Commands

```bash
dotnet build                    # Build the plugin
dotnet test                     # Run unit tests
dotnet publish -c Release       # Build release DLL for deployment
```

Deploy: Copy the compiled DLL to Jellyfin's plugin directory.

## Architecture

```
JellyfinInvoice/
├── Plugin.cs                   # Plugin entry point, registration
├── Configuration/
│   └── PluginConfiguration.cs  # User-configurable settings
├── Services/
│   ├── ViewingTracker.cs       # Hooks into playback events, records views
│   ├── InvoiceGenerator.cs     # Calculates charges, creates invoices
│   └── DataStore.cs            # Persists viewing records and invoices
├── Models/
│   ├── ViewingRecord.cs        # Single viewing session data
│   └── Invoice.cs              # Invoice with line items
├── Validation/
│   └── InputSanitizer.cs       # All input validation functions
├── Api/
│   └── InvoiceController.cs    # REST endpoints for invoice retrieval
└── Tests/
    └── *.Tests.cs              # Unit tests for each module
```

## Security Requirements (MANDATORY)

1. **All inputs must be sanitized** - This includes:
   - API request parameters
   - User IDs from Jellyfin events
   - Media metadata (titles, paths)
   - Log file content when parsed
   - Configuration values

2. **Use parameterized queries** - Never concatenate strings for data storage

3. **Validate before use** - Every function that accepts external data must validate it as the first operation

4. **Principle of least privilege** - Request only necessary Jellyfin permissions

## Code Standards

- **One function, one responsibility** - Extract logic into small, testable functions
- **Every function must have XML documentation** - Summary, params, returns, exceptions
- **No inline complex logic** - If it needs a comment, extract to a named function
- **Fail securely** - On validation failure, reject and log; never proceed with suspect data

## Input Sanitization Pattern

All external data flows through `InputSanitizer` before use:
```csharp
// Always validate at entry point
var safeUserId = InputSanitizer.ValidateUserId(rawUserId);
var safeTitle = InputSanitizer.SanitizeString(rawTitle, maxLength: 200);
```
