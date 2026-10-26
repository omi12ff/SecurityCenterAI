namespace SecurityCenterAI.Api.Services.Ai;

/// <summary>Process-local UTC quota. Attempts are charged before one provider call, including failed calls.</summary>
public sealed class AiExplanationQuota(TimeProvider clock)
{
    private const int MaximumOwners = 10_000;
    private const int MaximumConcurrent = 4;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _owners = [];
    private DateOnly _lastCleanup;
    private int _active;

    public IDisposable Reserve(Guid ownerId, int dailyLimit)
    {
        if (dailyLimit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(dailyLimit));
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (_lastCleanup != today || _owners.Count >= MaximumOwners)
            {
                foreach (var id in _owners.Where(item => item.Value.Day != today && !item.Value.Active)
                    .Select(item => item.Key).ToArray()) _owners.Remove(id);
                _lastCleanup = today;
            }

            _owners.TryGetValue(ownerId, out var entry);
            if (entry?.Active == true)
                throw new DeviceOperationException(429, "Ya estamos preparando una explicación para tu cuenta. Esperá a que termine antes de pedir otra.");
            if (_active >= MaximumConcurrent)
                throw new DeviceOperationException(429, "Las explicaciones con IA están ocupadas. Esperá un momento y volvé a intentar.");
            if (entry is null)
            {
                // Do not evict today's usage, which would let busy traffic bypass the daily limit.
                if (_owners.Count >= MaximumOwners)
                    throw new DeviceOperationException(429, "Las explicaciones con IA están ocupadas. Volvé a intentar más tarde.");
                entry = new Entry { Day = today };
                _owners.Add(ownerId, entry);
            }
            if (entry.Day != today) { entry.Day = today; entry.Attempts = 0; }
            if (entry.Attempts >= dailyLimit)
                throw new DeviceOperationException(429, "Alcanzaste el límite diario de explicaciones con IA. Se renueva al comenzar el siguiente día UTC.");
            entry.Attempts++;
            entry.Active = true;
            _active++;
            return new Reservation(this, entry);
        }
    }

    private void Release(Entry entry)
    {
        lock (_gate)
        {
            entry.Active = false;
            _active--;
        }
    }

    private sealed class Entry
    {
        public DateOnly Day { get; set; }
        public int Attempts { get; set; }
        public bool Active { get; set; }
    }

    private sealed class Reservation(AiExplanationQuota owner, Entry entry) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.Release(entry);
        }
    }
}
