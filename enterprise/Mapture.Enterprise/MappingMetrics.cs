using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Mapture.Enterprise
{
    public sealed class MappingMetrics
    {
        private readonly ConcurrentDictionary<string, MappingStats> _stats = new();

        public void RecordMapping(string mappingKey, long elapsedTicks)
        {
            _stats.AddOrUpdate(mappingKey,
                _ => new MappingStats(1, elapsedTicks),
                (_, existing) => existing.Record(elapsedTicks));
        }

        public MappingStats? GetStats(string mappingKey)
        {
            return _stats.TryGetValue(mappingKey, out var stats) ? stats : null;
        }

        public ConcurrentDictionary<string, MappingStats> GetAllStats() => _stats;
    }

    public sealed class MappingStats
    {
        private long _count;
        private long _totalTicks;
        private long _maxTicks;

        public long Count => _count;
        public double AverageMs => _count == 0 ? 0 : (_totalTicks / (double)_count) / Stopwatch.Frequency * 1000;
        public double MaxMs => _maxTicks / (double)Stopwatch.Frequency * 1000;

        public MappingStats(long count, long totalTicks)
        {
            _count = count;
            _totalTicks = totalTicks;
            _maxTicks = totalTicks;
        }

        internal MappingStats Record(long elapsedTicks)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalTicks, elapsedTicks);

            long currentMax;
            do
            {
                currentMax = _maxTicks;
                if (elapsedTicks <= currentMax) break;
            }
            while (Interlocked.CompareExchange(ref _maxTicks, elapsedTicks, currentMax) != currentMax);

            return this;
        }
    }

    public sealed class MappingAuditEntry
    {
        public string SourceType { get; set; } = default!;
        public string DestinationType { get; set; } = default!;
        public DateTime Timestamp { get; set; }
        public string[] MappedFields { get; set; } = Array.Empty<string>();
        public double ElapsedMs { get; set; }
    }
}
