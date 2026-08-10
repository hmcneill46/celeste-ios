#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Celeste;
using CoreFoundation;
using Foundation;
using Network;
using UIKit;

namespace CelesteTvOSHost;

internal sealed class Stage10ASaveManager : IDisposable
{
    internal const string BonjourServiceType = "_celeste-save._tcp";
    internal static readonly TimeSpan ManagerInactivityLifetime = TimeSpan.FromMinutes(12);

    private readonly object gate = new();
    private readonly Stage6PersistenceStore persistence;
    private readonly List<NSObject> observers = new();
    private readonly Dictionary<NWConnection, ConnectionState> connections = new();
    private readonly Stage10AConnectionGate connectionGate = new(Stage10AHttpProtocol.MaximumConcurrentConnections);
    private readonly DispatchQueue queue = new("Celeste Save Manager Network");
    private readonly Timer inactivityTimer;
    private NWListener? listener;
    private Stage10AHttpProtocol? protocol;
    private TvOSSaveManagerDisplayState status = new();
    private bool disposed;
    private bool idleTimerSuppressed;
    private bool restartRequired;
    private int bonjourAdds;
    private int bonjourRemoves;

    internal Stage10ASaveManager(Stage6PersistenceStore store)
    {
        persistence = store ?? throw new ArgumentNullException(nameof(store));
        inactivityTimer = new Timer(_ => Stop("inactivity-timeout"), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        TvOSSaveManagerHooks.StartRequested = Start;
        TvOSSaveManagerHooks.StatusRequested = Status;
        TvOSSaveManagerHooks.StopRequested = Stop;
        Observe(UIApplication.WillResignActiveNotification, "resign-active");
        Observe(UIApplication.DidEnterBackgroundNotification, "background");
        Observe(UIApplication.WillTerminateNotification, "termination");
        Stage3BLog.Info("STAGE10A_DORMANT listener=false; bonjour=false; activation=explicit-options-only");
#if TVOS_STAGE10A_AUTOMATION
        Stage3BLog.Warning("STAGE10A_AUTOMATION enabled=true; evidence=ignored-local-only");
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            Start();
        });
#endif
    }

    internal bool IsListening { get { lock (gate) return listener != null && status.Phase == "ready"; } }
    internal bool RestartRequired { get { lock (gate) return restartRequired; } }
    internal int BonjourAddCount { get { lock (gate) return bonjourAdds; } }
    internal int BonjourRemoveCount { get { lock (gate) return bonjourRemoves; } }

    internal void StopForLeave() => Stop("stage12b-user-leave");
    internal void StopForSoftReload() => Stop("stage13b-soft-reload");

    internal void CompleteSoftReload()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            StopNetworkObjects(clearStatus: false);
            restartRequired = false;
            status = new TvOSSaveManagerDisplayState { Phase = "stopped" };
            Stage3BLog.Info("STAGE13B_MANAGER result=complete; restart-required=false; listener=false; sessions=cleared");
        }
    }

    internal void MarkSoftReloadFailed(string category)
    {
        lock (gate)
        {
            if (disposed) return;
            StopNetworkObjects(clearStatus: false);
            restartRequired = true;
            status = new TvOSSaveManagerDisplayState
            {
                Phase = "restart-required",
                RestartRequired = true,
                Detail = category
            };
            Stage3BLog.Warning($"STAGE13B_MANAGER result=reload-failed; category={SanitizeReason(category)}; restart-required=true");
        }
    }

    private TvOSSaveManagerDisplayState Start()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (restartRequired)
                return new TvOSSaveManagerDisplayState { Phase = "restart-required", RestartRequired = true };
            if (status.Phase is "starting" or "ready") return CopyStatus(status);
            status = new TvOSSaveManagerDisplayState { Phase = "starting" };
            SetIdleTimerSuppressed(true);
            _ = Task.Run(StartCoreAsync);
            return CopyStatus(status);
        }
    }

    private async Task StartCoreAsync()
    {
        try
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(8);
            while (UserIO.Saving && DateTimeOffset.UtcNow < deadline) await Task.Delay(25).ConfigureAwait(false);
            if (UserIO.Saving) throw new TimeoutException("Celeste did not finish its in-progress save before the snapshot deadline.");

            Stage10AExportSnapshot stableSnapshot = persistence.CreateReadOnlySaveManagerSnapshot();
            IReadOnlyList<string> addresses = DiscoverLanAddresses();
            if (addresses.Count == 0)
            {
                lock (gate) status = new TvOSSaveManagerDisplayState { Phase = "unavailable" };
                Stage3BLog.Warning("STAGE10A_START result=no-lan-address; listener=false; bonjour=false");
                return;
            }

            Stage10AHttpProtocol nextProtocol = new(
                stableSnapshot,
                mutationHandler: persistence.MutateFromSaveManager
            );
            using NWParameters parameters = NWParameters.CreateTcp(_ => { });
            parameters.ReuseLocalAddress = true;
            NWListener nextListener = NWListener.Create(parameters)
                ?? throw new InvalidOperationException("Network framework did not create a TCP listener.");
            // Do not set NWListener.ConnectionLimit here. Physical tvOS treats
            // that property as a lifetime accept budget: after the code page,
            // authentication, and two requests, a value of four permanently
            // stopped the listener from accepting another connection. The
            // reusable Stage10AConnectionGate below enforces the intended
            // four-simultaneous-connection bound and releases each slot when
            // its native NWConnection closes.
            using NWAdvertiseDescriptor descriptor = NWAdvertiseDescriptor.CreateBonjourService(
                "Celeste Save Manager", BonjourServiceType, null
            ) ?? throw new InvalidOperationException("Network framework did not create a Bonjour descriptor.");
            descriptor.NoAutoRename = false;
            nextListener.SetAdvertiseDescriptor(descriptor);
            nextListener.SetAdvertisedEndpointChangedHandler((_, added) =>
            {
                lock (gate)
                {
                    if (added) bonjourAdds++; else bonjourRemoves++;
                }
                Stage3BLog.Info($"STAGE10A_BONJOUR change={(added ? "add" : "remove")}; endpoint-details=redacted");
            });
            nextListener.SetNewConnectionHandler(AcceptConnection);
            nextListener.SetStateChangedHandler((state, error) => ListenerStateChanged(nextListener, addresses, state, error));
            nextListener.SetQueue(queue);

            lock (gate)
            {
                if (disposed || status.Phase != "starting")
                {
                    nextProtocol.Stop();
                    nextListener.Cancel();
                    nextListener.Dispose();
                    return;
                }
                protocol = nextProtocol;
                listener = nextListener;
                connectionGate.Start();
                bonjourAdds = 0;
                bonjourRemoves = 0;
            }
            nextListener.Start();
            Stage3BLog.Info($"STAGE10A_START result=listener-starting; generation={stableSnapshot.Generation}; logical={stableSnapshot.LogicalHash}; access-code=not-logged");
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                status = new TvOSSaveManagerDisplayState
                {
                    Phase = "failed",
                    Detail = exception is TimeoutException ? "A save was still in progress. Please try again." : "Please check the local network and try again."
                };
            }
            Stage3BLog.Error($"STAGE10A_START result=failed; type={exception.GetType().Name}; message={exception.Message}");
            StopNetworkObjects(clearStatus: false);
        }
    }

    private void ListenerStateChanged(NWListener source, IReadOnlyList<string> addresses, NWListenerState listenerState, NWError? error)
    {
        lock (gate)
        {
            if (source != listener) return;
            switch (listenerState)
            {
                case NWListenerState.Ready:
                    ushort port = source.Port;
                    string[] urls = addresses.Select(address => Stage10ALanAddressPolicy.FormatUrl(address, port)).ToArray();
                    status = new TvOSSaveManagerDisplayState
                    {
                        Phase = "ready",
                        Urls = urls,
                        AccessCode = protocol?.AccessCode ?? ""
                    };
                    inactivityTimer.Change(ManagerInactivityLifetime, Timeout.InfiniteTimeSpan);
                    Stage3BLog.Info($"STAGE10A_READY port={port}; address-count={urls.Length}; bonjour=advertising; access-code=not-logged");
#if TVOS_STAGE10A_AUTOMATION
                    // This compile-time-only acceptance lane writes secrets to
                    // ignored device evidence so a Mac can stress the physical
                    // listener without asking a person to transcribe the TV UI.
                    Stage3BLog.Warning($"STAGE10A_AUTOMATION_READY url={urls[0]}; access-code={protocol?.AccessCode}");
#endif
                    break;
                case NWListenerState.Waiting:
                    status = new TvOSSaveManagerDisplayState { Phase = "starting", Detail = "Waiting for a local network..." };
                    Stage3BLog.Warning($"STAGE10A_LISTENER state=waiting; error={SafeError(error)}; url=cleared");
                    break;
                case NWListenerState.Failed:
                    status = new TvOSSaveManagerDisplayState { Phase = "failed", Detail = "The network listener stopped. Please try again." };
                    Stage3BLog.Error($"STAGE10A_LISTENER state=failed; error={SafeError(error)}; url=cleared");
                    _ = Task.Run(() => StopNetworkObjects(clearStatus: false));
                    break;
                case NWListenerState.Cancelled:
                    Stage3BLog.Info("STAGE10A_LISTENER state=cancelled; url=cleared");
                    break;
            }
        }
    }

    private void AcceptConnection(NWConnection connection)
    {
        lock (gate)
        {
            if (listener == null || protocol == null || status.Phase != "ready" || !connectionGate.TryEnter())
            {
                connection.Cancel();
                connection.Dispose();
                return;
            }
            TouchInactivityTimer();
            ConnectionState state = new(connection, Stage10AHttpProtocol.RequestLifetime, ConnectionTimedOut);
            connections.Add(connection, state);
            connection.SetQueue(queue);
            connection.SetStateChangeHandler((value, error) =>
            {
                if (value is NWConnectionState.Failed or NWConnectionState.Cancelled)
                {
                    if (error != null) Stage3BLog.Warning($"STAGE10A_CONNECTION state={value}; error={SafeError(error)}");
                    CloseConnection(connection, $"state-{value.ToString().ToLowerInvariant()}");
                }
            });
            connection.Start();
            Stage3BLog.Info($"STAGE10A_CONNECTION result=accepted; active={connectionGate.Count}");
            Receive(connection);
        }
    }

    private void Receive(NWConnection connection)
    {
        uint maximumReceive;
        lock (gate)
        {
            if (!connections.TryGetValue(connection, out ConnectionState? pending)) return;
            maximumReceive = checked((uint)Math.Max(1, pending.NextReceiveMaximum));
        }
        connection.Receive(1, maximumReceive,
            (data, size, _, complete, error) =>
            {
                try
                {
                    lock (gate)
                    {
                        if (!connections.TryGetValue(connection, out ConnectionState? state)) return;
                        if (error != null) { CloseConnection(connection, "receive-error"); return; }
                        if (data != IntPtr.Zero && size > 0)
                        {
                            int count = checked((int)size);
                            byte[] chunk = new byte[count];
                            Marshal.Copy(data, chunk, 0, count);
                            state.Buffer.Write(chunk, 0, chunk.Length);
                        }
                        byte[] buffered = state.Buffer.ToArray();
                        Stage10ARequestProgress progress = Stage10AHttpProtocol.InspectRequestProgress(buffered);
                        if (progress.Rejection != null)
                        {
                            SendResponse(connection, progress.Rejection, headOnly: false);
                            return;
                        }
                        if (!progress.Complete && !complete)
                        {
                            int ceiling = progress.ExpectedBytes > 0
                                ? progress.ExpectedBytes
                                : Stage10AHttpProtocol.MaximumHeaderBytes;
                            state.NextReceiveMaximum = Math.Max(1, ceiling - buffered.Length);
                            Receive(connection);
                            return;
                        }
                        Stage10AHttpProtocol? current = protocol;
                        if (current == null) { CloseConnection(connection, "server-stopped"); return; }
                        bool headOnly = buffered.AsSpan().StartsWith("HEAD "u8);
                        Stage10AHttpResponse response = current.Handle(buffered);
                        if (current.RestartRequired)
                        {
                            restartRequired = true;
                            status = CopyStatus(status, restartRequired: true);
                        }
                        string payloadEvidence = response.Headers.TryGetValue("X-Celeste-Content-SHA256", out string? payloadHash) &&
                            response.Headers.TryGetValue("X-Celeste-Logical-Name", out string? logicalName)
                            ? $"; logical={logicalName}; download-sha256={payloadHash}"
                            : "";
                        string authEvidence = response.Headers.ContainsKey("X-Celeste-Authentication") ? "; auth=accepted" : "";
                        Stage3BLog.Info($"STAGE10A_HTTP status={response.StatusCode}; request-bytes={buffered.Length}; response-bytes={response.Body.Length}; authenticated-sessions={current.ActiveSessionCount}{authEvidence}{payloadEvidence}");
                        SendResponse(connection, response, headOnly);
                    }
                }
                catch (Exception exception)
                {
                    Stage3BLog.Error($"STAGE10A_CONNECTION result=failed; type={exception.GetType().Name}; message={exception.Message}");
                    CloseConnection(connection, "managed-exception");
                }
            });
    }

    private void SendResponse(NWConnection connection, Stage10AHttpResponse response, bool headOnly)
    {
        if (!connections.TryGetValue(connection, out ConnectionState? state)) return;
        // Physical acceptance normally observes this final-message completion.
        // Retain a bounded fallback so an abnormal native callback loss can
        // never occupy one of the four concurrent request slots forever. Five
        // seconds is ample for the bounded <= 256 KiB LAN payload.
        state.ResponsePending = true;
        state.Timer.Change(Stage10AConnectionPolicy.ResponseCloseLifetime, Timeout.InfiniteTimeSpan);
        byte[] encoded = response.Encode(headOnly);
        connection.Send(encoded, NWContentContext.FinalMessage, true, error =>
        {
            if (error != null) Stage3BLog.Warning($"STAGE10A_CONNECTION result=send-error; error={SafeError(error)}");
            CloseConnection(connection, error == null ? "send-complete" : "send-error");
        });
    }

    private void CloseConnection(NWConnection connection, string reason)
    {
        lock (gate)
        {
            // State-change and send callbacks can race. Only the callback that
            // owns the dictionary entry may cancel/dispose the native object.
            if (!connections.Remove(connection, out ConnectionState? state)) return;
            state.Dispose();
            connectionGate.Leave();
            Stage3BLog.Info($"STAGE10A_CONNECTION result=closed; reason={SanitizeReason(reason)}; active={connectionGate.Count}");
            try { connection.Cancel(); } catch { }
            connection.Dispose();
        }
    }

    private void ConnectionTimedOut(NWConnection connection)
    {
        bool responsePending;
        lock (gate) responsePending = connections.TryGetValue(connection, out ConnectionState? state) && state.ResponsePending;
        string reason = responsePending ? "response-close-fallback" : "request-timeout";
        Stage3BLog.Warning($"STAGE10A_CONNECTION result=timeout; phase={reason}");
        CloseConnection(connection, reason);
    }

    private TvOSSaveManagerDisplayState Status()
    {
        lock (gate) return CopyStatus(status, restartRequired);
    }

    private void Stop(string reason)
    {
        lock (gate)
        {
            if (disposed && reason != "dispose") return;
            bool wasActive = listener != null || protocol != null || status.Phase is "starting" or "ready";
            StopNetworkObjects(clearStatus: true);
            if (wasActive) Stage3BLog.Info($"STAGE10A_STOP reason={SanitizeReason(reason)}; listener=false; sessions=cleared; url=cleared; access-code=cleared");
        }
    }

    private void StopNetworkObjects(bool clearStatus)
    {
        lock (gate)
        {
            SetIdleTimerSuppressed(false);
            inactivityTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            foreach (NWConnection connection in connections.Keys.ToArray()) CloseConnection(connection, "server-stop");
            connections.Clear();
            connectionGate.Stop();
            protocol?.Stop();
            protocol = null;
            if (listener != null)
            {
                try { listener.Cancel(); } catch { }
                listener.Dispose();
                listener = null;
            }
            if (clearStatus)
            {
                status = restartRequired
                    ? new TvOSSaveManagerDisplayState { Phase = "restart-required", RestartRequired = true }
                    : new TvOSSaveManagerDisplayState { Phase = "stopped" };
            }
        }
    }

    private void Observe(NSString notification, string reason) =>
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, _ => Stop(reason)));

    private void TouchInactivityTimer() => inactivityTimer.Change(ManagerInactivityLifetime, Timeout.InfiniteTimeSpan);

    // Save Manager is a foreground maintenance screen. Prevent the Apple TV
    // screensaver from resigning the app while somebody is reading the code or
    // downloading files, then restore normal tvOS idle behaviour on every stop
    // path. Real resign/background notifications still stop the listener.
    private void SetIdleTimerSuppressed(bool suppressed)
    {
        if (idleTimerSuppressed == suppressed) return;
        idleTimerSuppressed = suppressed;
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
            UIApplication.SharedApplication.IdleTimerDisabled = suppressed);
        Stage3BLog.Info($"STAGE10A_IDLE_TIMER suppressed={suppressed.ToString().ToLowerInvariant()}");
    }

    internal static IReadOnlyList<string> DiscoverLanAddresses()
    {
        List<(int Rank, string Address)> values = new();
        IntPtr head = IntPtr.Zero;
        if (GetIfAddrs(out head) != 0 || head == IntPtr.Zero)
            throw new InvalidOperationException($"getifaddrs failed with errno {Marshal.GetLastPInvokeError()}.");
        try
        {
            HashSet<IntPtr> visited = new();
            for (IntPtr current = head; current != IntPtr.Zero && visited.Add(current) && visited.Count <= 256;)
            {
                IfAddrs item = Marshal.PtrToStructure<IfAddrs>(current);
                current = item.Next;
                if ((item.Flags & InterfaceFlagUp) == 0 || item.Name == IntPtr.Zero || item.Address == IntPtr.Zero) continue;
                string? name = Marshal.PtrToStringUTF8(item.Name);
                if (string.IsNullOrWhiteSpace(name) || Stage10ALanAddressPolicy.IsExcludedInterface(name)) continue;
                IPAddress? address = Stage10ALanAddressPolicy.ReadDarwinSockAddr(item.Address);
                if (address == null) continue;
                int rank = Stage10ALanAddressPolicy.InterfaceRank(name);
                if (address.AddressFamily == AddressFamily.InterNetwork && Stage10ALanAddressPolicy.IsUsableIpv4(address)) values.Add((rank, address.ToString()));
                else if (address.AddressFamily == AddressFamily.InterNetworkV6 && Stage10ALanAddressPolicy.IsUsableIpv6(address)) values.Add((rank + 10, address.ToString()));
            }
        }
        finally { FreeIfAddrs(head); }
        return values.OrderBy(value => value.Rank).ThenBy(value => value.Address, StringComparer.Ordinal)
            .Select(value => value.Address).Distinct(StringComparer.Ordinal).Take(3).ToArray();
    }

    private static string SafeError(NWError? error) => error == null ? "none" : $"domain={error.ErrorDomain}; code={error.ErrorCode}";
    private static string SanitizeReason(string value) => new(value.Where(character => char.IsAsciiLetterOrDigit(character) || character == '-').Take(48).ToArray());

    private const uint InterfaceFlagUp = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct IfAddrs
    {
        internal IntPtr Next;
        internal IntPtr Name;
        internal uint Flags;
        internal IntPtr Address;
        internal IntPtr Netmask;
        internal IntPtr DestinationAddress;
        internal IntPtr Data;
    }

    [DllImport("__Internal", EntryPoint = "getifaddrs", SetLastError = true)]
    private static extern int GetIfAddrs(out IntPtr addresses);

    [DllImport("__Internal", EntryPoint = "freeifaddrs")]
    private static extern void FreeIfAddrs(IntPtr addresses);
    private static TvOSSaveManagerDisplayState CopyStatus(TvOSSaveManagerDisplayState value, bool? restartRequired = null) => new()
    {
        Phase = value.Phase,
        Urls = value.Urls.ToArray(),
        AccessCode = value.AccessCode,
        Detail = value.Detail,
        RestartRequired = restartRequired ?? value.RestartRequired
    };

    private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(Stage10ASaveManager)); }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            Stop("dispose");
            disposed = true;
            TvOSSaveManagerHooks.Reset();
            foreach (NSObject observer in observers)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
                observer.Dispose();
            }
            observers.Clear();
            inactivityTimer.Dispose();
            queue.Dispose();
        }
    }

    private sealed class ConnectionState : IDisposable
    {
        internal MemoryStream Buffer { get; } = new();
        internal Timer Timer { get; }
        internal bool ResponsePending { get; set; }
        internal int NextReceiveMaximum { get; set; } = Stage10AHttpProtocol.MaximumHeaderBytes;

        internal ConnectionState(NWConnection connection, TimeSpan lifetime, Action<NWConnection> timeout) =>
            Timer = new Timer(_ => timeout(connection), null, lifetime, Timeout.InfiniteTimeSpan);

        public void Dispose()
        {
            Timer.Dispose();
            Buffer.Dispose();
        }
    }
}
#endif
