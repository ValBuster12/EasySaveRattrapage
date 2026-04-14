using System.Collections.Concurrent;
using EasySave.ViewModels;

namespace EasySave.Models.Network;

/// <summary>
///     Thread-safe runtime registry used to map remote command targets to active job items.
/// </summary>
public sealed class BackupJobRuntimeRegistry
{
    private readonly ConcurrentDictionary<int, BackupJobItemViewModel> _jobsById = new();

    public void RegisterJobs(IEnumerable<BackupJobItemViewModel> jobs)
    {
        ArgumentNullException.ThrowIfNull(jobs);

        var latestIds = new HashSet<int>();
        foreach (var jobItem in jobs)
        {
            latestIds.Add(jobItem.Job.Id);
            _jobsById[jobItem.Job.Id] = jobItem;
        }

        foreach (var knownJobId in _jobsById.Keys)
            if (!latestIds.Contains(knownJobId))
                _jobsById.TryRemove(knownJobId, out _);
    }

    public bool TryGetJobItem(int jobId, out BackupJobItemViewModel? jobItem)
    {
        if (_jobsById.TryGetValue(jobId, out var resolved))
        {
            jobItem = resolved;
            return true;
        }

        jobItem = null;
        return false;
    }

    public IReadOnlyList<BackupJobItemViewModel> SnapshotJobs()
    {
        return _jobsById.Values.OrderBy(item => item.Job.Id).ToList();
    }
}
