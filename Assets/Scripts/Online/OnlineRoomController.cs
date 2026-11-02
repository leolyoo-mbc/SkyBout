using System;
using System.Threading;
using System.Threading.Tasks;

namespace SkyBout.Online
{
    public sealed class OnlineRoomController
    {
        public RoomConnectionState State { get; private set; }
        public RoomFailure Failure { get; private set; }
        public string Code { get; private set; } = "";
        public bool NeedsCleanup { get; private set; }
        public bool RecoveryRequired { get; private set; }
        public bool IsHost => session != null && session.IsHost;
        public int ConnectedCount => State == RoomConnectionState.InRoom ? game.ConnectedCount : 0;
        public ulong OperationGeneration { get; private set; }
        private readonly IRoomService service;
        private readonly IRoomGame game;
        private readonly IRoomClock clock;
        private CancellationTokenSource cancellation;
        private Task<IRoomSession> operation;
        private Task cleanup;
        private IRoomSession session;
        private bool active;
        private double deadline, cleanupDeadline;

        public OnlineRoomController(IRoomService service, IRoomGame game, IRoomClock clock)
        {
            this.service = service; this.game = game; this.clock = clock;
        }

        public bool Create() => Begin(null);
        public bool Join(string code)
        {
            if (!CanBegin()) return false;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0)
            {
                Failure = RoomFailure.InvalidCode;
                State = RoomConnectionState.Failed;
                return false;
            }
            return Begin(code);
        }

        private bool CanBegin() => !active && !NeedsCleanup &&
            (State == RoomConnectionState.Offline || State == RoomConnectionState.Failed);

        private bool Begin(string code)
        {
            if (!CanBegin()) return false;
            OperationGeneration++;
            active = true; deadline = clock.Now + 30;
            Failure = RoomFailure.None; Code = ""; RecoveryRequired = false;
            State = RoomConnectionState.Initializing;
            cancellation?.Dispose();
            cancellation = new CancellationTokenSource();
            operation = ConnectAsync(code, OperationGeneration, cancellation.Token);
            return true;
        }

        private async Task<IRoomSession> ConnectAsync(string code, ulong generation, CancellationToken token)
        {
            await service.InitializeAsync(token);
            token.ThrowIfCancellationRequested();
            if (generation != OperationGeneration) throw new OperationCanceledException();
            game.Prepare();
            State = code == null ? RoomConnectionState.Creating : RoomConnectionState.Joining;
            return code == null ? await service.CreateAsync(token) : await service.JoinAsync(code, token);
        }

        public void Tick()
        {
            if (active && clock.Now >= deadline) Fail(RoomFailure.Timeout);
            if (operation != null && operation.IsCompleted)
            {
                var completed = operation; operation = null;
                try
                {
                    session = completed.GetAwaiter().GetResult();
                    if (active) session.Ended += SessionEnded;
                }
                catch (Exception error)
                {
                    if (active) Fail(Classify(error));
                }
            }
            if (active && session != null && game.IsReady && game.ConnectedCount >= 1 && game.ConnectedCount <= 2)
            {
                active = false;
                Code = session.Code;
                State = RoomConnectionState.InRoom;
            }
            if (!NeedsCleanup) return;
            if (operation == null && session != null && cleanup == null)
            {
                try { cleanup = session.CloseAsync(); }
                catch (Exception error) { cleanup = Task.FromException(error); }
            }
            if (cleanup != null && cleanup.IsCompleted)
            {
                var completed = cleanup;
                try
                {
                    completed.GetAwaiter().GetResult();
                    cleanup = null; session = null;
                }
                catch
                {
                    // Keep the failed task/handle until explicit recovery, rather than retrying every frame.
                    RecoveryRequired = true;
                }
            }
            if (operation == null && session == null && cleanup == null)
            {
                NeedsCleanup = false; RecoveryRequired = false;
                if (State == RoomConnectionState.Leaving) State = RoomConnectionState.Offline;
            }
            else if (clock.Now >= cleanupDeadline) RecoveryRequired = true;
        }

        private void Fail(RoomFailure failure)
        {
            Failure = failure;
            State = RoomConnectionState.Failed;
            BeginCleanup();
        }

        public void NotifyDisconnected(RoomFailure reason = RoomFailure.HostEnded) => Fail(reason);
        private void SessionEnded() => Fail(RoomFailure.HostEnded);

        private void BeginCleanup()
        {
            active = false;
            OperationGeneration++;
            cancellation?.Cancel();
            if (session != null) session.Ended -= SessionEnded;
            Code = "";
            if (!NeedsCleanup) cleanupDeadline = clock.Now + 30;
            NeedsCleanup = true;
            game.Stop();
        }

        public void Leave()
        {
            if (NeedsCleanup) return;
            State = RoomConnectionState.Leaving;
            BeginCleanup();
        }

        public void ReturnOffline()
        {
            if (active || State == RoomConnectionState.InRoom) BeginCleanup();
            State = RoomConnectionState.Offline;
            game.Stop();
        }

        public void RetryCleanup()
        {
            if (!NeedsCleanup) return;
            RecoveryRequired = false;
            cleanupDeadline = clock.Now + 30;
            if (cleanup != null && cleanup.IsCompleted && (cleanup.IsFaulted || cleanup.IsCanceled)) cleanup = null;
        }

        private static RoomFailure Classify(Exception error) => error is RoomException room ? room.Failure :
            error is OperationCanceledException ? RoomFailure.Timeout : RoomFailure.Network;
    }
}
