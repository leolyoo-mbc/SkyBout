using UnityEngine;

namespace SkyBout.Online
{
    // Prototype UI follows the existing elimination overlay; no SDK calls are made from OnGUI.
    [RequireComponent(typeof(OnlineRoomBootstrap))]
    public sealed class OnlineRoomPanel : MonoBehaviour
    {
        private OnlineRoomBootstrap bootstrap;
        private string code = "";
        private bool lastInRoom;
        private void Awake() => bootstrap = GetComponent<OnlineRoomBootstrap>();
        private void Update()
        {
            var inRoom = bootstrap.Controller.State == RoomConnectionState.InRoom;
            if (inRoom && !lastInRoom) bootstrap.SetPanelOpen(false);
            if (!inRoom && lastInRoom) bootstrap.SetPanelOpen(true);
            lastInRoom = inRoom;
        }
        private void OnGUI()
        {
            if (bootstrap.Controller == null) return;
            var controller = bootstrap.Controller;
            if (!bootstrap.PanelOpen)
            {
                if (GUI.Button(new Rect(12, 12, 220, 36), controller.State == RoomConnectionState.InRoom
                    ? $"온라인 {controller.ConnectedCount}/2 · 메뉴" : "온라인 방")) bootstrap.SetPanelOpen(true);
                return;
            }
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(420, Screen.width - 24), Mathf.Min(400, Screen.height - 24)), GUI.skin.box);
            GUILayout.Label("SkyBout · 온라인 2인 방");
            GUILayout.Label(controller.State.ToString());
            if (controller.State == RoomConnectionState.InRoom)
            {
                GUILayout.Label($"{(controller.IsHost ? "방장" : "참가자")} · {controller.ConnectedCount}/2");
                GUILayout.Label("참가 코드: " + controller.Code);
                if (!string.IsNullOrEmpty(bootstrap.CleanupNotice)) GUILayout.Label(bootstrap.CleanupNotice);
                if (GUILayout.Button("코드 복사")) GUIUtility.systemCopyBuffer = controller.Code;
                if (GUILayout.Button("플레이로 돌아가기")) bootstrap.SetPanelOpen(false);
                if (GUILayout.Button("방 나가기")) controller.Leave();
            }
            else
            {
                GUILayout.Label(RoomMessages.For(controller.Failure));
                if (controller.NeedsCleanup)
                {
                    GUILayout.Label(controller.RecoveryRequired ? "연결을 확인하고 복구를 다시 시도해 주세요." : "이전 연결을 정리 중입니다.");
                    if (controller.RecoveryRequired && GUILayout.Button("복구 재시도")) controller.RetryCleanup();
                }
                else
                {
                    var canBegin = controller.State == RoomConnectionState.Offline || controller.State == RoomConnectionState.Failed;
                    GUI.enabled = canBegin;
                    if (GUILayout.Button("방 만들기")) controller.Create();
                    code = GUILayout.TextField(code, 64);
                    if (GUILayout.Button("코드로 참가")) controller.Join(code);
                    GUI.enabled = true;
                }
                if (GUILayout.Button("오프라인으로 돌아가기"))
                {
                    controller.ReturnOffline(); bootstrap.SetPanelOpen(false);
                }
            }
            GUILayout.EndArea();
        }
    }
}
