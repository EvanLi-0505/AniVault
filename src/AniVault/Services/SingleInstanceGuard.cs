using System;
using System.Threading;

namespace AniVault.Services;

/// <summary>
/// Keeps AniVault to a single running process. The first instance holds a named mutex and
/// listens on a named event; a second instance signals that event (so the first can come to
/// the front) and then exits. Everything is local to the current Windows session.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateSignal;
    private readonly Thread? _listener;
    private volatile bool _stopped;

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{name}.Instance", out var createdNew);
        IsPrimary = createdNew;
        _activateSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Activate");

        if (IsPrimary)
        {
            _listener = new Thread(ListenLoop) { IsBackground = true, Name = "SingleInstanceListener" };
            _listener.Start();
        }
    }

    /// <summary>True when this process is the first / only instance.</summary>
    public bool IsPrimary { get; }

    /// <summary>Raised on a background thread when another instance asked this one to activate.</summary>
    public event Action? ActivationRequested;

    /// <summary>Called by a secondary instance to wake the primary one, then that instance should exit.</summary>
    public void SignalPrimaryInstance()
    {
        try
        {
            _activateSignal.Set();
        }
        catch (ObjectDisposedException)
        {
            // primary went away between the mutex check and here — nothing to signal.
        }
    }

    private void ListenLoop()
    {
        while (!_stopped)
        {
            try
            {
                if (_activateSignal.WaitOne(500) && !_stopped)
                {
                    ActivationRequested?.Invoke();
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _stopped = true;

        try
        {
            _activateSignal.Set();      // unblock the listener so the thread ends promptly
        }
        catch (ObjectDisposedException)
        {
        }

        _activateSignal.Dispose();

        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // not the owner (shouldn't happen for the primary) — ignore.
            }
        }

        _mutex.Dispose();
    }
}
