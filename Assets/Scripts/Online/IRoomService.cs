using System;
using System.Threading;
using System.Threading.Tasks;

namespace SkyBout.Online
{
    public sealed class RoomException : Exception
    {
        public RoomFailure Failure { get; }
        public RoomException(RoomFailure failure) : base(RoomMessages.For(failure)) => Failure = failure;
    }

    public interface IRoomSession
    {
        string Id { get; }
        string Code { get; }
        bool IsHost { get; }
        event Action Ended;
        Task CloseAsync();
        Task RemovePlayerAsync(string playerId);
    }

    public interface IRoomService
    {
        string LocalPlayerId { get; }
        Task InitializeAsync(CancellationToken cancellation);
        Task<IRoomSession> CreateAsync(CancellationToken cancellation);
        Task<IRoomSession> JoinAsync(string normalizedCode, CancellationToken cancellation);
    }

    public interface IRoomClock { double Now { get; } }

    // Sessions starts NGO during Create/Join. Prepare must happen before those calls.
    public interface IRoomGame
    {
        bool IsReady { get; }
        int ConnectedCount { get; }
        void Prepare();
        void Stop();
    }
}
