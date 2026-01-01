using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using JellyfinInvoice.Configuration;
using JellyfinInvoice.Models;
using JellyfinInvoice.Validation;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JellyfinInvoice.Services;

/// <summary>
/// Tracks user viewing activity by listening to Jellyfin playback events.
/// Creates ViewingRecords when playback sessions complete.
/// </summary>
public sealed class ViewingTracker : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<ViewingTracker> _logger;
    private readonly DataStore _dataStore;

    /// <summary>
    /// Tracks active playback sessions: SessionId -> StartTime.
    /// </summary>
    private readonly ConcurrentDictionary<string, PlaybackSession> _activeSessions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewingTracker"/> class.
    /// </summary>
    /// <param name="sessionManager">Jellyfin session manager.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="dataStore">Data storage service.</param>
    public ViewingTracker(
        ISessionManager sessionManager,
        ILogger<ViewingTracker> logger,
        DataStore dataStore)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    /// <summary>
    /// Starts tracking playback events.
    /// Called by Jellyfin when the plugin loads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _logger.LogInformation("ViewingTracker started");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops tracking playback events.
    /// Called by Jellyfin when the plugin unloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _activeSessions.Clear();
        _logger.LogInformation("ViewingTracker stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles playback start events.
    /// </summary>
    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        if (!IsTrackingEnabled())
        {
            return;
        }

        var sessionId = ExtractSessionId(e);
        if (sessionId == null)
        {
            return;
        }

        var session = CreatePlaybackSession(e);
        if (session == null)
        {
            return;
        }

        _activeSessions.TryAdd(sessionId, session);
        _logger.LogDebug(
            "Playback started: User={UserId}, Item={ItemId}",
            session.UserId,
            session.ItemId);
    }

    /// <summary>
    /// Handles playback stop events.
    /// </summary>
    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        if (!IsTrackingEnabled())
        {
            return;
        }

        var sessionId = ExtractSessionId(e);
        if (sessionId == null)
        {
            return;
        }

        if (!_activeSessions.TryRemove(sessionId, out var session))
        {
            _logger.LogDebug("No active session found for {SessionId}", sessionId);
            return;
        }

        var record = CreateViewingRecord(session, e);
        if (record == null)
        {
            return;
        }

        SaveRecord(record);
    }

    /// <summary>
    /// Checks if tracking is enabled in configuration.
    /// </summary>
    private static bool IsTrackingEnabled()
    {
        var config = Plugin.Instance?.Configuration;
        return config?.EnableTracking ?? false;
    }

    /// <summary>
    /// Extracts and validates session ID from event args.
    /// </summary>
    private string? ExtractSessionId(PlaybackProgressEventArgs e)
    {
        var rawSessionId = e.Session?.Id;
        if (string.IsNullOrEmpty(rawSessionId))
        {
            _logger.LogDebug("Playback event with no session ID");
            return null;
        }

        return InputSanitizer.SanitizeString(rawSessionId, 100);
    }

    /// <summary>
    /// Creates a playback session record from event args.
    /// </summary>
    private PlaybackSession? CreatePlaybackSession(PlaybackProgressEventArgs e)
    {
        try
        {
            var userId = ExtractUserId(e);
            if (userId == null)
            {
                return null;
            }

            var itemId = ExtractItemId(e);
            if (itemId == null)
            {
                return null;
            }

            var itemName = ExtractItemName(e);
            var itemType = ExtractItemType(e);

            return new PlaybackSession
            {
                UserId = userId.Value,
                ItemId = itemId.Value,
                ItemName = itemName,
                ItemType = itemType,
                StartTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create playback session");
            return null;
        }
    }

    /// <summary>
    /// Extracts the media item type from event args.
    /// </summary>
    private MediaItemType ExtractItemType(PlaybackProgressEventArgs e)
    {
        var item = e.Item;
        if (item == null)
        {
            return MediaItemType.Other;
        }

        // Check the item type using Jellyfin's type system
        var typeName = item.GetType().Name;
        return typeName switch
        {
            "Movie" => MediaItemType.Movie,
            "Episode" => MediaItemType.Episode,
            _ => MediaItemType.Other
        };
    }

    /// <summary>
    /// Extracts and validates user ID from event args.
    /// </summary>
    private Guid? ExtractUserId(PlaybackProgressEventArgs e)
    {
        var rawUserId = e.Session?.UserId;
        if (rawUserId == null || rawUserId == Guid.Empty)
        {
            _logger.LogDebug("Playback event with no user ID");
            return null;
        }

        try
        {
            return InputSanitizer.ValidateGuid(rawUserId.Value, "UserId");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid user ID in playback event");
            return null;
        }
    }

    /// <summary>
    /// Extracts and validates item ID from event args.
    /// </summary>
    private Guid? ExtractItemId(PlaybackProgressEventArgs e)
    {
        var rawItemId = e.Item?.Id;
        if (rawItemId == null || rawItemId == Guid.Empty)
        {
            _logger.LogDebug("Playback event with no item ID");
            return null;
        }

        try
        {
            return InputSanitizer.ValidateGuid(rawItemId.Value, "ItemId");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid item ID in playback event");
            return null;
        }
    }

    /// <summary>
    /// Extracts and sanitizes item name from event args.
    /// </summary>
    private string ExtractItemName(PlaybackProgressEventArgs e)
    {
        var rawName = e.Item?.Name ?? "Unknown";
        var maxLength = Plugin.Instance?.Configuration?.MaxTitleLength ?? 200;
        return InputSanitizer.SanitizeString(rawName, maxLength);
    }

    /// <summary>
    /// Creates a viewing record from a completed session.
    /// </summary>
    private ViewingRecord? CreateViewingRecord(PlaybackSession session, PlaybackStopEventArgs e)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var durationTicks = CalculateDuration(session.StartTime, endTime, e);

            var validDuration = InputSanitizer.ValidateDurationTicks(durationTicks, "Duration");

            // Skip very short sessions (less than 30 seconds)
            if (validDuration < TimeSpan.FromSeconds(30).Ticks)
            {
                _logger.LogDebug("Skipping short session (< 30s)");
                return null;
            }

            return new ViewingRecord(
                id: Guid.NewGuid(),
                userId: session.UserId,
                itemId: session.ItemId,
                itemName: session.ItemName,
                itemType: session.ItemType,
                startTime: session.StartTime,
                endTime: endTime,
                durationTicks: validDuration
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create viewing record");
            return null;
        }
    }

    /// <summary>
    /// Calculates playback duration.
    /// </summary>
    private long CalculateDuration(DateTime startTime, DateTime endTime, PlaybackStopEventArgs e)
    {
        var reportedTicks = e.PlaybackPositionTicks;
        if (reportedTicks.HasValue && reportedTicks.Value > 0)
        {
            return reportedTicks.Value;
        }

        return (endTime - startTime).Ticks;
    }

    /// <summary>
    /// Saves a viewing record to storage.
    /// </summary>
    private void SaveRecord(ViewingRecord record)
    {
        try
        {
            _dataStore.SaveViewingRecord(record);
            _logger.LogInformation(
                "Recorded viewing: User={UserId}, Item={ItemName}, Duration={Duration}",
                record.UserId,
                record.ItemName,
                record.Duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save viewing record");
        }
    }

    /// <summary>
    /// Disposes of resources.
    /// </summary>
    public void Dispose()
    {
        _activeSessions.Clear();
    }

    /// <summary>
    /// Represents an active playback session.
    /// </summary>
    private sealed class PlaybackSession
    {
        public Guid UserId { get; init; }
        public Guid ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public MediaItemType ItemType { get; init; }
        public DateTime StartTime { get; init; }
    }
}
