using System.Threading.Tasks;
using NUnit.Framework;
using SkyBout.Online;

public class RoomCleanupTests
{
    [Test]
    public void LateSuccessIsClosedWithoutStartingANewGame()
    {
        var service = new FakeRoomService(); var game = new FakeRoomGame(); var clock = new ManualRoomClock();
        var c = new OnlineRoomController(service, game, clock);
        c.Create(); clock.Now = 30; c.Tick();
        Assert.That(c.Failure, Is.EqualTo(RoomFailure.Timeout));
        Assert.That(c.NeedsCleanup, Is.True);
        Assert.That(c.Create(), Is.False);
        var session = new FakeRoomSession(); service.Result.SetResult(session); c.Tick(); c.Tick();
        Assert.That(session.Closed, Is.EqualTo(1));
        Assert.That(c.NeedsCleanup, Is.False);
        Assert.That(c.State, Is.EqualTo(RoomConnectionState.Failed));
        Assert.That(game.Prepared, Is.EqualTo(1));
    }

    [Test]
    public void StalledCleanupOffersOfflineAndBoundedRecovery()
    {
        var service = new FakeRoomService(); var game = new FakeRoomGame(); var clock = new ManualRoomClock();
        var cleanup = new TaskCompletionSource<bool>();
        var session = new FakeRoomSession { Cleanup = cleanup.Task };
        var c = new OnlineRoomController(service, game, clock);
        c.Create(); service.Result.SetResult(session); c.Tick(); clock.Now = 30; c.Tick();
        clock.Now = 60; c.Tick(); Assert.That(c.RecoveryRequired, Is.True);
        c.ReturnOffline(); Assert.That(c.State, Is.EqualTo(RoomConnectionState.Offline));
        Assert.That(c.Create(), Is.False);
        c.RetryCleanup(); clock.Now = 90; c.Tick(); Assert.That(c.RecoveryRequired, Is.True);
        cleanup.SetResult(true); c.Tick();
        Assert.That(c.NeedsCleanup, Is.False);
        Assert.That(c.Create(), Is.True);
    }
}
