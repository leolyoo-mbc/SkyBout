using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GlideHeightTests
{
    private GameObject player, floor, visual;
    private Rigidbody body;
    private PlayerMovement movement;
    private Keyboard keyboard;
    private Scene previous, scene;
    private SimulationMode simulation;
    private InputSettings.UpdateMode update;
    private InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
    private const float Dt = 0.02f;
    private readonly Vector3 start = new Vector3(500f, 1f, 500f);

    [SetUp]
    public void SetUp()
    {
        previous = SceneManager.GetActiveScene();
        scene = SceneManager.CreateScene("GlideHeightTests");
        SceneManager.SetActiveScene(scene);
        simulation = Physics.simulationMode; update = InputSystem.settings.updateMode;
        background = InputSystem.settings.backgroundBehavior;
        Physics.simulationMode = SimulationMode.Script;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        keyboard = InputSystem.AddDevice<Keyboard>();
        floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.transform.position = start + Vector3.down * 1.5f;
        floor.transform.localScale = new Vector3(32f, 1f, 32f);
        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.SetActive(false); player.transform.position = start;
        body = player.AddComponent<Rigidbody>(); body.constraints = RigidbodyConstraints.FreezeRotation;
        var feet = new GameObject("Feet").transform;
        feet.SetParent(player.transform, false); feet.localPosition = Vector3.down;
        visual = new GameObject("Wing"); visual.transform.SetParent(player.transform, false); visual.SetActive(false);
        movement = player.AddComponent<PlayerMovement>();
        Set("feetTransform", feet); Set("groundLayer", (LayerMask)(1 << floor.layer)); Set("gliderVisual", visual);
        Set("glideTargetFootHeight", 0f); // Isolated geometry uses origin target; actual SampleScene verifies the default 2m.
        player.SetActive(true);
        ((PlayerInputActions)Read("playerInput")).devices = new InputDevice[] { keyboard };
        movement.SendMessage("OnApplicationFocus", true);
        Physics.SyncTransforms(); Press(); Step(5);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(player); Object.DestroyImmediate(floor);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        Physics.simulationMode = simulation; InputSystem.settings.updateMode = update;
        InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    [Test]
    public void HoldingSpaceGlidesReleaseClosesAndFreshPressRedeploysFiveTimes()
    {
        Set("glideTargetFootHeight", 2f);
        for (int repeat = 0; repeat < 5; repeat++)
        {
            MoveFoot(-3f);
            Press(Key.Space, Key.W); Step(10);
            Assert.That(movement.IsGliding, Is.True);
            float beforeRelease = body.linearVelocity.y;
            var position = body.position;
            Press(Key.W); // Release only Space; movement remains held.
            Assert.That(body.position, Is.EqualTo(position), "Release must not teleport");
            Step();
            Assert.That(movement.IsGliding, Is.False, "Space release must close the glider");
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.linearVelocity.y, Is.EqualTo(beforeRelease + Physics.gravity.y * Dt).Within(0.001f));
            Assert.That(body.linearVelocity.z, Is.GreaterThan(0f));
            Press(Key.Space, Key.W); Step();
            Assert.That(movement.IsGliding, Is.True, "Fresh airborne press must redeploy");
            Press(); Step();
            Assert.That(movement.IsGliding, Is.False);
        }
    }

    [Test]
    public void ReleasedAirborneTapCannotLatchGliderBeforePhysics()
    {
        MoveFoot(3f);
        Press(Key.Space); Press(); Step();
        Assert.That(movement.IsGliding, Is.False);
        Assert.That(visual.activeSelf, Is.False);
        Assert.That(body.useGravity, Is.True);
        Assert.That(body.linearVelocity.y, Is.LessThan(0f));
    }

    [Test]
    public void FortyFivePhysicalFlightsConvergeAndHoldWithContinuousVelocity()
    {
        var samples = new StringBuilder("sample,offset,initial,tick,footY,velocityY\n");
        var summary = new StringBuilder("sample,offset,initial,settledSeconds,holdRange,excursions\n");
        int id = 0;
        foreach (float offset in new[] { -3f, -1f, 0f, 1f, 3f })
        foreach (float initial in Mathf.Abs(offset) == 3f ? new[] { -5f, 0f, 5f } : new[] { 0f })
        for (int repeat = 0; repeat < 5; repeat++)
        {
            ResetGround(); MoveFoot(offset, initial);
            var before = body.position;
            Press(Key.Space); movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsGliding, Is.True);
            Assert.That(visual.activeSelf, Is.True);
            Assert.That(body.useGravity, Is.False);
            Assert.That(Vector3.Distance(body.position, before), Is.LessThanOrEqualTo(0.001f));
            Assert.That(Mathf.Abs(body.linearVelocity.y - initial), Is.LessThanOrEqualTo(0.20001f));
            var heights = new float[751]; var velocities = new float[751];
            heights[0] = offset; velocities[0] = initial;
            int lastOutside = Mathf.Abs(offset) > 0.1f ? 0 : -1, excursions = 0;
            bool opposite = false;
            for (int tick = 1; tick <= 750; tick++)
            {
                if (tick == 1) Physics.Simulate(Dt); else Step();
                heights[tick] = body.position.y - 1f; velocities[tick] = body.linearVelocity.y;
                Assert.That(movement.IsGliding, Is.True, "No floor below these flights");
                if (tick >= 5) Assert.That(Mathf.Abs(velocities[tick] - velocities[tick - 5]), Is.LessThanOrEqualTo(1.00001f));
                if (Mathf.Abs(heights[tick]) > 0.1f) lastOutside = tick;
                bool nowOpposite = offset != 0f && heights[tick] * Mathf.Sign(offset) < -0.1f;
                if (nowOpposite && !opposite) excursions++;
                opposite = nowOpposite;
                samples.AppendLine(System.FormattableString.Invariant($"{id},{offset},{initial},{tick},{heights[tick]:R},{velocities[tick]:R}"));
            }
            int settled = lastOutside + 1;
            Assert.That(settled, Is.LessThanOrEqualTo(250), $"offset={offset} initial={initial}");
            float min = float.MaxValue, max = float.MinValue;
            for (int i = settled; i <= settled + 500; i++) { min = Mathf.Min(min, heights[i]); max = Mathf.Max(max, heights[i]); }
            Assert.That(max - min, Is.LessThanOrEqualTo(0.1f));
            Assert.That(excursions, Is.LessThanOrEqualTo(1));
            summary.AppendLine(System.FormattableString.Invariant($"{id++},{offset},{initial},{settled * Dt:R},{max - min:R},{excursions}"));
        }
        System.IO.Directory.CreateDirectory("TestResults");
        System.IO.File.WriteAllText("TestResults/007-height-samples.csv", samples.ToString());
        System.IO.File.WriteAllText("TestResults/007-height-summary.csv", summary.ToString());
        Assert.That(id, Is.EqualTo(45));
    }

    [Test]
    public void RepeatedSpaceDoesNotRetargetAndNonGlidingNeverCorrectsHeight()
    {
        MoveFoot(-2f); Step();
        Assert.That(body.linearVelocity.y, Is.LessThan(0f));
        Press(Key.Space); Step(250);
        Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
        MoveFoot(2f);
        for (int i = 0; i < 5; i++) { Press(Key.Space); Step(); }
        Step(250);
        Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
        Assert.That(movement.IsGliding, Is.True);
        ResetGround(); Set("glideTargetFootHeight", 3f); Step(50);
        Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
        Assert.That(movement.IsGliding, Is.False);
    }

    [Test]
    public void EightDirectionsTurningAndReleaseKeepHeightControlAtThreeHeights()
    {
        Key[][] keys = { new[] { Key.W }, new[] { Key.S }, new[] { Key.A }, new[] { Key.D },
            new[] { Key.W, Key.D }, new[] { Key.W, Key.A }, new[] { Key.S, Key.D }, new[] { Key.S, Key.A } };
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1,0,1), new Vector3(-1,0,1), new Vector3(1,0,-1), new Vector3(-1,0,-1) };
        foreach (float offset in new[] { -3f, 0f, 3f })
        for (int i = 0; i < keys.Length; i++)
        {
            ResetGround(); MoveFoot(offset); Press(Key.Space); Step();
            HoldGlide(keys[i]); Step();
            Assert.That(new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude, Is.EqualTo(0.2f).Within(0.001f));
            Step(44);
            var horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            Assert.That(horizontal.magnitude, Is.EqualTo(8f).Within(0.01f));
            Assert.That(Vector3.Dot(horizontal.normalized, directions[i].normalized), Is.GreaterThan(0.999f));
            HoldGlide(Key.D); Step();
            Assert.That(body.linearVelocity.x, Is.GreaterThanOrEqualTo(horizontal.x - 0.001f));
            HoldGlide(); Step(10);
            Assert.That(new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude, Is.LessThan(0.001f));
            Step(250);
            Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
            Assert.That(movement.IsGliding, Is.True);
        }
    }

    [TestCase("landing")]
    [TestCase("side")]
    [TestCase("ceiling")]
    public void PhysicalObstaclesRespectLandingAndResumeAfterOpeningFiveTimes(string kind)
    {
        var originalPosition = floor.transform.position;
        var originalScale = floor.transform.localScale;
        for (int sample = 0; sample < 5; sample++)
        {
            floor.transform.position = originalPosition; floor.transform.localScale = originalScale;
            Physics.SyncTransforms(); ResetGround();
            MoveFoot(kind == "ceiling" ? -3f : 3f);
            Press(Key.Space); Step();
            floor.transform.localScale = kind == "side" ? new Vector3(1f, 16f, 4f) : new Vector3(6f, 1f, 6f);
            floor.transform.position = kind == "landing" ? new Vector3(550f, 0.5f, 500f)
                : kind == "ceiling" ? new Vector3(550f, 0f, 500f) : new Vector3(551f, 0f, 500f);
            Physics.SyncTransforms();
            if (kind == "landing")
            {
                int ticks = 0;
                while (movement.IsGliding && ticks++ < 250) Step();
                Assert.That(ticks, Is.LessThan(250));
                Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
                Assert.That(body.position.y - 1f, Is.EqualTo(1f).Within(0.03f));
                Assert.That(visual.activeSelf, Is.False);
                Assert.That(body.useGravity, Is.True);
                Press(); Press(Key.Space); Step(); Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            }
            else
            {
                if (kind == "side") HoldGlide(Key.D);
                Step(200);
                Assert.That(movement.IsGliding, Is.True);
                if (kind == "side") Assert.That(body.position.x, Is.LessThanOrEqualTo(550.02f));
                else Assert.That(body.position.y + 1f, Is.LessThanOrEqualTo(-0.47f), "Capsule top must stay below ceiling");
                HoldGlide(); floor.transform.position = originalPosition; floor.transform.localScale = originalScale;
                Physics.SyncTransforms(); Step(250);
                Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
            }
        }
    }

    [Test]
    public void BoundaryDeploymentAndFastDescentCannotRescueEliminatedPlayer()
    {
        for (int repeat = 0; repeat < 5; repeat++)
        {
            foreach (float offset in new[] { -0.01f, 0f, 0.01f })
            {
                ResetGround(); MoveFoot(-10f + offset);
                Press(Key.Space); movement.SendMessage("FixedUpdate");
                Assert.That(movement.IsEliminated, Is.EqualTo(offset < 0f));
                Assert.That(movement.IsGliding, Is.EqualTo(offset >= 0f));
                if (offset >= 0f) Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            }
            ResetGround(); MoveFoot(-9.9f, -5f);
            Press(Key.Space); Step(4);
            Assert.That(movement.IsEliminated, Is.True, "Bounded braking must not teleport across death boundary");
            Assert.That(movement.IsGliding, Is.False);
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(body.useGravity, Is.True);
        }
    }

    [Test]
    public void TenRestartsClearVerticalMotionAndHeldSpaceCannotReplay()
    {
        for (int repeat = 0; repeat < 10; repeat++)
        {
            ResetGround(); MoveFoot(-3f); Press(Key.Space); Step(20);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            MoveFoot(-10.1f, -5f); Step();
            Press(Key.Space, Key.W); Step(3);
            Assert.That(movement.IsEliminated, Is.True);
            Press(Key.Space, Key.R); movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.False);
            Assert.That(movement.IsGliding, Is.False);
            Assert.That(body.position, Is.EqualTo(start));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Step(5); Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
            Press(); Press(Key.Space); Step(10);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Assert.That(movement.IsGliding, Is.False);
            Press(); Press(Key.Space); Step();
            Assert.That(movement.IsGliding, Is.True);
            Assert.That((float)Read("glideTargetFootHeight"), Is.Zero);
        }
    }

    [TestCase(-5f)]
    [TestCase(5f)]
    public void ExistingVelocityAtTargetBrakesThenReturnsWithoutPositionSnap(float velocity)
    {
        MoveFoot(0f, velocity); var position = body.position;
        Press(Key.Space); movement.SendMessage("FixedUpdate");
        Assert.That(body.position, Is.EqualTo(position));
        Assert.That(Mathf.Abs(body.linearVelocity.y - velocity), Is.LessThanOrEqualTo(0.20001f));
        Physics.Simulate(Dt); Step(250);
        Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
    }

    private object Read(string name) => Field(name).GetValue(movement);
    private void Set(string name, object value) => Field(name).SetValue(movement, value);
    private static FieldInfo Field(string name)
    {
        var field = typeof(PlayerMovement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name); return field;
    }
    private void Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update();
    }
    private void HoldGlide(params Key[] movementKeys)
    {
        var held = new Key[movementKeys.Length + 1];
        movementKeys.CopyTo(held, 0); held[held.Length - 1] = Key.Space; Press(held);
    }
    private void Step(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (movement.isActiveAndEnabled) movement.SendMessage("FixedUpdate");
            Physics.Simulate(Dt);
        }
    }
    private void MoveFoot(float y, float velocity = 0f)
    {
        body.position = new Vector3(550f, y + 1f, 500f); player.transform.position = body.position;
        body.linearVelocity = new Vector3(0f, velocity, 0f); Physics.SyncTransforms();
    }
    private void ResetGround()
    {
        if (movement.IsEliminated) { Press(); Press(Key.R); Step(); }
        body.position = start; player.transform.position = start; body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms(); Press(); Step(5);
        Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
    }
}
