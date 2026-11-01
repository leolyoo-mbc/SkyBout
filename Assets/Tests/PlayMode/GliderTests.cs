using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GliderTests
{
    private GameObject player, floor, visual;
    private Rigidbody body;
    private PlayerMovement movement;
    private Keyboard keyboard;
    private PlayerInputActions actions;
    private Scene previousScene, scene;
    private SimulationMode simulation;
    private InputSettings.UpdateMode updateMode;
    private InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
    private readonly Vector3 start = new Vector3(500f, 1f, 500f);

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        scene = SceneManager.CreateScene("GliderTests");
        SceneManager.SetActiveScene(scene);
        simulation = Physics.simulationMode;
        updateMode = InputSystem.settings.updateMode;
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
        player.SetActive(false);
        player.transform.position = start;
        body = player.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotation;
        var foot = new GameObject("Feet").transform;
        foot.SetParent(player.transform, false);
        foot.localPosition = Vector3.down;
        visual = new GameObject("GliderVisual");
        visual.transform.SetParent(player.transform, false);
        visual.SetActive(false);
        movement = player.AddComponent<PlayerMovement>();
        Set("feetTransform", foot);
        Set("groundLayer", (LayerMask)(1 << floor.layer));
        Set("glideTargetFootHeight", 0f); // Origin-floor fixture; actual scene tests use the requested 2m target.
        // The fixture must also compile and run before the glider fields exist.
        typeof(PlayerMovement).GetField("gliderVisual", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(movement, visual);
        player.SetActive(true);
        actions = (PlayerInputActions)Read("playerInput");
        actions.devices = new InputDevice[] { keyboard };
        movement.SendMessage("OnApplicationFocus", true);
        Physics.SyncTransforms();
        Press(); Step(5);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(floor);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        Physics.simulationMode = simulation;
        InputSystem.settings.updateMode = updateMode;
        InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }

    [TestCase("rising")]
    [TestCase("apex")]
    [TestCase("falling")]
    [TestCase("walkoff")]
    public void FreshAirPressDeploysOnceInEveryPhaseFiveTimes(string phase)
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor();
            if (phase == "walkoff")
            {
                MoveTo(start + Vector3.right * 17f);
                Step(3);
            }
            else
            {
                Press(Key.Space); Step();
                Assert.That(Gliding(), Is.False, "Holding the ground jump must not deploy.");
                if (phase == "rising") Step(10); // Leave room for repeated presses before landing.
                if (phase == "apex") Step(30);
                if (phase == "falling") Step(40);
            }
            Press();
            Vector3 before = body.position;
            Press(Key.Space); Press(); // Released airborne taps must not latch deployment.
            Assert.That((bool)Read("glideRequested"), Is.False);
            Press(Key.Space);
            Assert.That((bool)Read("glideRequested"), Is.True);
            float previousVelocity = body.linearVelocity.y;
            movement.SendMessage("FixedUpdate");
            Assert.That(Gliding(), Is.True);
            Assert.That(visual.activeSelf, Is.True);
            Assert.That(Vector3.Distance(body.position, before), Is.LessThan(0.001f));
            Assert.That(body.useGravity, Is.False);
            Assert.That(Mathf.Abs(body.linearVelocity.y - previousVelocity), Is.LessThanOrEqualTo(0.20001f));
            Assert.That((bool)Read("glideRequested"), Is.False);
            for (int i = 0; i < 5; i++)
            {
                Press(Key.Space); Step();
                Assert.That(Gliding(), Is.True);
                Assert.That(body.useGravity, Is.False);
                Assert.That((float)Read("glideTargetFootHeight"), Is.Zero);
                Assert.That((bool)Read("jumpRequested"), Is.False);
            }
        }
    }

    [Test]
    public void HoldingGroundJumpNeverDeploysFiveTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor();
            Press(Key.Space); Step(20);
            Assert.That(body.position.y, Is.GreaterThan(start.y));
            Assert.That(Gliding(), Is.False);
            Step(140);
            Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
            Assert.That(Gliding(), Is.False);
            Assert.That(body.linearVelocity.y, Is.LessThan(0.1f));
        }
    }

    [Test]
    public void FirstRequestWinsAndRequestsNeverChangeIntentFiveTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor();
            Press(Key.Space);
            MoveTo(start + Vector3.up * 4f);
            Press(); Press(Key.Space); Press();
            Step();
            Assert.That(Gliding(), Is.False, "A pending ground jump must not become a glide.");
            Assert.That(body.linearVelocity.y, Is.LessThan(0f));

            ResetOnFloor();
            MoveTo(start + Vector3.up * 0.1f);
            Press(Key.Space);
            Assert.That((bool)Read("glideRequested"), Is.True);
            MoveTo(start);
            // Let contacts update before consuming the accepted airborne request.
            Physics.Simulate(Time.fixedDeltaTime);
            Step();
            Assert.That(Gliding(), Is.False, "A pending glide must not become a landed jump.");
            Assert.That(body.linearVelocity.y, Is.LessThan(0.1f));
            Assert.That((bool)Read("glideRequested"), Is.False);
        }
    }

    [Test]
    public void GlideConvergesWhileNonDeployedPlayerStillFallsFivePairs()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor(); MoveTo(new Vector3(520f, 3f, 500f));
            Step(40);
            Assert.That(body.position.y - 1f, Is.LessThan(0f));
            ResetOnFloor(); MoveTo(new Vector3(520f, 3f, 500f));
            Press(Key.Space); Step(250);
            Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
            Step(500);
            Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
            Assert.That(Gliding(), Is.True);
        }
    }

    private static IEnumerable InvalidGlideSettings()
    {
        foreach (string field in new[] { "glideTargetFootHeight", "glideHeightResponse", "glideVerticalAcceleration" })
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        foreach (bool awake in new[] { false, true })
            yield return new TestCaseData(field, value, awake);
        foreach (string field in new[] { "glideHeightResponse", "glideVerticalAcceleration" })
        foreach (float value in new[] { 0f, -1f })
        foreach (bool awake in new[] { false, true })
            yield return new TestCaseData(field, value, awake);
        foreach (bool awake in new[] { false, true })
        {
            yield return new TestCaseData("glideTargetFootHeight", -10f, awake);
            yield return new TestCaseData("glideTargetFootHeight", -11f, awake);
            yield return new TestCaseData("glideHeightResponse", float.MaxValue, awake);
            yield return new TestCaseData("glideHeightResponse", float.Epsilon, awake);
            yield return new TestCaseData("glideVerticalAcceleration", 10.01f, awake);
        }
    }

    [TestCaseSource(nameof(InvalidGlideSettings))]
    public void InvalidGlideSettingIsRejectedAtAwakeAndOnValidate(string name, float value, bool awake)
    {
        if (!awake)
        {
            Set(name, value);
            LogAssert.Expect(LogType.Error, new Regex("PlayerMovement:"));
            movement.SendMessage("OnValidate");
            Assert.That(movement.enabled, Is.False);
            return;
        }
        player.SetActive(false);
        var clone = Object.Instantiate(player);
        try
        {
            var candidate = clone.GetComponent<PlayerMovement>();
            Field(name).SetValue(candidate, value);
            LogAssert.Expect(LogType.Error, new Regex("PlayerMovement:"));
            clone.SetActive(true);
            Assert.That(candidate.enabled, Is.False);
        }
        finally { Object.DestroyImmediate(clone); }
    }

    [TestCase(0f)]
    [TestCase(-2f)]
    public void SignedFiniteTargetAboveBoundaryIsValid(float height)
    {
        Set("glideTargetFootHeight", height);
        movement.SendMessage("OnValidate");
        Assert.That(movement.enabled, Is.True);
    }

    [Test]
    public void EightDirectionsTurningAndReleaseKeepExistingRatesFiveTimes()
    {
        Key[][] keys = { new[] { Key.W }, new[] { Key.S }, new[] { Key.A }, new[] { Key.D },
            new[] { Key.W, Key.D }, new[] { Key.W, Key.A }, new[] { Key.S, Key.D }, new[] { Key.S, Key.A } };
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1,0,1), new Vector3(-1,0,1), new Vector3(1,0,-1), new Vector3(-1,0,-1) };
        for (int sample = 0; sample < 5; sample++)
        for (int i = 0; i < keys.Length; i++)
        {
            ResetOnFloor();
            MoveTo(start + Vector3.up * 8f);
            Press(Key.Space); Step();
            HoldGlide(keys[i]); Step();
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude,
                Is.EqualTo(10f * Time.fixedDeltaTime).Within(0.001f));
            Step(44);
            var horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            Assert.That(horizontal.magnitude, Is.EqualTo(8f).Within(0.01f));
            Assert.That(Vector3.Dot(horizontal.normalized, directions[i].normalized), Is.GreaterThan(0.999f));
            HoldGlide(Key.D);
            movement.SendMessage("FixedUpdate");
            var expected = Vector3.MoveTowards(horizontal, Vector3.right * 8f,
                (i == 3 ? 10f : 50f) * Time.fixedDeltaTime);
            Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up), expected), Is.LessThan(0.001f));
            Assert.That(body.useGravity, Is.False);
            HoldGlide(); Step(10);
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.LessThan(0.001f));
            Assert.That(Gliding(), Is.True);
        }
    }

    [Test]
    public void LandingClosesImmediatelyAndHeldGlideNeverBecomesJumpTenTimes()
    {
        for (int sample = 0; sample < 10; sample++)
        {
            ResetOnFloor();
            Press(Key.Space); Step(15);
            Press(); Press(Key.Space); Step();
            Assert.That(Gliding(), Is.True);
            // Stop on the very first solved collision, without another controller tick.
            int ticks = 0;
            while (Gliding() && ticks++ < 250) Step();
            Assert.That(ticks, Is.LessThan(250), "Wait for actual support, not small vertical speed.");
            Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
            Assert.That(Gliding(), Is.False, "Landing must close before the next displayed frame.");
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(body.useGravity, Is.True);
            Step(5);
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            Assert.That((bool)Read("jumpRequested"), Is.False);
            Press(); Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }
    }

    [TestCase("side")]
    [TestCase("trigger")]
    [TestCase("nearby")]
    public void NonSupportingSurfacesDoNotCloseGliderFiveTimes(string kind)
    {
        for (int sample = 0; sample < 5; sample++)
        {
            floor.GetComponent<Collider>().isTrigger = false;
            floor.transform.position = start + Vector3.down * 1.5f;
            floor.transform.localScale = new Vector3(32f, 1f, 32f);
            ResetOnFloor();
            MoveTo(start + Vector3.up * 2f);
            Press(Key.Space); Step();
            if (kind == "side")
            {
                floor.transform.position = start + new Vector3(1f, 2f, 0f);
                floor.transform.localScale = new Vector3(1f, 6f, 4f);
            }
            else if (kind == "trigger") floor.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            Step(3);
            Assert.That(Gliding(), Is.True);
            Assert.That(Read("state").ToString(), Is.EqualTo("Gliding"));
            Assert.That(body.useGravity, Is.False);
        }
    }

    [Test]
    public void BoundaryAndSameTickInputAlwaysGiveDeathPriorityFiveTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        for (int relation = -1; relation <= 1; relation++)
        {
            ResetOnFloor();
            MoveTo(new Vector3(520f, 3f, 500f));
            Press(Key.Space); Step(); Press();
            MoveTo(new Vector3(520f, -9f + relation * 0.01f, 500f));
            Press(Key.Space);
            movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.EqualTo(relation < 0));
            Assert.That(Gliding(), Is.EqualTo(relation >= 0));
            if (relation < 0)
            {
                Assert.That(body.useGravity, Is.True);
                Assert.That(visual.activeSelf, Is.False);
                Press(); Press(Key.Space, Key.W); Step(5);
                Assert.That(Gliding(), Is.False);
                Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
            }
        }
        // A pending first deployment cannot rescue an already-below player.
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor();
            MoveTo(new Vector3(520f, -9.01f, 500f));
            Press(Key.Space); Press(Key.R);
            movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.True);
            Assert.That(Gliding(), Is.False);
            Assert.That((bool)Read("glideRequested"), Is.False);
        }
    }

    [TestCase("focus")]
    [TestCase("component")]
    [TestCase("object")]
    public void LifecycleClosesGliderAndRequiresFreshPressFiveTimes(string boundary)
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor();
            MoveTo(start + Vector3.up * 5f);
            Press(Key.Space);
            Assert.That((bool)Read("glideRequested"), Is.True);
            Suspend(boundary);
            Assert.That((bool)Read("glideRequested"), Is.False);
            Resume(boundary);
            Step(2);
            Assert.That(Gliding(), Is.False, "Returning cannot replay the pending request.");
            Press(); Press(Key.Space); Step();
            Assert.That(Gliding(), Is.True);
            Press(Key.Space, Key.W); Step(3);
            float before = body.position.y;
            float velocityBefore = body.linearVelocity.y;
            Suspend(boundary);
            Assert.That(Gliding(), Is.False);
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(body.useGravity, Is.True);
            // Deactivating the entire GameObject removes its body from simulation;
            // Unity may clear velocity. Only the resumed flight is prescribed there.
            if (boundary != "object") Assert.That(body.linearVelocity.y, Is.EqualTo(velocityBefore));
            Step(3);
            if (boundary != "object") Assert.That(body.position.y, Is.LessThan(before));
            else Assert.That(player.transform.position.y, Is.EqualTo(before));
            if (boundary != "object") Assert.That(body.linearVelocity.y, Is.LessThan(velocityBefore));
            Resume(boundary);
            Step(3);
            Assert.That(Gliding(), Is.False, "Held Space across input reset must not auto-redeploy");
            Assert.That((float)Read("glideTargetFootHeight"), Is.Zero);
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
            Press(); Press(Key.Space, Key.D); Step();
            Assert.That(Gliding(), Is.True);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            // Move outside support, then returning from any lifecycle boundary must converge.
            MoveTo(new Vector3(520f, 4f, 500f)); Press(Key.Space); Step(250);
            Assert.That(body.position.y - 1f, Is.EqualTo(0f).Within(0.1f));
            Assert.That(Gliding(), Is.True);
        }
    }

    [Test]
    public void DisabledLandingAndDeathRebuildStateOnReactivationFiveTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor(); MoveTo(start + Vector3.up * 2f);
            Press(Key.Space); Step();
            body.linearVelocity = Vector3.down * 2f;
            movement.enabled = false;
            Step(80); // Physics continues, but disabled callbacks must not retain old contacts.
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            movement.enabled = true;
            movement.SendMessage("OnApplicationFocus", true);
            Press(); Step(5);
            Assert.That(Gliding(), Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));

            MoveTo(new Vector3(520f, -8f, 500f));
            Press(Key.Space); Step();
            body.linearVelocity = Vector3.down * 2f;
            movement.enabled = false; Step(40);
            Assert.That(body.position.y - 1f, Is.LessThan(-10f));
            movement.enabled = true;
            movement.SendMessage("OnApplicationFocus", true);
            movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.True);
            Assert.That(Gliding(), Is.False);
            Assert.That(body.useGravity, Is.True);
        }
    }

    [Test]
    public void HeldSpaceAcrossDeathRestartCannotAutoDeployFiveTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            ResetOnFloor();
            MoveTo(new Vector3(520f, -9.01f, 500f));
            Step();
            Press(Key.Space); Press(Key.Space, Key.R);
            Step(); Step(5);
            Assert.That(movement.IsEliminated, Is.False);
            Assert.That(Gliding(), Is.False);
            Assert.That(visual.activeSelf, Is.False);
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            Press(); Press(Key.Space); Step(10);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Assert.That(Gliding(), Is.False);
            Press(); Press(Key.Space); Step();
            Assert.That(Gliding(), Is.True);
        }
    }

    [Test]
    public void RemovingControllerRestoresGravityAndHidesVisual()
    {
        MoveTo(start + Vector3.up * 3f);
        Press(Key.Space); Step();
        Assert.That(body.useGravity, Is.False);
        Object.DestroyImmediate(movement);
        Assert.That(body.useGravity, Is.True);
        Assert.That(visual.activeSelf, Is.False);
    }

    private void Suspend(string boundary)
    {
        if (boundary == "focus") movement.SendMessage("OnApplicationFocus", false);
        else if (boundary == "component") movement.enabled = false;
        else player.SetActive(false);
    }

    private void Resume(string boundary)
    {
        if (boundary == "component") movement.enabled = true;
        if (boundary == "object") player.SetActive(true);
        movement.SendMessage("OnApplicationFocus", true);
        InputSystem.Update();
    }

    private void ResetOnFloor()
    {
        if (movement.IsEliminated)
        {
            Press(); Press(Key.R); Step();
        }
        MoveTo(start);
        Press(); Step(5);
        Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
    }

    private bool Gliding()
    {
        var property = typeof(PlayerMovement).GetProperty("IsGliding");
        Assert.That(property, Is.Not.Null, "Missing glider observation: IsGliding");
        return (bool)property.GetValue(movement);
    }

    private static FieldInfo Field(string name)
    {
        var field = typeof(PlayerMovement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing player field: " + name);
        return field;
    }

    private object Read(string name) => Field(name).GetValue(movement);
    private void Set(string name, object value) => Field(name).SetValue(movement, value);
    private void Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }

    private void HoldGlide(params Key[] movementKeys)
    {
        var held = new Key[movementKeys.Length + 1];
        movementKeys.CopyTo(held, 0); held[held.Length - 1] = Key.Space; Press(held);
    }

    private void MoveTo(Vector3 position)
    {
        body.position = position;
        player.transform.position = position;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    private void Step(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            if (movement.isActiveAndEnabled) movement.SendMessage("FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime);
        }
    }
}
