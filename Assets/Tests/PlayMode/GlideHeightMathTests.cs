using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GlideHeightMathTests
{
    // Reflection keeps the red phase compilable before the new math seam exists.
    private static float Next(float y, float target, float velocity, float dt = 0.02f)
    {
        var method = typeof(MovementMath).GetMethod("CalculateGlideVerticalVelocity", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, "Missing target height calculation");
        return (float)method.Invoke(null, new object[] { y, target, velocity, 3f, 10f, dt });
    }

    [Test]
    public void DirectionDampingTimeAndWorldHeightAreIndependentOfCentre()
    {
        Assert.That(Next(-1f, 0f, 0f), Is.EqualTo(0.18f).Within(0.00001f));
        Assert.That(Next(1f, 0f, 0f), Is.EqualTo(-0.18f).Within(0.00001f));
        Assert.That(Next(0f, 0f, 0f), Is.Zero);
        Assert.That(Next(10f, 10f, 5f), Is.EqualTo(4.8f).Within(0.00001f));
        Assert.That(Next(10f, 10f, -5f, 0.01f), Is.EqualTo(-4.9f).Within(0.00001f));
        Assert.That(Next(9f, 10f, 0f), Is.EqualTo(Next(-1f, 0f, 0f)));
        Assert.That(Next(-1f, 0f, 0f), Is.EqualTo(Next(-1f, 0f, 0f)));
    }

    [Test]
    public void ThirteenInitialConditionsConvergeWithoutRepeatedExcursions()
    {
        foreach (float offset in new[] { -3f, -1f, 0f, 1f, 3f })
        foreach (float initial in offset == 0f ? new[] { 0f } : new[] { -5f, 0f, 5f })
        {
            float y = offset, v = initial, min = float.MaxValue, max = float.MinValue;
            int lastOutside = -1, excursions = 0;
            bool opposite = false;
            var velocities = new float[751];
            velocities[0] = v;
            for (int tick = 1; tick <= 750; tick++)
            {
                float next = Next(y, 0f, v);
                Assert.That(Mathf.Abs(next - v), Is.LessThanOrEqualTo(0.20001f));
                v = next; y += v * 0.02f; velocities[tick] = v;
                if (tick >= 5) Assert.That(Mathf.Abs(v - velocities[tick - 5]), Is.LessThanOrEqualTo(1.00001f));
                if (Mathf.Abs(y) > 0.1f) lastOutside = tick;
                bool nowOpposite = offset != 0f && y * Mathf.Sign(offset) < -0.1f;
                if (nowOpposite && !opposite) excursions++;
                opposite = nowOpposite;
                if (tick >= 250) { min = Mathf.Min(min, y); max = Mathf.Max(max, y); }
            }
            Assert.That(lastOutside, Is.LessThan(250), $"offset={offset}, initial={initial}");
            Assert.That(max - min, Is.LessThanOrEqualTo(0.1f));
            Assert.That(excursions, Is.LessThanOrEqualTo(1));
        }
    }
}
