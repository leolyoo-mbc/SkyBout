namespace SkyBout.Online
{
    public enum RoomConnectionState { Offline, Initializing, Creating, Joining, InRoom, Leaving, Failed }
    public enum RoomFailure { None, InvalidCode, NotFoundOrClosed, Full, Network, Timeout, Configuration, VersionMismatch, HostEnded }

    public static class RoomMessages
    {
        public static string For(RoomFailure failure)
        {
            switch (failure)
            {
                case RoomFailure.InvalidCode: return "참가 코드를 입력해 주세요.";
                case RoomFailure.NotFoundOrClosed: return "코드가 잘못되었거나 종료된 방입니다";
                case RoomFailure.Full: return "방이 가득 찼습니다. 다른 방에 참가해 주세요.";
                case RoomFailure.Timeout: return "연결 시간이 초과되었습니다. 인터넷 연결을 확인해 주세요.";
                case RoomFailure.Configuration: return "온라인 서비스 설정을 확인해 주세요.";
                case RoomFailure.VersionMismatch: return "게임 버전이 다릅니다. 같은 빌드를 사용해 주세요.";
                case RoomFailure.HostEnded: return "방장이 나갔거나 연결이 끊겨 방이 종료되었습니다.";
                case RoomFailure.Network: return "통신에 실패했습니다. 인터넷 연결을 확인해 주세요.";
                default: return "";
            }
        }
    }
}
