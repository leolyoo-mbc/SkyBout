using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace SkyBout.Online
{
    public sealed class UnityRoomService : IRoomService
    {
        public const string TicketProperty = "connection-ticket";
        public string LocalPlayerId => AuthenticationService.Instance.PlayerId;
        public string ConnectionTicket { get; private set; }
        public ISession Current { get; private set; }
        private readonly string profile;
        public UnityRoomService(string profile = null) => this.profile = profile;

        public async Task InitializeAsync(CancellationToken token)
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId)) throw new RoomException(RoomFailure.Configuration);
            token.ThrowIfCancellationRequested();
            var options = new InitializationOptions();
            if (!string.IsNullOrEmpty(profile)) options.SetProfile(profile);
            await UnityServices.InitializeAsync(options);
            token.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            token.ThrowIfCancellationRequested();
            ConnectionTicket = Guid.NewGuid().ToString("N");
        }

        private Dictionary<string, PlayerProperty> PlayerProperties() => new Dictionary<string, PlayerProperty>
        {
            { TicketProperty, new PlayerProperty(ConnectionTicket, VisibilityPropertyOptions.Member) }
        };

        public async Task<IRoomSession> CreateAsync(CancellationToken token)
        {
            try
            {
                var options = new SessionOptions { MaxPlayers = 2, IsPrivate = true, PlayerProperties = PlayerProperties() }.WithRelayNetwork();
                Current = await MultiplayerService.Instance.CreateSessionAsync(options, token);
                return new SessionHandle(this, Current, true);
            }
            catch (Exception error) { throw ConvertError(error); }
        }

        public async Task<IRoomSession> JoinAsync(string normalizedCode, CancellationToken token)
        {
            try
            {
                Current = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode,
                    new JoinSessionOptions { PlayerProperties = PlayerProperties() }, token);
                return new SessionHandle(this, Current, false);
            }
            catch (Exception error) { throw ConvertError(error); }
        }

        public static Exception ConvertError(Exception error)
        {
            if (error is RoomException || error is OperationCanceledException) return error;
            if (error is SessionException session)
            {
                if (session.Error == SessionError.SessionNotFound || session.Error == SessionError.SessionDeleted ||
                    session.Error == SessionError.InvalidParameter || session.Error == SessionError.InvalidSessionIdentifier)
                    return new RoomException(RoomFailure.NotFoundOrClosed);
                // MPS 2.4 converts LobbyFull to Unknown, retaining only the service detail string.
                if (session.Message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new RoomException(RoomFailure.Full);
                if (session.Error == SessionError.NetworkManagerNotInitialized || session.Error == SessionError.MissingAssembly ||
                    session.Error == SessionError.TransportComponentMissing)
                    return new RoomException(RoomFailure.Configuration);
            }
            // Do not echo service details, codes, credentials or tokens into UI/logs.
            return new RoomException(RoomFailure.Network);
        }

        private sealed class SessionHandle : IRoomSession
        {
            private readonly UnityRoomService owner;
            private readonly ISession native;
            private bool closed;
            private bool closing;
            public string Id => native.Id;
            public string Code => native.Code;
            public bool IsHost { get; }
            public event Action Ended;

            public SessionHandle(UnityRoomService owner, ISession native, bool originalHost)
            {
                this.owner = owner; this.native = native; IsHost = originalHost;
                native.Deleted += Deleted;
                native.RemovedFromSession += Removed;
                native.SessionHostChanged += HostChanged;
            }

            private void Deleted() { closed = true; Detach(); if (!closing) Ended?.Invoke(); }
            private void Removed() { closed = true; Detach(); if (!closing) Ended?.Invoke(); }
            private void HostChanged(string id) { if (!closing) Ended?.Invoke(); }
            private void Detach()
            {
                native.Deleted -= Deleted; native.RemovedFromSession -= Removed; native.SessionHostChanged -= HostChanged;
                if (ReferenceEquals(owner.Current, native)) owner.Current = null;
            }

            public async Task CloseAsync()
            {
                if (closed) { Detach(); return; }
                closing = true;
                try
                {
                    // A guest elected service host after host loss must delete the orphaned room as well.
                    if (native.IsHost) await native.AsHost().DeleteAsync();
                    else if (native.IsMember) await native.LeaveAsync();
                    closed = true; Detach();
                }
                catch (SessionException error) when (error.Error == SessionError.SessionDeleted || error.Error == SessionError.SessionNotFound)
                {
                    closed = true; Detach();
                }
                finally { closing = false; }
            }

            public async Task RemovePlayerAsync(string playerId)
            {
                if (!closed && native.IsHost && native.HasPlayer(playerId) && playerId != native.Host)
                    await native.AsHost().RemovePlayerAsync(playerId);
            }
        }
    }
}
