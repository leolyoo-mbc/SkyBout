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

public class JumpTests
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
    private InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
    private Scene previousScene;
    private Scene testScene;
    private Scene integrationScene;
    private readonly Vector3 start = new Vector3(200f, 1f, 200f);

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("SkyBoutJumpTests");
        SceneManager.SetActiveScene(testScene);
        previousSimulation = Physics.simulationMode;
        previousInputMode = InputSystem.settings.updateMode;
        previousBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        Physics.simulationMode = SimulationMode.Script;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = new Vector3(200f, -0.5f, 200f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);
        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "JumpTestPlayer";
        player.SetActive(false);
        player.transform.position = start;
        body = player.AddComponent<Rigidbody>();
        body.mass = 80f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
#if UNITY_EDITOR
        player.GetComponent<Collider>().sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/PlayerFriction.physicMaterial");
#endif
        var foot = new GameObject("GroundCheck").transform;
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
        InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
        if (integrationScene.IsValid() && integrationScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(integrationScene);
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        if (testScene.IsValid() && testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(testScene);
    }

    [Test]
    public void SupportRequiresContactNotNearbyFloorOrUpwardDeparture()
    {
        Assert.That(Grounded(), Is.True, "Settled flat ground must support the player.");
        Teleport(start + Vector3.up * 0.1f);
        Step();
        Assert.That(Grounded(), Is.False, "A nearby floor is not a landing.");
        Teleport(start);
        Step(5);
        body.linearVelocity = Vector3.up * 2f;
        movement.SendMessage("FixedUpdate");
        Assert.That(Grounded(), Is.False, "Old floor contacts cannot re-ground an ascending body.");
    }

    [Test]
    public void SideTriggerAndMissingFloorDoNotSupportPlayer()
    {
        ground.transform.position = new Vector3(201f, 1f, 200f);
        ground.transform.localScale = new Vector3(1f, 4f, 4f);
        Teleport(start);
        Step(2);
        Assert.That(Grounded(), Is.False, "Side contact is not support.");
        ground.GetComponent<Collider>().isTrigger = true;
        ground.transform.position = new Vector3(200f, -0.5f, 200f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);
        Teleport(start);
        Step(2);
        Assert.That(Grounded(), Is.False, "Triggers are not walkable support.");
        ground.SetActive(false);
        Teleport(start);
        Step();
        Assert.That(Grounded(), Is.False, "Starting height alone cannot count as ground.");
    }

    [Test]
    public void JumpActionUsesSpaceButton()
    {
        var jump = actions.Player.Jump;
        Assert.That(jump, Is.Not.Null);
        Assert.That(jump.type, Is.EqualTo(InputActionType.Button));
        Assert.That(System.Array.Exists(jump.bindings.ToArray(), b => b.path == "<Keyboard>/space"), Is.True);
    }

    [Test]
    public void TwentyJumpsLandAndCanJumpAgain()
    {
        for (int i = 0; i < 20; i++)
        {
            Press();
            float peak = CompleteJump(false, false);
            Assert.That(peak, Is.InRange(1.9f, 2.1f));
            Assert.That(Grounded(), Is.True, $"Jump {i + 1} must land.");
        }
    }

    [TestCase(2f)]
    [TestCase(4f)]
    public void HeightIsIndependentOfHoldAndMovement(float height)
    {
        SetField("jumpHeight", height);
        string measurementPath = "Temp/ground-jump-height-" + height + "m.txt";
        System.IO.File.WriteAllText(measurementPath, "target=" + height + "m\n");
        for (int condition = 0; condition < 4; condition++)
        {
            float min = float.MaxValue, max = 0f;
            for (int sample = 0; sample < 5; sample++)
            {
                Press();
                Teleport(start);
                Step(5);
                float peak = CompleteJump(condition == 1, condition == 3);
                min = Mathf.Min(min, peak);
                max = Mathf.Max(max, peak);
                Assert.That(peak, Is.InRange(height * 0.95f, height * 1.05f), $"height={height}, condition={condition}, sample={sample}");
            }
            string measurement = $"Height={height}, condition={condition}, samples=5, peak range={min:F4}..{max:F4}m";
            TestContext.WriteLine(measurement);
            System.IO.File.AppendAllText(measurementPath, measurement + "\n");
        }
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void InvalidHeightIsRejected(float height)
    {
        SetField("jumpHeight", height);
        ExpectInvalidSettings();
    }

    [TestCase("kinematic")]
    [TestCase("gravityOff")]
    [TestCase("damping")]
    [TestCase("verticalLock")]
    [TestCase("foot")]
    [TestCase("mask")]
    [TestCase("gravity")]
    public void InvalidDependenciesAreRejected(string setting)
    {
        Vector3 oldGravity = Physics.gravity;
        try
        {
            switch (setting)
            {
                case "kinematic": body.isKinematic = true; break;
                case "gravityOff": body.useGravity = false; break;
                case "damping": body.linearDamping = 1f; break;
                case "verticalLock": body.constraints |= RigidbodyConstraints.FreezePositionY; break;
                case "foot": SetField("groundCheckTransform", null); break;
                case "mask": SetField("groundLayer", (LayerMask)0); break;
                case "gravity": Physics.gravity = Vector3.zero; break;
            }
            ExpectInvalidSettings();
        }
        finally { Physics.gravity = oldGravity; }
    }

    [TestCase("Move")]
    [TestCase("Jump")]
    public void MissingRequiredActionIsRejected(string name)
    {
#if UNITY_EDITOR
        var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Player_Actions.inputactions");
        using (var changed = new PlayerInputActions())
        {
            changed.asset.FindAction("Player/" + name, true).RemoveAction();
            Assert.That(PlayerInputRefactorTests.CollectMismatches(source, changed.asset),
                Has.Some.Contains("/" + name + " is missing"));
        }
#endif
    }

    [TestCase("rising")]
    [TestCase("apex")]
    [TestCase("falling")]
    [TestCase("hold")]
    [TestCase("beforeLanding")]
    public void AirAndHeldInputNeverAutoJump(string phase)
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Press(); Teleport(start); Step(5);
            Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            if (phase != "hold") Press();
            if (phase == "apex") Step(30);
            if (phase == "falling") Step(40);
            if (phase == "beforeLanding")
            {
                for (int i = 0; i < 120 && (body.linearVelocity.y >= 0f || body.position.y > start.y + 0.15f); i++) Step();
                Assert.That(Grounded(), Is.False, "Pre-landing input must really be airborne.");
            }
            if (phase != "hold")
            {
                Press(Key.Space);
                Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
            }
            Step(160);
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            Assert.That(Grounded(), Is.True);
            Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(0.1f));
            Press(); Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f), "Release then a grounded press must rearm.");
        }
    }

    [Test]
    public void ShortPressIsConsumedOnceAndLosingSupportDiscardsRequest()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Press(); Teleport(start); Step(5);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            float first = body.linearVelocity.y;
            Step();
            Assert.That(body.linearVelocity.y, Is.LessThan(first));
            Step(160);
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            Press(Key.Space);
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.True);
            Teleport(start + Vector3.up * 5f);
            Step();
            Assert.That(body.linearVelocity.y, Is.LessThan(0f), "A request cannot survive loss of support.");
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
        }
    }

    [Test]
    public void FocusAndDisableClearRequestsAndRequireRelease()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Press(); Teleport(start); Step(5);
            Press(Key.Space);
            movement.SendMessage("OnApplicationFocus", false);
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
            movement.SendMessage("OnApplicationFocus", true);
            InputSystem.Update(); Step(5);
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            Press(); Press(Key.W, Key.Space); Step(10);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            float vertical = body.linearVelocity.y;
            movement.SendMessage("OnApplicationFocus", false);
            Assert.That(body.linearVelocity.y, Is.EqualTo(vertical));
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
            movement.SendMessage("OnApplicationFocus", true);
            InputSystem.Update(); Step(160);
            Assert.That(Grounded(), Is.True);
            Press(); Press(Key.Space);
            movement.enabled = false;
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
            movement.enabled = true;
            movement.SendMessage("OnApplicationFocus", true);
            InputSystem.Update(); Step(5);
            Assert.That(body.position.y, Is.EqualTo(start.y).Within(0.03f));
            Press(); Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            vertical = body.linearVelocity.y;
            movement.enabled = false;
            Assert.That(body.linearVelocity.y, Is.EqualTo(vertical));
            movement.enabled = true;
            movement.SendMessage("OnApplicationFocus", true);
            InputSystem.Update(); Step(160);
            Assert.That(Grounded(), Is.True);
            Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
        }
    }

    [Test]
    public void EightDirectionJumpsUseGroundRatesAndAirReleaseBrakes()
    {
        Key[][] keys = { new[] { Key.W }, new[] { Key.S }, new[] { Key.A }, new[] { Key.D },
            new[] { Key.W, Key.D }, new[] { Key.W, Key.A }, new[] { Key.S, Key.D }, new[] { Key.S, Key.A } };
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) };
        for (int i = 0; i < keys.Length; i++)
        {
            Press(); Teleport(start); Step(5);
            var launchKeys = new Key[keys[i].Length + 1];
            keys[i].CopyTo(launchKeys, 0); launchKeys[launchKeys.Length - 1] = Key.Space;
            Press(launchKeys); Step(45);
            Vector3 horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            Assert.That(horizontal.magnitude, Is.EqualTo(8f).Within(0.01f));
            Assert.That(Vector3.Dot(horizontal.normalized, directions[i].normalized), Is.GreaterThan(0.999f));
            Assert.That(Grounded(), Is.False);
            Press(); Step(10);
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.LessThan(0.001f));
            Step(100);
            Assert.That(Grounded(), Is.True);
            Press(keys[i]); Step();
            Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.GreaterThan(0f));
        }
    }

    [Test]
    public void AirDirectionChangeUsesTurnRateAndPreservesVerticalSpeed()
    {
        Press(Key.W, Key.Space); Step(12);
        float previousY = body.linearVelocity.y;
        Vector3 previousHorizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        Press(Key.D);
        movement.SendMessage("FixedUpdate");
        Assert.That(body.linearVelocity.y, Is.EqualTo(previousY).Within(0.0001f));
        Vector3 expected = Vector3.MoveTowards(previousHorizontal, Vector3.right * 8f, 50f * Time.fixedDeltaTime);
        Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up), expected), Is.LessThan(0.0001f));
    }

    [Test]
    public void ReenablingSleepingPlayerRebuildsSupport()
    {
        body.Sleep();
        movement.enabled = false;
        movement.enabled = true;
        movement.SendMessage("OnApplicationFocus", true);
        Press(); Step(5);
        Assert.That(Grounded(), Is.True);
        Press(Key.Space); Step();
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator SampleSceneRunsJumpMovementCameraAndReloadWithNaturalLifecycle()
    {
#if UNITY_EDITOR
        actions.Player.Disable();
        Physics.simulationMode = SimulationMode.FixedUpdate;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        integrationScene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        SceneManager.SetActiveScene(integrationScene);
        var actualPlayer = FindScenePlayer();
        var actualBody = actualPlayer.GetComponent<Rigidbody>();
        var actualInput = (PlayerInputActions)Field("actions").GetValue(actualPlayer);
        actualInput.devices = new InputDevice[] { keyboard, mouse };
        actualPlayer.SendMessage("OnApplicationFocus", true);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        Assert.That(Vector3.Distance(actualBody.position, new Vector3(0, 1, 0)), Is.LessThan(0.03f));
        int initialFrame = Time.frameCount;
        System.IO.File.WriteAllText("Temp/ground-jump-scene-measurements.txt", "Natural SampleScene lifecycle\n");
        Key[][] directions = { new[] { Key.W }, new[] { Key.S }, new[] { Key.A }, new[] { Key.D },
            new[] { Key.W, Key.D }, new[] { Key.W, Key.A }, new[] { Key.S, Key.D }, new[] { Key.S, Key.A } };
        foreach (var direction in directions)
            yield return ExerciseSceneJump(actualPlayer, direction, 2f);
        yield return ExerciseSceneJump(actualPlayer, new Key[0], 4f);
        Assert.That(Time.frameCount, Is.GreaterThan(initialFrame), "Natural player loop must advance.");

        // Edge departure must not land at the starting height or accept Space in the void.
        actualBody.position = new Vector3(15f, 1f, 0f);
        actualBody.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.Space));
        for (int i = 0; i < 200; i++)
        {
            yield return new WaitForFixedUpdate();
            if (actualBody.position.y < 0.8f) break;
        }
        Assert.That(actualBody.position.y, Is.LessThan(1f));
        Assert.That(Field("state").GetValue(actualPlayer).ToString(), Is.Not.EqualTo("Grounded"));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
        for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
        Assert.That(actualBody.linearVelocity.y, Is.LessThan(0f));
        float yVelocity = actualBody.linearVelocity.y;
        actualPlayer.SendMessage("OnApplicationFocus", false);
        Assert.That(actualBody.linearVelocity.y, Is.EqualTo(yVelocity));
        actualPlayer.SendMessage("OnApplicationFocus", true);

        // Contact with the island's vertical wall is not a landing.
        actualBody.position = new Vector3(16.49f, -0.8f, 0f);
        actualBody.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        Assert.That(Field("state").GetValue(actualPlayer).ToString(), Is.Not.EqualTo("Grounded"));

        yield return SceneManager.UnloadSceneAsync(integrationScene);
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        integrationScene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        actualPlayer = FindScenePlayer();
        ((PlayerInputActions)Field("actions").GetValue(actualPlayer)).devices = new InputDevice[] { keyboard, mouse };
        actualBody = actualPlayer.GetComponent<Rigidbody>();
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        Assert.That(Vector3.Distance(actualBody.position, new Vector3(0, 1, 0)), Is.LessThan(0.03f));
        Assert.That(Field("state").GetValue(actualPlayer).ToString(), Is.EqualTo("Grounded"));
        Assert.That(actualBody.linearVelocity.y, Is.LessThanOrEqualTo(0.1f), "Reload must not replay held Space.");
