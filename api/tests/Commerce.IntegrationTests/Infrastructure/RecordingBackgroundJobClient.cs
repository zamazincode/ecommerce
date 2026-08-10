using System.Collections.Concurrent;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace Commerce.IntegrationTests.Infrastructure;

public sealed record RecordedJob(
    Type Type, string Method, IReadOnlyList<object?> Args, string State, DateTime? EnqueueAt);

/// DIŞ DÜNYA SINIRI: Testing'de Hangfire worker'ı YOK; gerçek storage'a yazmanın
/// tek etkisi test container'ında bir "hangfire" şeması açmak olurdu (ölçüldü).
/// Bu sahte istemci kaydı alır, hiçbir şey çalıştırmaz.
///
/// IBackgroundJobClient'ın TAMAMI iki metot (ölçüldü) — kılavuzun
/// HangfireTestHelper'ı (JobStorage.Current üzerinden) BİLEREK yazılmadı:
/// EnqueuedJobDto.Job tipi çözülemeyen kayıtlarda null dönüyor.
public sealed class RecordingBackgroundJobClient : IBackgroundJobClient
{
    public ConcurrentBag<RecordedJob> Jobs { get; } = [];
    private int _next;

    public string Create(Job job, IState state)
    {
        // Hangfire'ın bu imzayı GERÇEKTEN serileştirebildiğini burada kanıtlıyoruz;
        // aksi hâlde "testte geçer, üretimde patlar" boşluğu açık kalırdı.
        InvocationData.SerializeJob(job);

        Jobs.Add(new RecordedJob(job.Type, job.Method.Name, job.Args,
                                 state.Name, (state as ScheduledState)?.EnqueueAt));
        return Interlocked.Increment(ref _next).ToString();
    }

    public bool ChangeState(string jobId, IState state, string expectedState) => true;

    public IEnumerable<RecordedJob> For<T>(string method)
        => Jobs.Where(j => j.Type == typeof(T) && j.Method == method);
}
