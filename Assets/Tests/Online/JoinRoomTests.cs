using NUnit.Framework;
using SkyBout.Online;

public class JoinRoomTests
{
    [Test]
    public void EmptyCodeDoesNotCallService()
    {
        var service = new FakeRoomService();
        var c = new OnlineRoomController(service, new FakeRoomGame(), new ManualRoomClock());
        Assert.That(c.Join("  "), Is.False);
        Assert.That(c.Failure, Is.EqualTo(RoomFailure.InvalidCode));
        Assert.That(service.Requests, Is.Zero);
    }

    [Test]
    public void CodeIsTrimmedAndUppercased()
    {
        var service = new FakeRoomService();
        var c = new OnlineRoomController(service, new FakeRoomGame(), new ManualRoomClock());
        Assert.That(c.Join(" aBc123 "), Is.True);
        Assert.That(service.LastCode, Is.EqualTo("ABC123"));
    }

    [Test]
    public void MembershipBlocksAnotherJoin()
    {
        var service = new FakeRoomService();
        var game = new FakeRoomGame { IsReady = true, ConnectedCount = 2 };
        var c = new OnlineRoomController(service, game, new ManualRoomClock());
        c.Join("abc"); service.Result.SetResult(new FakeRoomSession()); c.Tick();
        Assert.That(c.State, Is.EqualTo(RoomConnectionState.InRoom));
        Assert.That(c.Join("other"), Is.False);
        Assert.That(c.Create(), Is.False);
        Assert.That(service.Requests, Is.EqualTo(1));
    }
}
