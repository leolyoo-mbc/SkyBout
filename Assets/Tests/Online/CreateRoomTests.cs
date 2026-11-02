using System.Threading.Tasks;
using NUnit.Framework;
using SkyBout.Online;

public class CreateRoomTests
{
    [Test]
    public void SingleRequestWaitsForGameReadiness()
    {
        var service = new FakeRoomService(); var game = new FakeRoomGame(); var clock = new ManualRoomClock();
        var controller = new OnlineRoomController(service, game, clock);
        Assert.That(controller.Create(), Is.True);
        Assert.That(controller.Create(), Is.False);
        Assert.That(controller.Join("ABC"), Is.False);
        service.Result.SetResult(new FakeRoomSession { IsHost = true });
        controller.Tick();
        Assert.That(controller.State, Is.Not.EqualTo(RoomConnectionState.InRoom));
        game.IsReady = true; game.ConnectedCount = 1;
        controller.Tick();
        Assert.That(controller.State, Is.EqualTo(RoomConnectionState.InRoom));
        Assert.That(controller.Code, Is.EqualTo("ABC123"));
        Assert.That(service.Requests, Is.EqualTo(1));
    }

    [Test]
    public void DeadlineIncludesInitialization()
    {
        var service = new FakeRoomService { Initialization = new TaskCompletionSource<bool>().Task };
        var clock = new ManualRoomClock();
        var controller = new OnlineRoomController(service, new FakeRoomGame(), clock);
        controller.Create(); clock.Now = 30; controller.Tick();
        Assert.That(controller.State, Is.EqualTo(RoomConnectionState.Failed));
        Assert.That(controller.Failure, Is.EqualTo(RoomFailure.Timeout));
        Assert.That(service.Requests, Is.Zero);
    }
}
