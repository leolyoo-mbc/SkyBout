using NUnit.Framework;
using SkyBout.Online;

public class RoomDisconnectTests
{
    [Test]
    public void HostEndClosesSessionOnceAndStopsGame()
    {
        var service = new FakeRoomService(); var game = new FakeRoomGame();
        var session = new FakeRoomSession();
        var c = new OnlineRoomController(service, game, new ManualRoomClock());
        c.Join("abc"); service.Result.SetResult(session);
        game.IsReady = true; game.ConnectedCount = 2; c.Tick();
        Assert.That(c.State, Is.EqualTo(RoomConnectionState.InRoom));
        session.End(); c.Tick(); session.End(); c.Tick();
        Assert.That(c.Failure, Is.EqualTo(RoomFailure.HostEnded));
        Assert.That(c.State, Is.EqualTo(RoomConnectionState.Failed));
        Assert.That(session.Closed, Is.EqualTo(1));
        Assert.That(game.Stopped, Is.EqualTo(1));
    }
}
