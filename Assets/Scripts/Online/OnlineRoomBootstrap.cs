using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SkyBout.Online
{
    public sealed class OnlineRoomBootstrap : MonoBehaviour, IRoomGame, IRoomClock
    {
        [SerializeField] private PlayerMovement offlinePlayer;
        [SerializeField] private TopDownCameraFollow cameraFollow;
        [SerializeField] private NetworkManager manager;
        [SerializeField] private Transform hostSpawn;
        [SerializeField] private Transform guestSpawn;
        public NetworkManager Manager => manager;
        public TopDownCameraFollow CameraFollow => cameraFollow;
        public OnlineRoomController Controller { get; private set; }
        public double Now => Time.realtimeSinceStartupAsDouble;
        public bool IsReady => prepared && manager != null && manager.IsConnectedClient &&
            manager.LocalClient?.PlayerObject != null && manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>().HasLocalControl;
        public int ConnectedCount => manager == null || !manager.IsListening ? 0 :
            manager.SpawnManager.SpawnedObjects.Values.Count(value => value.IsPlayerObject);
        private static readonly Dictionary<NetworkManager, OnlineRoomBootstrap> instances = new Dictionary<NetworkManager, OnlineRoomBootstrap>();
        public static OnlineRoomBootstrap For(NetworkManager manager) => instances.TryGetValue(manager, out var value) ? value : null;
        private UnityRoomService service;
        private readonly RoomConnectionApproval approval = new RoomConnectionApproval();
        private readonly Dictionary<string, Unity.Services.Multiplayer.ISession> pendingRemovals = new Dictionary<string, Unity.Services.Multiplayer.ISession>();
        private readonly HashSet<string> removing = new HashSet<string>();
        private double nextRemovalAttempt;
        public string CleanupNotice => pendingRemovals.Count == 0 ? "" : "퇴장한 참가자의 연결을 정리 중입니다. 연결이 복구되면 자동 재시도합니다.";
        private bool prepared, stopping, restoring;
        public bool PanelOpen { get; private set; }

        private void Awake()
        {
            service = new UnityRoomService(DevelopmentProfile());
            Controller = new OnlineRoomController(service, this, this);
            if (manager != null)
            {
                instances[manager] = this;
                manager.ConnectionApprovalCallback += Approve;
                manager.OnClientDisconnectCallback += Disconnected;
                var transport = manager.GetComponent<UnityTransport>();
                transport.DisconnectTimeoutMS = 10000;
                manager.NetworkConfig.ClientConnectionBufferTimeout = 20;
            }
        }

        public Vector3 SpawnPosition(int slot) => (slot == 0 ? hostSpawn : guestSpawn).position;

        private static string DevelopmentProfile()
        {
            return RoomDevelopmentProfile.Resolve(Environment.GetCommandLineArgs(), Application.isEditor, Debug.isDebugBuild);
        }

        private void Update()
        {
            Controller.Tick();
            if (Now >= nextRemovalAttempt && pendingRemovals.Count > 0)
            {
                nextRemovalAttempt = Now + 5;
                foreach (var entry in pendingRemovals.ToArray())
                {
                    if (!ReferenceEquals(entry.Value, service.Current)) pendingRemovals.Remove(entry.Key);
                    else if (!removing.Contains(entry.Key)) _ = RemoveMemberAsync(entry.Key, entry.Value);
                }
            }
            if (restoring && (manager == null || (!manager.IsListening && !manager.ShutdownInProgress)))
            {
                restoring = false; stopping = false;
                if (offlinePlayer != null)
                {
                    offlinePlayer.gameObject.SetActive(true);
                    cameraFollow.BindTarget(offlinePlayer.transform);
                    offlinePlayer.SetControlSuspended(PanelOpen);
                }
            }
        }

        public void SetPanelOpen(bool value)
        {
            PanelOpen = value;
            if (offlinePlayer != null && offlinePlayer.gameObject.activeSelf) offlinePlayer.SetControlSuspended(value);
            if (manager != null && manager.IsConnectedClient && manager.LocalClient?.PlayerObject != null)
                manager.LocalClient.PlayerObject.GetComponent<PlayerMovement>().SetControlSuspended(value);
        }

        public void Prepare()
        {
            if (manager == null || hostSpawn == null || guestSpawn == null || manager.NetworkConfig.PlayerPrefab == null)
                throw new RoomException(RoomFailure.Configuration);
            approval.Clear(); stopping = false; prepared = true;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.ConnectionData = RoomConnectionApproval.Encode(service.LocalPlayerId, service.ConnectionTicket);
            cameraFollow.ClearTarget();
            offlinePlayer.gameObject.SetActive(false);
        }

        public void Stop()
        {
            if (!prepared && !restoring) return;
            prepared = false; stopping = true; restoring = true;
            cameraFollow.ClearTarget();
            if (manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (!prepared) { response.Approved = false; response.Reason = RoomFailure.HostEnded.ToString(); return; }
            _ = approval.ApproveAsync(request, response, this, service);
        }

        private void Disconnected(ulong clientId)
        {
            var member = approval.Release(clientId);
            if (stopping || !prepared) return;
            if (manager.IsServer)
            {
                if (!string.IsNullOrEmpty(member) && member != service.LocalPlayerId)
                {
                    var session = service.Current;
                    if (session != null)
                    {
                        pendingRemovals[member] = session;
                        _ = RemoveMemberAsync(member, session);
                    }
                }
            }
            else if (clientId == manager.LocalClientId)
            {
                var reason = Enum.TryParse<RoomFailure>(manager.DisconnectReason, out var rejection) && rejection != RoomFailure.None
                    ? rejection : RoomFailure.HostEnded;
                Controller.NotifyDisconnected(reason);
            }
        }

        private async Task RemoveMemberAsync(string member, Unity.Services.Multiplayer.ISession session)
        {
            if (!removing.Add(member)) return;
            try
            {
                if (!ReferenceEquals(session, service.Current)) return;
                if (approval.HasMember(member))
                {
                    pendingRemovals.Remove(member);
                    return;
                }
                if (session.IsHost && session.HasPlayer(member))
                    await session.AsHost().RemovePlayerAsync(member);
                if (pendingRemovals.TryGetValue(member, out var queued) && ReferenceEquals(queued, session))
                    pendingRemovals.Remove(member);
            }
            catch { /* Keep the handle for retry, scoped to this session; surface it in the host panel. */ }
            finally { removing.Remove(member); }
        }

        private void OnDestroy()
        {
            Controller?.ReturnOffline();
            Controller?.Tick(); // Start owned session cleanup even when this scene is being unloaded.
            if (manager != null)
            {
                manager.ConnectionApprovalCallback -= Approve;
                manager.OnClientDisconnectCallback -= Disconnected;
                instances.Remove(manager);
            }
        }
    }
}
