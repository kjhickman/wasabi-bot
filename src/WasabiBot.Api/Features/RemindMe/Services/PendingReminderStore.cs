using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.RemindMe.Services;

public sealed class PendingReminderStore
{
    private readonly Lock _lock = new();
    private readonly SortedSet<ReminderEntity> _sorted = new(ReminderComparer.Instance);
    private readonly Dictionary<long, ReminderEntity> _byId = new();
    private readonly SemaphoreSlim _earlierSignal = new(0, 1);

    public DateTimeOffset? NextDueTime { get { lock (_lock) return _sorted.Min?.RemindAt; } }

    public void InsertMany(IEnumerable<ReminderEntity> reminders)
    {
        var earliestChanged = false;
        lock (_lock)
        {
            var previousEarliest = _sorted.Min?.RemindAt;
            foreach (var r in reminders.Where(x => !x.IsReminderSent).OrderBy(r => r.RemindAt).ThenBy(r => r.Id))
            {
                if (!_sorted.Add(r)) continue;

                _byId[r.Id] = r;
                var newEarliest = _sorted.Min?.RemindAt;
                earliestChanged = previousEarliest == null || (newEarliest != null && newEarliest < previousEarliest);
            }
        }

        if (earliestChanged)
        {
            SignalEarlier();
        }
    }

    public void Insert(ReminderEntity entity)
    {
        if (entity.IsReminderSent) return;
        bool earliestChanged;
        lock (_lock)
        {
            var previousEarliest = _sorted.Min?.RemindAt;

            if (_sorted.Add(entity))
            {
                _byId[entity.Id] = entity;
            }
            var newEarliest = _sorted.Min?.RemindAt;
            earliestChanged = previousEarliest == null || (newEarliest != null && newEarliest < previousEarliest);
        }

        if (earliestChanged)
        {
            SignalEarlier();
        }
    }

    public List<ReminderEntity> GetAllDueReminders(DateTimeOffset now)
    {
        lock (_lock)
        {
            if (_sorted.Count == 0) return new();
            var list = new List<ReminderEntity>();
            foreach (var r in _sorted)
            {
                if (r.RemindAt <= now) list.Add(r); else break;
            }
            return list;
        }
    }

    public void RemoveById(long id)
    {
        bool earliestChanged;
        lock (_lock)
        {
            if (!_byId.TryGetValue(id, out var entity)) return;
            var previousEarliest = _sorted.Min?.RemindAt;
            _sorted.Remove(entity);
            _byId.Remove(id);
            var newEarliest = _sorted.Min?.RemindAt;
            earliestChanged = previousEarliest != newEarliest;
        }
        if (earliestChanged) SignalEarlier();
    }

    public Task WaitForEarlierAsync(CancellationToken ct) => _earlierSignal.WaitAsync(ct);

    private void SignalEarlier()
    {
        if (_earlierSignal.CurrentCount != 0)
        {
            return;
        }

        try
        {
            _earlierSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            /* should not happen due to check */
        }
    }

    private sealed class ReminderComparer : IComparer<ReminderEntity>
    {
        public static readonly ReminderComparer Instance = new();
        public int Compare(ReminderEntity? x, ReminderEntity? y)
        {
            if (x == null || y == null) return x == y ? 0 : x == null ? -1 : 1;
            var cmp = x.RemindAt.CompareTo(y.RemindAt);
            return cmp != 0 ? cmp : x.Id.CompareTo(y.Id);
        }
    }
}
