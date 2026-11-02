using System.Linq;

namespace SkyBout.Online
{
    public static class RoomDevelopmentProfile
    {
        public static string Resolve(string[] args, bool isEditor, bool isDebugBuild)
        {
            if (!isEditor && !isDebugBuild) return null;
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-roomProfile") return args[i + 1];
            if (!isEditor) return "room-player";
            // Multiplayer Play Mode 2.x launches additional Editors with -name Player2, etc.
            // Keep profiles deterministic and distinct without a runtime dependency on Editor APIs.
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] != "-name") continue;
                var name = new string(args[i + 1].Where(c =>
                    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '-' || c == '_').Take(16).ToArray()).ToLowerInvariant();
                if (name.Length > 0) return "room-editor-" + name;
            }
            return "room-editor";
        }
    }
}
