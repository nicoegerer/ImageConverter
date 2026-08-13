using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageConverter.Core.Models;

namespace ImageConverter.Core.Services;

public sealed class ExportService(ImageProcessingService processor)
{
    public async Task<IReadOnlyList<ExportResult>> ExportAsync(IReadOnlyList<ExportJob> jobs, int maxParallelism, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (jobs.Count == 0)
        {
            return Array.Empty<ExportResult>();
        }
        ExportResult?[] results = new ExportResult[jobs.Count];
        int completed = 0;
        using var semaphore = new SemaphoreSlim(Math.Clamp(maxParallelism, 1, 4));
        await Task.WhenAll(jobs.Select(async (job, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                ExportResult exportResult = await processor.ProcessAsync(job, cancellationToken);
                results[index] = exportResult;
                int completedCount = Interlocked.Increment(ref completed);
                progress?.Report(new ExportProgress(completedCount, jobs.Count, job.Source.File.FileName, exportResult));
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return results.Select(result => result ?? throw new InvalidOperationException("An export job completed without a result.")).ToArray();
    }
}
