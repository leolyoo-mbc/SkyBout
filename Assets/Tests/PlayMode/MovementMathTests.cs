using NUnit.Framework;
using UnityEngine;

public class MovementMathTests
{
    private const float DeltaTime = 0.02f;

    [Test]
    public void StartingMovementUsesAccelerationAndPreservesVerticalSpeed()
    {
        Vector3 next = Calculate(new Vector3(0f, -3f, 0f), Vector2.up);
        Assert.That(next.x, Is.Zero);
        Assert.That(next.z, Is.EqualTo(10f * DeltaTime).Within(0.0001f));
        Assert.That(next.y, Is.EqualTo(-3f));
    }

    [Test]
    public void ReleaseAndExcessSpeedUseDeceleration()
    {
        Vector3 stopping = Calculate(new Vector3(0f, 3f, 2f), Vector2.zero);
        Vector3 slowing = Calculate(new Vector3(0f, -3f, 10f), Vector2.up);
        Assert.That(stopping.z, Is.EqualTo(2f - 50f * DeltaTime).Within(0.0001f));
        Assert.That(slowing.z, Is.EqualTo(10f - 50f * DeltaTime).Within(0.0001f));
        Assert.That(stopping.y, Is.EqualTo(3f));
        Assert.That(slowing.y, Is.EqualTo(-3f));
    }

    [Test]
    public void DirectionChangeUsesTurnAcceleration()
    {
        Vector3 velocity = new Vector3(0f, 4f, 3f);
        Vector3 next = Calculate(velocity, Vector2.right);
        Vector3 expected = Vector3.MoveTowards(new Vector3(0f, 0f, 3f), Vector3.right * 8f, 30f * DeltaTime);
        Assert.That(Vector3.Distance(next, new Vector3(expected.x, 4f, expected.z)), Is.LessThan(0.0001f));
    }

    [Test]
    public void MovementDoesNotOvershootTargetSpeed()
    {
        Vector2 direction = new Vector2(1f, 1f).normalized;
        Vector3 velocity = new Vector3(0f, -2f, 0f);
        for (int i = 0; i < 100; i++)
            velocity = Calculate(velocity, direction);
        Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(8f).Within(0.0001f));
        Assert.That(velocity.y, Is.EqualTo(-2f));
    }

    [TestCase(2f, 9.81f, 0.02f)]
    [TestCase(4f, 20f, 0.01f)]
    public void JumpSpeedIncludesHalfStepGravityCompensation(float height, float gravity, float deltaTime)
    {
        float speed = MovementMath.CalculateJumpSpeed(height, gravity, deltaTime);
        Assert.That(speed, Is.EqualTo(Mathf.Sqrt(2f * gravity * height) + gravity * deltaTime * 0.5f).Within(0.0001f));
    }

    private static Vector3 Calculate(Vector3 velocity, Vector2 input)
        => MovementMath.CalculateVelocity(velocity, input, 8f, 10f, 50f, 30f, DeltaTime);
}
