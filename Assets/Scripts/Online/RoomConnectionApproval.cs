using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace SkyBout.Online
{
    public sealed class RoomConnectionApproval
    {
        public const string MapVersion = "islands-008";
        public const int ProtocolVersion = 1;
        [Serializable]
        public sealed class Payload
        {
            public int protocol = ProtocolVersion;
            public string map = MapVersion;
            public string player;
            public string ticket;
        }
        private readonly Dictionary<ulong, string> members = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();
        public int Count => slots.Count;
        public bool HasMember(string memberId) => members.ContainsValue(memberId);

        public bool TryReserve(ulong clientId, string memberId, out int slot)
        {
            slot = -1;
            if (slots.Count >= 2 || slots.ContainsKey(clientId) || members.ContainsValue(memberId)) return false;
            slot = slots.ContainsValue(0) ? 1 : 0;
            slots.Add(clientId, slot); members.Add(clientId, memberId);
            return true;
        }

        public string Release(ulong clientId)
        {
            members.TryGetValue(clientId, out var member);
            members.Remove(clientId); slots.Remove(clientId);
            return member;
        }
        public void Clear() { members.Clear(); slots.Clear(); }
        public static byte[] Encode(string id, string ticket) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Payload { player = id, ticket = ticket }));

        public async Task ApproveAsync(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response, OnlineRoomBootstrap bootstrap, UnityRoomService service)
        {
            response.Pending = true;
            try
            {
                if (request.Payload == null || request.Payload.Length > 1024) { Reject(response, RoomFailure.VersionMismatch); return; }
                var payload = JsonUtility.FromJson<Payload>(Encoding.UTF8.GetString(request.Payload));
                if (payload == null || payload.protocol != ProtocolVersion || payload.map != MapVersion)
                { Reject(response, RoomFailure.VersionMismatch); return; }
                var manager = bootstrap.Manager;
                var localHost = request.ClientNetworkId == NetworkManager.ServerClientId;
                if (!localHost)
                {
                    var session = service.Current;
                    if (session == null || !session.IsHost || payload.player == session.Host)
                    { Reject(response, RoomFailure.NotFoundOrClosed); return; }
                    // Refresh authenticated service membership, rather than trusting a claimed player ID.
                    await session.RefreshAsync();
                    if (!manager.IsListening || !manager.PendingClients.ContainsKey(request.ClientNetworkId) ||
                        !ReferenceEquals(session, service.Current))
                    { Reject(response, RoomFailure.HostEnded); return; }
                    var member = session.GetPlayer(payload.player ?? "");
                    if (member == null || !member.Properties.TryGetValue(UnityRoomService.TicketProperty, out var ticket) ||
                        string.IsNullOrEmpty(payload.ticket) || ticket.Value != payload.ticket)
                    { Reject(response, RoomFailure.NotFoundOrClosed); return; }
                }
                else if (payload.player != service.LocalPlayerId)
                { Reject(response, RoomFailure.NotFoundOrClosed); return; }
                if (!TryReserve(request.ClientNetworkId, payload.player, out var slot))
                { Reject(response, RoomFailure.Full); return; }
                response.Approved = true;
                response.CreatePlayerObject = true;
                response.Position = bootstrap.SpawnPosition(slot);
                response.Rotation = Quaternion.identity;
            }
            catch { Reject(response, RoomFailure.Network); }
            finally { response.Pending = false; }
        }

        private static void Reject(NetworkManager.ConnectionApprovalResponse response, RoomFailure reason)
        {
            response.Approved = false; response.Reason = reason.ToString();
        }
    }
}
