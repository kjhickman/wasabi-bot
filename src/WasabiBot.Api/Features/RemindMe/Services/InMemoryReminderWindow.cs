using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.RemindMe.Services;

/// <summary>
/// Maintains a sorted in-memory window of upcoming reminders capped by Capacity.
/// Thread-safe via internal lock; optimized for small collections up to the configured Capacity.
/// </summary>
public sealed class InMemoryReminderWindow
{
    private readonly Lock _lock = new();
    private readonly List<ReminderEntity> _reminders = new();
    public int Capacity { get; }
    public DateTimeOffset? LastDueTime { get; private set; }

    public InMemoryReminderWindow(int capacity = 1000)
    {
        Capacity = capacity;
    }

    public int Count
    {
        get { lock (_lock) return _reminders.Count; }
    }

    /// <summary>
    /// Loads (or reloads) the window with a fresh batch, replacing existing reminders.
    /// </summary>
    public void LoadInitial(IEnumerable<ReminderEntity> reminders)
    {
        lock (_lock)
        {
            _reminders.Clear();
            foreach (var r in reminders.OrderBy(r => r.RemindAt))
            {
                if (_reminders.Count < Capacity)
                {
                    _reminders.Add(r);
                }
                else
                {
                    break; // stop adding once capacity reached
                }
            }
            LastDueTime = _reminders.Count == 0 ? null : _reminders[^1].RemindAt;
        }
    }

    /// <summary>
    /// Inserts a newly scheduled reminder into the sorted window if it fits within capacity constraints.
    /// If at capacity and the reminder is later than current LastDueTime, it is ignored (will be picked up on a future refresh).
    /// If earlier than some existing reminders, it is inserted and the farthest future reminder may be dropped to maintain capacity.
    /// </summary>
    public void Insert(ReminderEntity entity)
    {
        if (entity.IsReminderSent) return; // ignore already sent

        lock (_lock)
        {
            // Binary search by RemindAt then Id for stable ordering
            int index = _reminders.BinarySearch(entity, ReminderComparer.Instance);
            if (index < 0) index = ~index;

            if (_reminders.Count < Capacity)
            {
                _reminders.Insert(index, entity);
            }
            else
            {
                // At capacity
                if (index >= Capacity)
                {
                    // New reminder is beyond current window tail -> ignore
                    return;
                }
                // Insert and drop the last item to keep capacity
                _reminders.Insert(index, entity);
                if (_reminders.Count > Capacity)
                {
                    _reminders.RemoveAt(_reminders.Count - 1);
                }
            }
            LastDueTime = _reminders.Count == 0 ? null : _reminders[^1].RemindAt;
        }
    }

    /// <summary>
    /// Returns all reminders due at or before 'now' WITHOUT removing them.
    /// Caller should remove only after successful persistence operations.
    /// </summary>
    public List<ReminderEntity> GetDue(DateTimeOffset now)
    {
        lock (_lock)
        {
            if (_reminders.Count == 0) return new List<ReminderEntity>();
            int dueCount = 0;
            foreach (var r in _reminders)
            {
                if (r.RemindAt <= now) dueCount++;
                else break;
            }
            if (dueCount == 0) return new List<ReminderEntity>();
            return _reminders.Take(dueCount).ToList();
        }
    }

    /// <summary>
    /// Removes reminders by their Id.
    /// </summary>
    /// <param name="id">The Id of the reminder to remove.</param>
    public void RemoveById(long id)
    {
        lock (_lock)
        {
            if (_reminders.Count == 0) return;
            _reminders.RemoveAll(r => r.Id == id);
            LastDueTime = _reminders.Count == 0 ? null : _reminders[^1].RemindAt;
        }
    }

    /// <summary>
    /// Determines if a refresh (DB fetch) is needed based on emptiness or last due time reached.
    /// </summary>
    public bool NeedsRefresh(DateTimeOffset now)
    {
        lock (_lock)
        {
            return _reminders.Count == 0 || (LastDueTime.HasValue && now >= LastDueTime.Value);
        }
    }

    private sealed class ReminderComparer : IComparer<ReminderEntity>
    {
        public static readonly ReminderComparer Instance = new();
        public int Compare(ReminderEntity? x, ReminderEntity? y)
        {
            if (x == null || y == null)
            {
                return x == y ? 0 : x == null ? -1 : 1;
            }
            int cmp = x.RemindAt.CompareTo(y.RemindAt);
            if (cmp != 0) return cmp;
            return x.Id.CompareTo(y.Id);
        }
    }
}
