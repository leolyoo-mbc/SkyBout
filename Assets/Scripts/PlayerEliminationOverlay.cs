using UnityEngine;

// shortcut: 로컬 1인 프로토타입 안내. 실제 HUD·다국어 도입 시 UI 체계로 이전한다.
internal sealed class PlayerEliminationOverlay
{
    private GUIStyle textStyle;

    // Unity OnGUI 안에서만 호출한다.
    internal void Draw()
    {
        if (textStyle == null)
            textStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        textStyle.fontSize = Mathf.Clamp(Screen.width / 25, 18, 32);
        float panelWidth = Mathf.Min(400f, Screen.width * 0.9f);
        var panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - 96f) * 0.5f, panelWidth, 96f);
        Color previousGuiColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.95f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(panelRect, "ELIMINATED\nR: 재시작", textStyle);
        GUI.color = previousGuiColor;
    }
}
