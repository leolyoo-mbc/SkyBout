using System;
using System.Threading;
using System.Threading.Tasks;
using SkyBout.Online;

public sealed class FakeRoomService : IRoomService
{
    public string LocalPlayerId => "test-player";
    public int Requests;
    public string LastCode;
    public Task Initialization = Task.CompletedTask;
    public readonly TaskCompletionSource<IRoomSession> Result = new TaskCompletionSource<IRoomSession>();
    public Task InitializeAsync(CancellationToken cancellation) => Initialization;
    public Task<IRoomSession> CreateAsync(CancellationToken cancellation) { Requests++; return Result.Task; }
    public Task<IRoomSession> JoinAsync(string code, CancellationToken cancellation) { LastCode = code; Requests++; return Result.Task; }
}

public sealed class FakeRoomSession : IRoomSession
{
    public string Id => "session";
    public string Code => "ABC123";
    public bool IsHost { get; set; }
    public int Closed;
    public Task Cleanup = Task.CompletedTask;
    public event Action Ended;
    public void End() => Ended?.Invoke();
    public Task CloseAsync() { Closed++; return Cleanup; }
    public Task RemovePlayerAsync(string playerId) => Task.CompletedTask;
}

public sealed class FakeRoomGame : IRoomGame
{
    public bool IsReady { get; set; }
    public int ConnectedCount { get; set; }
    public int Prepared, Stopped;
    public void Prepare() => Prepared++;
    public void Stop() { Stopped++; IsReady = false; ConnectedCount = 0; }
}
