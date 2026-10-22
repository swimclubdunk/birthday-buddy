using System.Runtime.InteropServices;

namespace BirthdayBuddy.Core;

/// <summary>Keeps one copy running per user session; later launches ask it to show its window.</summary>
public sealed class SingleInstance : IDisposable
{
    const string DefaultMutexName = @"Local\BirthdayBuddy.Instance";
    const string DefaultEventName = @"Local\BirthdayBuddy.Show";

    readonly Mutex _mutex;
    readonly EventWaitHandle _showRequested;
    RegisteredWaitHandle? _wait;

    SingleInstance(Mutex mutex, EventWaitHandle showRequested)
    {
        _mutex = mutex;
        _showRequested = showRequested;
    }

    /// <summary>Returns null when another instance already holds the mutex.</summary>
    public static SingleInstance? TryAcquire(string mutexName = DefaultMutexName, string eventName = DefaultEventName)
    {
        var mutex = new Mutex(initiallyOwned: false, mutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, eventName));
    }

    /// <summary>Asks the running instance to show itself, letting it take the foreground.</summary>
    public static void SignalExisting(string eventName = DefaultEventName)
    {
        if (!EventWaitHandle.TryOpenExisting(eventName, out var handle)) return;
        using (handle)
        {
            AllowSetForegroundWindow(AsfwAny);
            handle.Set();
        }
    }

    /// <summary>Runs <paramref name="callback"/> on <paramref name="context"/> each time another launch signals.</summary>
    public void OnActivationRequested(Action callback, SynchronizationContext context)
    {
        _wait?.Unregister(null);
        _wait = ThreadPool.RegisterWaitForSingleObject(
            _showRequested, (_, _) => context.Post(_ => callback(), null), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _wait?.Unregister(null);
        _showRequested.Dispose();
        _mutex.Dispose();
    }

    const int AsfwAny = -1;

    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}
