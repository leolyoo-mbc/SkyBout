using NUnit.Framework;
using SkyBout.Online;

public class RoomFailureTests
{
    [Test]
    public void LastSlotIsReservedOnceAndReturnsAfterDisconnect()
    {
        var approval = new RoomConnectionApproval();
        Assert.That(approval.TryReserve(0, "host", out var host), Is.True);
        Assert.That(host, Is.Zero);
        Assert.That(approval.TryReserve(1, "guest", out var guest), Is.True);
        Assert.That(guest, Is.EqualTo(1));
        Assert.That(approval.TryReserve(2, "third", out _), Is.False);
        Assert.That(approval.Release(1), Is.EqualTo("guest"));
        Assert.That(approval.Release(1), Is.Null);
        Assert.That(approval.TryReserve(2, "third", out var replacement), Is.True);
        Assert.That(replacement, Is.EqualTo(1));
        Assert.That(approval.Count, Is.EqualTo(2));
    }

    [Test]
    public void OneMemberCannotReserveTwoCharacters()
    {
        var approval = new RoomConnectionApproval();
        Assert.That(approval.TryReserve(0, "host", out _), Is.True);
        Assert.That(approval.TryReserve(1, "host", out _), Is.False);
    }

    [Test]
    public void MissingAndClosedRoomsShareMessageButEmptyInputDoesNot()
    {
        Assert.That(RoomMessages.For(RoomFailure.NotFoundOrClosed), Is.EqualTo("코드가 잘못되었거나 종료된 방입니다"));
        Assert.That(RoomMessages.For(RoomFailure.InvalidCode), Is.Not.EqualTo(RoomMessages.For(RoomFailure.NotFoundOrClosed)));
    }

    [TestCase(RoomFailure.Full)]
    [TestCase(RoomFailure.Network)]
    [TestCase(RoomFailure.NotFoundOrClosed)]
    [TestCase(RoomFailure.VersionMismatch)]
    public void FailedJoinPreservesClassifiedReasonAndClearsRequest(RoomFailure failure)
    {
        var service = new FakeRoomService();
        var c = new OnlineRoomController(service, new FakeRoomGame(), new ManualRoomClock());
        c.Join("abc"); service.Result.SetException(new RoomException(failure)); c.Tick();
        Assert.That(c.Failure, Is.EqualTo(failure));
        Assert.That(c.State, Is.EqualTo(RoomConnectionState.Failed));
        Assert.That(c.NeedsCleanup, Is.False);
    }
}