#else
        Assert.Ignore("SampleScene asset loading requires the Unity Editor.");
        yield break;
#endif
    }

    private PlayerMovement FindScenePlayer()
    {
        foreach (var root in integrationScene.GetRootGameObjects())
        {
            var candidate = root.GetComponentInChildren<PlayerMovement>();
            if (candidate != null) return candidate;
        }
        Assert.Fail("SampleScene PlayerMovement missing.");
        return null;
    }

    private IEnumerator ExerciseSceneJump(PlayerMovement actualPlayer, Key[] direction, float height)
    {
        var actualBody = actualPlayer.GetComponent<Rigidbody>();
        Field("jumpHeight").SetValue(actualPlayer, height);
        actualBody.position = new Vector3(0, 1, 0);
        actualBody.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
        Camera camera = null;
        foreach (var root in integrationScene.GetRootGameObjects())
            if (root.TryGetComponent<Camera>(out var candidate)) camera = candidate;
        Assert.That(camera, Is.Not.Null);
        var launchKeys = new Key[direction.Length + 1];
        direction.CopyTo(launchKeys, 0); launchKeys[launchKeys.Length - 1] = Key.Space;
        float baseline = actualBody.position.y;
        float peak = baseline;
        bool rose = false, fell = false, landed = false;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(launchKeys));
        for (int i = 0; i < 200; i++)
        {
            yield return new WaitForFixedUpdate();
            peak = Mathf.Max(peak, actualBody.position.y);
            rose |= actualBody.linearVelocity.y > 0.1f;
            if (!fell && actualBody.linearVelocity.y < -0.1f)
                SaveCameraFrame(camera, "Temp/ground-jump-" + height + "m.png");
            fell |= actualBody.linearVelocity.y < -0.1f;
            Bounds bounds = actualPlayer.GetComponent<Collider>().bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                ScenePointVisible(camera, point);
            }
            foreach (var offset in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                ScenePointVisible(camera, new Vector3(actualBody.position.x, 0, actualBody.position.z) + offset);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(i % 2 == 0 ? 10 : 900, 300) });
            if (i == 10) InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction));
            if (rose && fell && Field("state").GetValue(actualPlayer).ToString() == "Grounded") { landed = true; break; }
        }
        Assert.That(rose && fell && landed, Is.True, "Real scene jump must complete.");
        Assert.That(peak - baseline, Is.InRange(height * 0.95f, height * 1.05f));
        string measurement = $"SampleScene height={height}, measured={peak - baseline:F4}m, directionKeys={direction.Length}";
        TestContext.WriteLine(measurement);
        System.IO.File.AppendAllText("Temp/ground-jump-scene-measurements.txt", measurement + "\n");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
    }

    private static void SaveCameraFrame(Camera camera, string path)
    {
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var render = new RenderTexture(1024, 576, 24);
        var texture = new Texture2D(1024, 576, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = render;
            camera.Render();
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0);
            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            render.Release();
            Object.DestroyImmediate(render);
            Object.DestroyImmediate(texture);
        }
    }

    private static void ScenePointVisible(Camera camera, Vector3 point)
    {
        Vector3 viewport = camera.WorldToViewportPoint(point);
        Assert.That(viewport.z, Is.GreaterThan(0f));
        Assert.That(viewport.x, Is.InRange(0f, 1f));
        Assert.That(viewport.y, Is.InRange(0f, 1f));
    }

    private void ExpectInvalidSettings()
    {
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^PlayerMovement: check"));
        movement.SendMessage("OnValidate");
        Assert.That(movement.enabled, Is.False);
    }

    private float CompleteJump(bool hold, bool moving)
    {
        float takeoff = body.position.y;
        Press(moving ? new[] { Key.W, Key.Space } : new[] { Key.Space });
        Assert.That((bool)Field("pendingJump").GetValue(movement), Is.True,
            "Grounded input must enqueue jump: support=" + typeof(PlayerMovement).GetMethod("HasGroundSupport", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null)
            + ", held=" + Field("jumpHeld").GetValue(movement) + ", ready=" + Field("jumpReadyAfterRelease").GetValue(movement));
        Step();
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f),
            "A grounded Space press must launch. grounded=" + Grounded()
            + ", focused=" + Field("focused").GetValue(movement)
            + ", ready=" + Field("jumpReadyAfterRelease").GetValue(movement)
            + ", held=" + Field("jumpHeld").GetValue(movement)
            + ", Jump=" + actions.Player.Jump.ReadValue<float>() + ", enabled=" + movement.enabled);
        if (!hold) Press(moving ? new[] { Key.W } : new Key[0]);
        float peak = body.position.y;
        for (int i = 0; i < 250; i++)
        {
            Step();
            peak = Mathf.Max(peak, body.position.y);
            if (i > 5 && Grounded()) return peak - takeoff;
        }
        Assert.Fail("Jump did not land within 250 physics steps.");
        return 0f;
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
