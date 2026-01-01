using System;

namespace JellyfinInvoice.Models;

/// <summary>
/// Type of media item for billing purposes.
/// </summary>
public enum MediaItemType
{
    /// <summary>Unknown or other content type.</summary>
    Other = 0,
    /// <summary>A movie.</summary>
    Movie = 1,
    /// <summary>A TV episode.</summary>
    Episode = 2
}

/// <summary>
/// Represents a single viewing session for a user.
/// Immutable after creation to ensure data integrity.
/// </summary>
public sealed class ViewingRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ViewingRecord"/> class.
    /// </summary>
    /// <param name="id">Unique identifier for this record.</param>
    /// <param name="userId">The Jellyfin user ID (already sanitized).</param>
    /// <param name="itemId">The media item ID (already sanitized).</param>
    /// <param name="itemName">The display name of the media (already sanitized).</param>
    /// <param name="itemType">The type of media (movie, episode, etc.).</param>
    /// <param name="startTime">When playback started (UTC).</param>
    /// <param name="endTime">When playback ended (UTC).</param>
    /// <param name="durationTicks">Total playback duration in ticks.</param>
    public ViewingRecord(
        Guid id,
        Guid userId,
        Guid itemId,
        string itemName,
        MediaItemType itemType,
        DateTime startTime,
        DateTime endTime,
        long durationTicks)
    {
        Id = id;
        UserId = userId;
        ItemId = itemId;
        ItemName = itemName;
        ItemType = itemType;
        StartTime = startTime;
        EndTime = endTime;
        DurationTicks = durationTicks;
    }

    /// <summary>
    /// Gets the unique identifier for this viewing record.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the Jellyfin user ID who watched the content.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the Jellyfin media item ID that was watched.
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// Gets the sanitized display name of the media item.
    /// </summary>
    public string ItemName { get; }

    /// <summary>
    /// Gets the type of media item (movie, episode, etc.).
    /// </summary>
    public MediaItemType ItemType { get; }

    /// <summary>
    /// Gets the UTC timestamp when playback started.
    /// </summary>
    public DateTime StartTime { get; }

    /// <summary>
    /// Gets the UTC timestamp when playback ended.
    /// </summary>
    public DateTime EndTime { get; }

    /// <summary>
    /// Gets the total playback duration in ticks.
    /// </summary>
    public long DurationTicks { get; }

    /// <summary>
    /// Gets the duration as a TimeSpan.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);

    /// <summary>
    /// Gets the duration in hours for billing calculations.
    /// </summary>
    public decimal DurationHours => (decimal)Duration.TotalHours;
}
