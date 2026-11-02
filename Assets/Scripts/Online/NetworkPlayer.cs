using Unity.Netcode;
using UnityEngine;

namespace SkyBout.Online
{
    [RequireComponent(typeof(NetworkObject), typeof(PlayerMovement))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        private readonly NetworkVariable<int> motion = new NetworkVariable<int>(1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<uint> restart = new NetworkVariable<uint>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private PlayerMovement movement;
        private OwnerNetworkTransform networkTransform;
        private bool bound;

        public override void OnNetworkSpawn()
        {
            movement = GetComponent<PlayerMovement>();
            networkTransform = GetComponent<OwnerNetworkTransform>();
            motion.OnValueChanged += MotionChanged;
            if (IsOwner)
            {
                var bootstrap = OnlineRoomBootstrap.For(NetworkManager);
                if (bootstrap == null) return;
                movement.BindLocalControl(bootstrap.CameraFollow, transform.position, transform.rotation);
                movement.Restarted += Restarted;
                bound = true;
                motion.Value = movement.MotionState;
            }
            else
            {
                movement.UnbindLocalControl();
                movement.ApplyRemoteState(motion.Value);
            }
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", IsOwner ? new Color(0.2f, 0.75f, 1f) : new Color(1f, 0.5f, 0.2f));
                renderer.SetPropertyBlock(properties);
            }
        }

        private void FixedUpdate()
        {
            if (IsSpawned && IsOwner && bound && motion.Value != movement.MotionState)
                motion.Value = movement.MotionState;
        }

        private void MotionChanged(int previous, int current)
        {
            if (!IsOwner) movement.ApplyRemoteState(current);
        }

        private void Restarted()
        {
            restart.Value++;
            motion.Value = movement.MotionState;
            networkTransform.Teleport(transform.position, transform.rotation, transform.localScale);
        }

        public override void OnNetworkDespawn()
        {
            motion.OnValueChanged -= MotionChanged;
            if (movement != null)
            {
                movement.Restarted -= Restarted;
                movement.UnbindLocalControl();
            }
            bound = false;
        }
    }
}
