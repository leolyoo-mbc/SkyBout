using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FallEliminationTests
{
    private GameObject player;
    private GameObject ground;
    private Rigidbody body;
    private PlayerMovement movement;
    private PlayerInputActions actions;
    private Keyboard keyboard;
    private Mouse mouse;
    private SimulationMode previousSimulation;
    private InputSettings.UpdateMode previousInputMode;
    private InputSettings.BackgroundBehavior previousBackgroundBehavior;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
#endif
    private Scene previousScene;
    private Scene testScene;
    private Scene integrationScene;
    private readonly Vector3 start = new Vector3(200f, 1f, 200f);

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("SkyBoutFallTests");
        SceneManager.SetActiveScene(testScene);
        previousSimulation = Physics.simulationMode;
        previousInputMode = InputSystem.settings.updateMode;
        previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        Physics.simulationMode = SimulationMode.Script;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = start + Vector3.down * 2f;
        ground.transform.localScale = new Vector3(32f, 2f, 32f);
        CreatePlayer();
    }

    private void CreatePlayer()
    {
        if (player != null) Object.DestroyImmediate(player);
        Press();
        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "FallTestPlayer";
        player.SetActive(false);
        player.transform.position = start;
        body = player.AddComponent<Rigidbody>();
        body.mass = 80f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
#if UNITY_EDITOR
        player.GetComponent<Collider>().sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/PlayerFriction.physicMaterial");
#endif
        var foot = new GameObject("GroundCheckOffset").transform;
        foot.SetParent(player.transform, false);
        foot.localPosition = Vector3.down;
        movement = player.AddComponent<PlayerMovement>();
        SetField("groundCheckTransform", foot);
        SetField("groundLayer", (LayerMask)(1 << ground.layer));
        player.SetActive(true);
        actions = (PlayerInputActions)Field("actions").GetValue(movement);
        actions.devices = new InputDevice[] { keyboard, mouse };
        movement.SendMessage("OnApplicationFocus", true);
        Physics.SyncTransforms();
        Press();
        Step(5);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        Physics.simulationMode = previousSimulation;
        InputSystem.settings.updateMode = previousInputMode;
        InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
#endif
        if (integrationScene.IsValid() && integrationScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(integrationScene);
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        if (testScene.IsValid() && testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(testScene);
    }

    [Test]
    public void FixtureSettlesOnActualSupport()
    {
        Assert.That(Grounded(), Is.True);
        Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
    }

    [Test]
    public void FourEdgesWalkingAndJumpingFallFortyTimes()
    {
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        Key[] keys = { Key.W, Key.S, Key.A, Key.D };
        for (int side = 0; side < directions.Length; side++)
        for (int jump = 0; jump < 2; jump++)
        for (int sample = 0; sample < 5; sample++)
        {
            CreatePlayer();
            Teleport(start + directions[side] * 15f);
            Step(5);
            Assert.That(Grounded(), Is.True);
            Press(jump == 1 ? new[] { keys[side], Key.Space } : new[] { keys[side] });
            for (int i = 0; i < 180 && body.position.y >= 0.8f; i++) Step();
            Assert.That(body.position.y, Is.LessThan(0.8f), $"side={side}, jump={jump}, sample={sample}");
            Assert.That(body.position.y, Is.GreaterThan(-8f), "Observe departure before elimination boundary.");
            Assert.That(Grounded(), Is.False);
            Assert.That(body.linearVelocity.y, Is.LessThan(0f));
        }
    }

    [Test]
    public void AirborneReturnLandsFiveTimesAndCentralJumpLandsTenTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            CreatePlayer();
            Teleport(start + Vector3.right * 15.5f);
            Step(5);
            Press(Key.D, Key.Space);
            Step(20);
            Assert.That(body.position.x, Is.GreaterThan(start.x + 16f), "Must actually leave island bounds.");
            Assert.That(Grounded(), Is.False);
            Press(Key.A);
            WaitForLanding();
            Assert.That(body.linearVelocity.x, Is.LessThan(0f));
            Press(); Step(10);
            Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f), "Landing must permit a new jump.");
        }
        CreatePlayer();
        for (int sample = 0; sample < 10; sample++)
        {
            Press(); Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Press();
            WaitForLanding();
        }
    }

    [Test]
    public void SpaceOutsideIslandNeverAddsJumpOrGliderFiveTimes()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            CreatePlayer();
            Teleport(start + Vector3.right * 20f + Vector3.up * 2f);
            Step(3);
            for (int i = 0; i < 10; i++)
            {
                float before = body.linearVelocity.y;
                Press(i % 2 == 0 ? new[] { Key.Space } : new Key[0]);
                Step();
                Assert.That(body.linearVelocity.y, Is.LessThan(before));
                Assert.That(Grounded(), Is.False);
            }
        }
    }

    [TestCase(-10f)]
    [TestCase(-20f)]
    public void BoundaryUsesFeetAndStrictlyBelowAtTwoHeights(float height)
    {
        for (int relation = -1; relation <= 1; relation++)
        for (int sample = 0; sample < 5; sample++)
        {
            CreatePlayer();
            SetField("fallDeathHeight", height);
            Teleport(new Vector3(220f, height + 1f + relation * 0.01f, 200f));
            movement.SendMessage("FixedUpdate");
            Assert.That(Eliminated(), Is.EqualTo(relation < 0), $"height={height}, relation={relation}");
        }
    }

    [Test]
    public void HighSpeedCrossingIsDetectedOnFirstBelowBoundaryObservation()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            CreatePlayer();
            Teleport(new Vector3(220f, -8f, 200f));
            body.linearVelocity = Vector3.down * 200f;
            movement.SendMessage("FixedUpdate");
            Assert.That(Eliminated(), Is.False);
            Physics.Simulate(Time.fixedDeltaTime);
            Assert.That(body.position.y - 1f, Is.LessThan(-10f));
            movement.SendMessage("FixedUpdate");
            Assert.That(Eliminated(), Is.True);
        }
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public void NonFiniteBoundaryIsRejected(float height)
    {
        SetField("fallDeathHeight", height);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^PlayerMovement: check"));
        movement.SendMessage("OnValidate");
        Assert.That(movement.enabled, Is.False);
    }

    [TestCase(0f)]
    [TestCase(5f)]
    public void FiniteNonNegativeBoundaryIsValidWithRaisedGround(float height)
    {
        ground.transform.position += Vector3.up * 20f;
        Teleport(start + Vector3.up * 20f);
        SetField("fallDeathHeight", height);
        movement.SendMessage("OnValidate");
        Assert.That(movement.enabled, Is.True);
        Step(5);
        Assert.That(Eliminated(), Is.False);
        Assert.That(Grounded(), Is.True);
    }

    [Test]
    public void TenEliminationsBlockTenSecondsOfInputAndCannotRevive()
    {
        for (int sample = 0; sample < 10; sample++)
        {
            CreatePlayer();
            Press(Key.D, Key.Space);
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.True);
            Teleport(new Vector3(220f, -9.01f, 200f));
            body.linearVelocity = new Vector3(8f, -2f, 8f);
            movement.SendMessage("FixedUpdate");
            Assert.That(Eliminated(), Is.True);
            Assert.That(Grounded(), Is.False);
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
            Assert.That(((System.Collections.Generic.HashSet<Collider>)Field("supportingColliders").GetValue(movement)).Count, Is.Zero);
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
            Assert.That(body.linearVelocity.y, Is.EqualTo(-2f), "Elimination does not cancel gravity velocity.");

            // A real supporting floor and fresh key presses cannot revive this instance.
            Teleport(start);
            for (int step = 0; step < Mathf.CeilToInt(10f / Time.fixedDeltaTime); step++)
            {
                Press(step % 2 == 0 ? new[] { Key.W, Key.D, Key.Space } : new Key[0]);
                Step();
                Assert.That(Eliminated(), Is.True);
                Assert.That(Grounded(), Is.False);
                Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
                Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
                Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f));
            }
            movement.SendMessage("OnApplicationFocus", false);
            movement.SendMessage("OnApplicationFocus", true);
            movement.enabled = false;
            movement.enabled = true;
            SetField("fallDeathHeight", -100f);
            Press(); Press(Key.Space); Step(5);
            Assert.That(Eliminated(), Is.True);
            Assert.That(Grounded(), Is.False);
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
        }
        CreatePlayer();
        Assert.That(Eliminated(), Is.False, "A new instance starts alive, not a reset of the old one.");
    }

    [Test]
    public void EliminationPrecedesGroundedPendingJumpEvenWithFocusLost()
    {
        Press(Key.Space);
        Assert.That(Grounded(), Is.True);
        Assert.That((bool)Field("pendingJump").GetValue(movement), Is.True);
        SetField("fallDeathHeight", 0.1f);
        movement.SendMessage("FixedUpdate");
        Assert.That(Eliminated(), Is.True);
        Assert.That(Grounded(), Is.False);
        Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
        CreatePlayer();
        movement.SendMessage("OnApplicationFocus", false);
        Teleport(new Vector3(220f, -10f, 200f));
        movement.SendMessage("FixedUpdate");
        Assert.That(Eliminated(), Is.True, "Focus gating must not skip death detection.");
    }

    [UnityTest]
    public IEnumerator SampleSceneTenNaturalFallsShowOverlayAndReloadAlive()
    {
#if UNITY_EDITOR
        actions.Player.Disable();
        movement.enabled = false;
        Physics.simulationMode = SimulationMode.FixedUpdate;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        System.IO.Directory.CreateDirectory("TestResults");
        System.IO.File.WriteAllText("TestResults/fall-overlay-timings.txt", "Natural SampleScene fall to first captured GUI frame\n");
        for (int sample = 0; sample <= 10; sample++)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            integrationScene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
            SceneManager.SetActiveScene(integrationScene);
            PlayerMovement actual = null;
            Camera camera = null;
            foreach (var root in integrationScene.GetRootGameObjects())
            {
                if (root.TryGetComponent<PlayerMovement>(out var candidate)) actual = candidate;
                if (root.TryGetComponent<Camera>(out var view)) camera = view;
            }
            Assert.That(actual, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            var actualBody = actual.GetComponent<Rigidbody>();
            var actualInput = (PlayerInputActions)Field("actions").GetValue(actual);
            actualInput.devices = new InputDevice[] { keyboard, mouse };
            actual.SendMessage("OnApplicationFocus", true);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(actual.IsEliminated, Is.False, "Every reload must create a living player.");
            Assert.That(Vector3.Distance(actualBody.position, new Vector3(0f, 1f, 0f)), Is.LessThan(0.03f));
            Assert.That((float)Field("fallDeathHeight").GetValue(actual), Is.EqualTo(-10f));
            Assert.That(Field("state").GetValue(actual).ToString(), Is.EqualTo("Grounded"));
            var target = typeof(TopDownCameraFollow).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(target.GetValue(camera.GetComponent<TopDownCameraFollow>()), Is.EqualTo(actual.transform));
            if (sample == 10)
            {
                yield return SceneManager.UnloadSceneAsync(integrationScene);
                break;
            }
            int initialFrame = Time.frameCount;
            actualBody.position = new Vector3(17.5f, 1f, 0f);
            actualBody.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            for (int i = 0; i < 180 && !actual.IsEliminated; i++) yield return new WaitForFixedUpdate();
            Assert.That(actual.IsEliminated, Is.True);
            Assert.That(actualBody.position.y - 1f, Is.LessThan(-10f));
            Assert.That(Time.frameCount, Is.GreaterThan(initialFrame));
            float detectedAt = Time.realtimeSinceStartup;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            if (sample == 9)
            {
                camera.GetComponent<TopDownCameraFollow>().enabled = false;
                actualBody.position += Vector3.right * 1000f;
                actual.transform.position = actualBody.position;
                Physics.SyncTransforms();
                Assert.That(camera.WorldToViewportPoint(actualBody.position).x, Is.GreaterThan(1f));
            }
            // Use the actual Game view after OnGUI, not Camera.Render (which excludes IMGUI).
            yield return new WaitForEndOfFrame();
            float elapsed = Time.realtimeSinceStartup - detectedAt;
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                AssertOverlayPixels(screenshot);
                Assert.That(elapsed, Is.LessThan(1f));
                System.IO.File.WriteAllBytes("TestResults/fall-overlay-" + screenshot.width + "x" + screenshot.height + "-" + sample + ".png", screenshot.EncodeToPNG());
                System.IO.File.AppendAllText("TestResults/fall-overlay-timings.txt",
                    $"sample={sample}, observedToVisible={elapsed:F4}s, view={screenshot.width}x{screenshot.height}\n");
            }
            finally { Object.DestroyImmediate(screenshot); }
            yield return new WaitForFixedUpdate();
            Assert.That(actual.IsEliminated, Is.True);
            Assert.That(Vector3.ProjectOnPlane(actualBody.linearVelocity, Vector3.up).magnitude, Is.Zero);
            Assert.That((bool)Field("pendingJump").GetValue(actual), Is.False);
            yield return SceneManager.UnloadSceneAsync(integrationScene);
        }
