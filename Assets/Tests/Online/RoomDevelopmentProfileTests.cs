using NUnit.Framework;
using SkyBout.Online;

public class RoomDevelopmentProfileTests
{
    [Test]
    public void AdditionalEditorsUseDistinctDeterministicProfiles()
    {
        var main = RoomDevelopmentProfile.Resolve(new string[0], true, true);
        var second = RoomDevelopmentProfile.Resolve(new[] { "-name", "Player2" }, true, true);
        var third = RoomDevelopmentProfile.Resolve(new[] { "-name", "Player3" }, true, true);
        Assert.That(main, Is.EqualTo("room-editor"));
        Assert.That(second, Is.EqualTo("room-editor-player2"));
        Assert.That(third, Is.EqualTo("room-editor-player3"));
        Assert.That(second, Is.Not.EqualTo(main));
        Assert.That(second, Is.Not.EqualTo(third));
    }

    [Test]
    public void ExplicitDevelopmentProfileTakesPrecedence()
    {
        Assert.That(RoomDevelopmentProfile.Resolve(new[] { "-name", "Player2", "-roomProfile", "custom" }, true, true), Is.EqualTo("custom"));
    }

    [Test]
    public void ReleasePlayerDoesNotUseDevelopmentOverrides()
    {
        Assert.That(RoomDevelopmentProfile.Resolve(new[] { "-roomProfile", "custom" }, false, false), Is.Null);
        Assert.That(RoomDevelopmentProfile.Resolve(new string[0], false, true), Is.EqualTo("room-player"));
    }

    [Test]
    public void InstanceNamesStayWithinAuthenticationProfileConstraints()
    {
        var profile = RoomDevelopmentProfile.Resolve(new[] { "-name", "Player 2/invalid/name!abcdefghijklmnopqrstuvwxyz" }, true, true);
        Assert.That(profile.Length, Is.LessThanOrEqualTo(30));
        Assert.That(profile, Does.Match("^[a-z0-9_-]+$"));
        Assert.That(RoomDevelopmentProfile.Resolve(new[] { "-name" }, true, true), Is.EqualTo("room-editor"));
    }
}