#else
        Assert.Ignore("SampleScene asset loading requires the Unity Editor.");
        yield break;
#endif
    }

    private static void AssertOverlayPixels(Texture2D screenshot)
    {
        int width = Mathf.Min(380, (int)(screenshot.width * 0.8f));
        int height = Mathf.Min(80, screenshot.height);
        var pixels = screenshot.GetPixels((screenshot.width - width) / 2, (screenshot.height - height) / 2, width, height);
        int white = 0, black = 0;
        foreach (var pixel in pixels)
        {
            if (pixel.r > 0.85f && pixel.g > 0.85f && pixel.b > 0.85f) white++;
            if (pixel.r < 0.08f && pixel.g < 0.08f && pixel.b < 0.08f) black++;
        }
        Assert.That(white, Is.GreaterThan(40), "ELIMINATED text must be rendered in the Game view.");
        Assert.That(black, Is.GreaterThan(pixels.Length / 2), "Text must have a high contrast dark panel.");
    }

    private bool Eliminated()
    {
        var property = typeof(PlayerMovement).GetProperty("IsEliminated");
        Assert.That(property, Is.Not.Null, "Read-only survival observation is missing.");
        Assert.That(property.CanWrite, Is.False);
        return (bool)property.GetValue(movement);
    }

    private void WaitForLanding()
    {
        for (int i = 0; i < 160; i++)
        {
            Step();
            if (Grounded()) return;
        }
        Assert.Fail("Player must return to real island support.");
    }

    private bool Grounded() => Field("state").GetValue(movement).ToString() == "Grounded";
    private FieldInfo Field(string name)
    {
        var field = typeof(PlayerMovement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return field;
    }
    private void SetField(string name, object value) => Field(name).SetValue(movement, value);
    private void Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }
    private void Step(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            movement.SendMessage("FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime);
        }
    }
    private void Teleport(Vector3 position)
    {
        body.position = position;
        player.transform.position = position;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }
}
