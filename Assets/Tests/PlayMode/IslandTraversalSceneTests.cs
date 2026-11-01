using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class IslandTraversalSceneTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CsvPath = "TestResults/008-traversal-attempts.csv";
    private static readonly WaitForFixedUpdate Tick = new WaitForFixedUpdate();
    private Scene previousScene, scene;
    private bool ownsScene;
    private Keyboard keyboard;
    private PlayerMovement player;
    private Rigidbody body;
    private Camera view;
    private Transform feet;
    private PlayerInputActions actions;
    private InputDevice[] oldDevices;
    private InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
    private Vector3 spawn;
    private Quaternion spawnRotation;
    private float oldAspect;
    private Vector3 launchPosition;
    private float launchSpeed;
    private string failure = "Interrupted; see NUnit result";
    private readonly string run = DateTime.UtcNow.ToString("yyyyMMddTHHmmss.fff", CultureInfo.InvariantCulture);

    private sealed class Route
    {
        internal readonly string Name, Source, Destination;
        internal readonly Vector3 Start, Direction, Edge;
        internal readonly float Launch, Stop;
        internal readonly Key Key;
        internal readonly bool XAxis;
        internal Route(string name, string source, string destination, Vector3 start,
            Vector3 direction, Key key, float launch, float stop, Vector3 edge, bool xAxis)
        {
            Name = name; Source = source; Destination = destination; Start = start;
            Direction = direction; Key = key; Launch = launch; Stop = stop; Edge = edge; XAxis = xAxis;
        }
        internal float Coordinate(Vector3 point) => XAxis ? point.x : point.z;
        internal bool Reached(Vector3 point, float coordinate) =>
            (Coordinate(point) - coordinate) * (XAxis ? Direction.x : Direction.z) >= 0f;
    }

    private static readonly Route AB = new Route("A-B", "Ground", "IslandShort", new Vector3(0, 1, 11),
        Vector3.forward, Key.W, 15.2f, 25f, new Vector3(0, 0, 20), false);
    private static readonly Route BA = new Route("B-A", "IslandShort", "Ground", new Vector3(0, 1, 25),
        Vector3.back, Key.S, 20.8f, 11f, new Vector3(0, 0, 16), false);
    private static readonly Route BC = new Route("B-C", "IslandShort", "IslandLong", new Vector3(1, 1, 26),
        Vector3.right, Key.D, 5.2f, 25f, new Vector3(20, 0, 26), true);
    private static readonly Route CB = new Route("C-B", "IslandLong", "IslandShort", new Vector3(25, 1, 26),
        Vector3.left, Key.A, 20.8f, 1f, new Vector3(6, 0, 26), true);

    private Vector3 Foot => body.position + (feet.position - player.transform.position);
    private string State => Read(player, "state").ToString();
    private GameObject Wing => (GameObject)Read(player, "gliderVisual");
    private Vector3 HorizontalVelocity => new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Directory.CreateDirectory("TestResults");
        previousScene = SceneManager.GetActiveScene();
        background = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
        scene = SceneManager.GetSceneByPath(ScenePath);
        ownsScene = !scene.IsValid() || !scene.isLoaded;
        if (ownsScene)
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
        scene = SceneManager.GetSceneByPath(ScenePath);
        SceneManager.SetActiveScene(scene);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent<PlayerMovement>(out var movement)) player = movement;
            if (root.TryGetComponent<Camera>(out var camera)) view = camera;
        }
        Assert.That(player, Is.Not.Null);
        Assert.That(view, Is.Not.Null);
        body = player.GetComponent<Rigidbody>();
        feet = (Transform)Read(player, "feetTransform");
        spawn = (Vector3)Read(player, "startingPosition");
        spawnRotation = (Quaternion)Read(player, "startingRotation");
        actions = (PlayerInputActions)Read(player, "playerInput");
        oldDevices = actions.devices.HasValue ? actions.devices.Value.ToArray() : null;
        actions.devices = new InputDevice[] { keyboard };
        oldAspect = view.aspect;
        view.aspect = 16f / 9f;
        yield return PrepareAt(spawn);
#else
        Assert.Ignore("SampleScene integration requires Unity Editor.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (player != null && keyboard != null && keyboard.added)
        {
            yield return PrepareAt(spawn);
            if (actions != null) actions.devices = oldDevices;
        }
        if (view != null) view.aspect = oldAspect;
        if (ownsScene && scene.IsValid() && scene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scene);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
    }

    [UnityTest]
    public IEnumerator US1_LayoutHasThreeLevelSeparatedStaticIslandsAndSafeSpawn()
    {
        AssertIsland("Ground", new Vector3(0, -1, 0), new Vector3(32, 2, 32));
        AssertIsland("IslandShort", new Vector3(0, -1, 26), new Vector3(12, 2, 12));
        AssertIsland("IslandLong", new Vector3(26, -1, 26), new Vector3(12, 2, 12));
        Assert.That(Island("IslandShort").bounds.min.z - Island("Ground").bounds.max.z, Is.EqualTo(4f).Within(0.001f));
        Assert.That(Island("IslandLong").bounds.min.x - Island("IslandShort").bounds.max.x, Is.EqualTo(14f).Within(0.001f));
        Assert.That(Physics.Raycast(new Vector3(0, 5, 18), Vector3.down, 20, 8), Is.False, "No bridge across A-B.");
        Assert.That(Physics.Raycast(new Vector3(13, 5, 26), Vector3.down, 20, 8), Is.False, "No bridge across B-C.");
        Assert.That(Physics.Raycast(new Vector3(24, 5, 0), Vector3.down, 20, 8), Is.False, "Preserve east fall corridor.");
        Assert.That(spawn, Is.EqualTo(new Vector3(0, 1, 0)));
        yield return StableLanding(Island("Ground"));
        Assert.That((float)Read(player, "maxHorizontalSpeed"), Is.EqualTo(8));
        Assert.That((float)Read(player, "jumpHeight"), Is.EqualTo(2));
        Assert.That((float)Read(player, "glideTargetFootHeight"), Is.EqualTo(2));
        Assert.That((float)Read(player, "fallDeathHeight"), Is.EqualTo(-10));
        Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.02f).Within(0.00001f));
    }

    [UnityTest]
    public IEnumerator US1_GapsAndSideContactsAreNotGround()
    {
        Island("IslandShort");
        yield return PrepareAt(new Vector3(0, 1, 18), false);
        Assert.That(State, Is.EqualTo("Airborne"));
        Assert.That(body.linearVelocity.y, Is.LessThan(0));
        yield return PrepareAt(new Vector3(6.49f, -0.8f, 26), false);
        Assert.That(State, Is.EqualTo("Airborne"), "Only side contact is not landing.");
        Assert.That(Foot.y, Is.LessThan(0));
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator US1_ShortJumpBothDirectionsFiveTimesAndFreshGroundActions()
    {
        foreach (var route in new[] { AB, BA })
        for (int attempt = 0; attempt < 5; attempt++)
        {
            yield return PrepareAt(route.Start);
            yield return Recorded(route, attempt, "jump", Traverse(route, false, attempt == 0));
            yield return GroundActions(Island(route.Destination));
        }
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator US2_LongJumpAloneCannotLandInEitherDirectionFiveTimes()
    {
        foreach (var route in new[] { BC, CB })
        for (int attempt = 0; attempt < 5; attempt++)
        {
            yield return PrepareAt(route.Start);
            yield return Recorded(route, attempt, "jump-unreachable", UnreachableJump(route));
        }
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator US2_LongGlideBothDirectionsFiveTimesReleaseLandAndJump()
    {
        foreach (var route in new[] { BC, CB })
        for (int attempt = 0; attempt < 5; attempt++)
        {
            yield return PrepareAt(route.Start);
            yield return Recorded(route, attempt, "glide", Traverse(route, true, attempt == 0));
            yield return GroundActions(Island(route.Destination));
        }
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator US2_FiveDestinationHoldsAtTwoMetresForTenSecondsThenLandWithinThree()
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            var route = attempt % 2 == 0 ? BC : CB;
            yield return PrepareAt(route.Start);
            yield return Recorded(route, attempt, "hold-release", HoldAndLand(route));
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator US2_OvershootTurnBackAndLandUsingOnlyInput()
    {
        yield return PrepareAt(BC.Start);
        yield return Recorded(BC, 0, "overshoot-return", TurnBack());
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator US2_ThreeContinuousCircuitsWithVisibleDestinations()
    {
        for (int circuit = 0; circuit < 3; circuit++)
        {
            yield return NavigateOnSurface(spawn);
            foreach (var route in new[] { AB, BC, CB, BA })
            {
                yield return NavigateOnSurface(route.Start);
                yield return Recorded(route, circuit, "continuous", Traverse(route, route.XAxis, circuit == 0));
                yield return GroundActions(Island(route.Destination));
            }
        }
    }

    [UnityTest, Timeout(240000)]
    public IEnumerator US3_TenRemoteNaturalFallsRestartOriginalSpawnAndKeepLayout()
    {
        int playerId = player.GetInstanceID(), cameraId = view.GetInstanceID(), roots = scene.rootCount;
        var positions = new[] { Island("Ground").transform.position, Island("IslandShort").transform.position, Island("IslandLong").transform.position };
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var route = attempt % 2 == 0 ? BC : CB;
            yield return PrepareAt(route.Start);
            yield return Recorded(route, attempt, "fall-restart", RemoteFallAndRestart(route, attempt == 0));
            Assert.That(player.GetInstanceID(), Is.EqualTo(playerId));
            Assert.That(view.GetInstanceID(), Is.EqualTo(cameraId));
            Assert.That(scene.rootCount, Is.EqualTo(roots));
            for (int i = 0; i < 3; i++)
                Assert.That(Island(new[] { "Ground", "IslandShort", "IslandLong" }[i]).transform.position, Is.EqualTo(positions[i]));
            yield return GroundActions(Island("Ground"));
            yield return JumpAndDeploy(Key.None);
            Assert.That(player.IsGliding, Is.True);
            Press(); yield return null;
            yield return StableLanding(Island("Ground"));
        }
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator US3_HeldJumpFocusReleaseRedeployAndHeldRestartRespectExistingPolicy()
    {
        yield return PrepareAt(AB.Start);
        yield return Traverse(AB, false, false);
        // The original ground jump remains held through landing, never an airborne re-press.
        for (int i = 0; i < 50; i++)
        {
            yield return Tick;
            Assert.That(State, Is.EqualTo("Grounded"));
            Assert.That(player.IsGliding, Is.False);
        }
        yield return PrepareAt(BC.Start);
        yield return Traverse(BC, true, false, false);
        player.SendMessage("OnApplicationFocus", false);
        Assert.That(player.IsGliding, Is.False);
        Assert.That(body.useGravity, Is.True);
        player.SendMessage("OnApplicationFocus", true);
        Press(Key.Space); yield return null;
        for (int i = 0; i < 5; i++) yield return Tick;
        Assert.That(player.IsGliding, Is.False, "Focus return with held Space must not redeploy.");
        Press(); yield return null;
        Press(Key.Space); yield return null;
        yield return Tick;
        Assert.That(player.IsGliding, Is.True);
        Press(); yield return null;
        yield return StableLanding(Island("IslandLong"));
        yield return PrepareAt(BC.Start);
        yield return JumpAndDeploy(Key.D);
        yield return WaitFor(() => body.position.x >= 13, 5, "Reach the real gap");
        Press(); yield return null;
        yield return WaitFor(() => player.IsEliminated, 5, "Natural fall eliminates");
        Press(Key.Space, Key.D); yield return null;
        Press(Key.Space, Key.D, Key.R); yield return null;
        yield return WaitFor(() => !player.IsEliminated, 2, "Fresh R restarts");
        for (int i = 0; i < 5; i++) yield return Tick;
        Assert.That(player.IsGliding, Is.False);
        Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f));
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0), "Existing policy resumes held WASD, not held Space.");
        Press(); yield return null;
        yield return GroundActions(Island("Ground"));
    }

    private IEnumerator PrepareAt(Vector3 position, bool grounded = true)
    {
        player.SendMessage("OnApplicationFocus", true);
        // Focus resets release confirmation, so process a neutral input update AFTER it.
        Press(); yield return null;
        if (player.IsEliminated)
        {
            Press(Key.R); yield return null;
            yield return WaitFor(() => !player.IsEliminated, 2, "Fixture restart");
            Press(); yield return null;
        }
        // Independent fixture placement only. Never called during a continuous circuit.
        ((GroundSupport)Read(player, "groundSupport")).Clear();
        body.position = position;
        body.rotation = spawnRotation;
        player.transform.SetPositionAndRotation(position, spawnRotation);
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        Physics.SyncTransforms();
        for (int i = 0; i < 8; i++) yield return Tick;
        view.GetComponent<TopDownCameraFollow>().SnapToTarget();
        if (grounded) Assert.That(State, Is.EqualTo("Grounded"), "Fixture must settle on real support.");
    }

    private IEnumerator Launch(Route route, bool capture)
    {
        Assert.That(IsSupportedBy(Island(route.Source)), Is.True);
        Press(route.Key); yield return null;
        float deadline = Time.time + 5;
        while (!route.Reached(body.position, route.Launch))
        {
            CheckDeadline(deadline, "Run-up " + route.Name);
            if (Mathf.Abs(route.Coordinate(body.position) - route.Launch) <= 1f) AssertVisible(route);
            yield return Tick;
        }
        Assert.That(IsSupportedBy(Island(route.Source)), Is.True, "Launch foot stays inside source.");
        Assert.That(HorizontalVelocity.magnitude, Is.EqualTo(8f).Within(0.1f));
        launchPosition = body.position;
        launchSpeed = HorizontalVelocity.magnitude;
        if (capture) Capture("008-camera-" + (route.XAxis ? "us2-" : "us1-") + route.Name + "-launch");
        Press(route.Key, Key.Space); yield return null;
        yield return Tick;
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0), "Actual ground jump must precede glide.");
    }

    private IEnumerator Traverse(Route route, bool glide, bool capture, bool land = true)
    {
        yield return Launch(route, capture);
        if (glide)
        {
            for (int i = 0; i < 5; i++) yield return Tick;
            Press(route.Key); yield return null;
            Press(route.Key, Key.Space); yield return null;
            yield return Tick;
            Assert.That(player.IsGliding, Is.True);
            Assert.That(Wing.activeSelf, Is.True);
        }
        float deadline = Time.time + 8;
        bool captured = false;
        while (!route.Reached(body.position, route.Stop))
        {
            CheckDeadline(deadline, "Crossing " + route.Name);
            Assert.That(player.IsEliminated, Is.False);
            Assert.That(player.IsGliding, Is.EqualTo(glide));
            AssertVisible(route);
            if (capture && !captured && !IsSupportedBy(Island(route.Source)) && !IsSupportedBy(Island(route.Destination)))
            {
                Capture("008-camera-" + (route.XAxis ? "us2-" : "us1-") + route.Name + "-air");
                captured = true;
            }
            yield return Tick;
        }
        Press(Key.Space); yield return null; // Keep the jump/glide hold; brake with existing deceleration.
        yield return WaitFor(() => HorizontalVelocity.magnitude < 0.02f, 1, "Brake over destination");
        if (land)
        {
            if (glide) { Press(); yield return null; }
            yield return StableLanding(Island(route.Destination));
            Assert.That(Wing.activeSelf, Is.False);
        }
        AssertVisible(route);
        if (capture) Capture("008-camera-" + (route.XAxis ? "us2-" : "us1-") + route.Name + (land ? "-landed" : "-hover"));
    }

    private IEnumerator UnreachableJump(Route route)
    {
        yield return Launch(route, false);
        float deadline = Time.time + 5;
        while (Foot.y >= -0.5f)
        {
            CheckDeadline(deadline, "Unreachable jump must actually fall below surface");
            Assert.That(IsSupportedBy(Island(route.Destination)), Is.False);
            Assert.That(player.IsGliding, Is.False);
            yield return Tick;
        }
        Assert.That(route.Reached(body.position, route.Edge.x), Is.False, "Falls before long destination edge.");
        Press(); yield return null;
        yield return WaitFor(() => player.IsEliminated, 5, "Failed jump falls naturally");
    }

    private IEnumerator HoldAndLand(Route route)
    {
        yield return Traverse(route, true, false, false);
        yield return WaitFor(() => Mathf.Abs(Foot.y - 2f) <= 0.05f, 5, "Height settles");
        float end = Time.time + 10;
        while (Time.time < end)
        {
            Assert.That(Foot.y, Is.EqualTo(2f).Within(0.1f));
            Assert.That(player.IsGliding, Is.True);
            Assert.That(Wing.activeSelf, Is.True);
            Assert.That(State, Is.EqualTo("Gliding"));
            AssertVisible(route);
            yield return Tick;
        }
        float released = Time.time;
        Press(); yield return null;
        Assert.That(Wing.activeSelf, Is.False);
        Assert.That(body.useGravity, Is.True);
        yield return StableLanding(Island(route.Destination));
        Assert.That(Time.time - released, Is.LessThanOrEqualTo(3));
        yield return GroundActions(Island(route.Destination));
    }

    private IEnumerator TurnBack()
    {
        yield return Traverse(BC, true, true, false);
        Press(Key.D, Key.Space); yield return null;
        yield return WaitFor(() => body.position.x >= 33, 5, "Overshoot the far island");
        Press(Key.A, Key.Space); yield return null;
        yield return WaitFor(() => body.position.x <= 26, 5, "Return with opposite WASD");
        Press(Key.Space); yield return null;
        yield return WaitFor(() => HorizontalVelocity.magnitude < 0.02f, 1, "Stop after return");
        AssertVisible(BC);
        Capture("008-camera-us2-turn-return");
        Press(); yield return null;
        yield return StableLanding(Island("IslandLong"));
    }

    private IEnumerator RemoteFallAndRestart(Route route, bool capture)
    {
        yield return JumpAndDeploy(route.Key);
        yield return WaitFor(() => route.Reached(body.position, 13), 5, "Reach inter-island void");
        Press(Key.Space); yield return null;
        yield return WaitFor(() => HorizontalVelocity.magnitude < 0.02f, 1, "Brake in void");
        Assert.That(Physics.Raycast(Foot + Vector3.up * 0.1f, Vector3.down, 10, 8), Is.False);
        Press(); yield return null;
        yield return WaitFor(() => player.IsEliminated, 5, "Release produces real death below boundary");
        Assert.That(Foot.y, Is.LessThan(-10));
        Assert.That(Wing.activeSelf, Is.False);
        Press(Key.D, Key.Space); yield return null;
        for (int i = 0; i < 5; i++) yield return Tick;
        Assert.That(HorizontalVelocity.magnitude, Is.LessThan(0.01f));
        Assert.That(player.IsGliding, Is.False);
        Press(); yield return null;
        if (capture) { yield return new WaitForEndOfFrame(); CaptureScreen("008-camera-us3-eliminated"); }
        float requested = Time.realtimeSinceStartup;
        Press(Key.R); yield return null;
        yield return WaitFor(() => !player.IsEliminated, 2, "R returns to original spawn");
        Assert.That(Time.realtimeSinceStartup - requested, Is.LessThanOrEqualTo(2));
        Press(); yield return null;
        yield return StableLanding(Island("Ground"));
        Assert.That(Vector3.Distance(body.position, spawn), Is.LessThan(0.04f));
        Assert.That(Quaternion.Angle(body.rotation, spawnRotation), Is.LessThan(0.01f));
        Assert.That(HorizontalVelocity.magnitude, Is.LessThan(0.01f));
        Assert.That(player.IsGliding, Is.False);
        Assert.That(body.useGravity, Is.True);
        if (capture) { yield return new WaitForEndOfFrame(); CaptureScreen("008-camera-us3-restored"); }
    }

    private IEnumerator JumpAndDeploy(Key direction)
    {
        Press(); yield return null;
        launchPosition = body.position;
        launchSpeed = HorizontalVelocity.magnitude;
        Press(Keys(direction, true)); yield return null;
        for (int i = 0; i < 6; i++) yield return Tick;
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0));
        Press(Keys(direction, false)); yield return null;
        Press(Keys(direction, true)); yield return null;
        yield return Tick;
        Assert.That(player.IsGliding, Is.True);
    }

    private IEnumerator GroundActions(Collider destination)
    {
        Press(); yield return null;
        Vector3 before = body.position;
        Press(Key.W); yield return null;
        for (int i = 0; i < 10; i++) yield return Tick;
        Press(); yield return null;
        yield return WaitFor(() => HorizontalVelocity.magnitude < 0.02f, 1, "Ground brake");
        Assert.That(body.position.z - before.z, Is.GreaterThan(0.05f));
        Assert.That(IsSupportedBy(destination), Is.True);
        Press(Key.Space); yield return null;
        yield return Tick;
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0));
        Press(); yield return null;
        yield return StableLanding(destination);
    }

    private IEnumerator NavigateOnSurface(Vector3 point)
    {
        Press(); yield return null;
        float deadline = Time.time + 15;
        while (true)
        {
            var delta = point - body.position; delta.y = 0;
            if (delta.magnitude <= HorizontalVelocity.sqrMagnitude / 100f + 0.12f) break;
            CheckDeadline(deadline, "Walk to next run-up start " + point);
            Assert.That(State, Is.EqualTo("Grounded"));
            Key x = Mathf.Abs(delta.x) > 0.1f ? (delta.x > 0 ? Key.D : Key.A) : Key.None;
            Key z = Mathf.Abs(delta.z) > 0.1f ? (delta.z > 0 ? Key.W : Key.S) : Key.None;
            Press(new[] { x, z }.Where(key => key != Key.None).ToArray());
            yield return null;
            yield return Tick;
        }
        Press(); yield return null;
        yield return WaitFor(() => HorizontalVelocity.magnitude < 0.02f, 1, "Stop at run-up start");
        Assert.That(new Vector2(body.position.x - point.x, body.position.z - point.z).magnitude, Is.LessThan(0.4f));
    }

    private IEnumerator StableLanding(Collider destination)
    {
        int consecutive = 0;
        float deadline = Time.time + 3;
        while (consecutive < 5)
        {
            CheckDeadline(deadline, "Stable landing on " + destination.name);
            consecutive = IsSupportedBy(destination) && State == "Grounded"
                && Mathf.Abs(Foot.y - destination.bounds.max.y) <= 0.05f
                && Mathf.Abs(body.linearVelocity.y) <= 0.1f ? consecutive + 1 : 0;
            yield return Tick;
        }
    }

    private bool IsSupportedBy(Collider destination) =>
        Physics.Raycast(Foot + Vector3.up * 0.05f, Vector3.down, out var hit, 0.08f, 8, QueryTriggerInteraction.Ignore)
        && hit.collider == destination && hit.normal.y >= 0.7f;

    private IEnumerator WaitFor(Func<bool> condition, float seconds, string message)
    {
        float deadline = Time.time + seconds;
        while (!condition()) { CheckDeadline(deadline, message); yield return Tick; }
    }

    private void CheckDeadline(float deadline, string message)
    {
        if (Time.time <= deadline) return;
        failure = message + "; position=" + body.position + "; velocity=" + body.linearVelocity + "; state=" + State;
        Assert.Fail(failure);
    }

    private IEnumerator Recorded(Route route, int attempt, string mode, IEnumerator operation)
    {
        float started = Time.time;
        launchPosition = body.position;
        launchSpeed = HorizontalVelocity.magnitude;
        bool completed = false;
        failure = "Assertion failed or interrupted; see matching NUnit test result";
        try { yield return operation; completed = true; }
        finally
        {
            if (!File.Exists(CsvPath))
                File.WriteAllText(CsvPath, "scenario,direction,attempt,launchPosition,launchSpeed,mode,destination,landedCollider,footHeight,elapsedGameSeconds,result,reason\n");
            string landed = Physics.Raycast(Foot + Vector3.up * 0.05f, Vector3.down, out var hit, 0.08f, 8) ? hit.collider.name : "none";
            var fields = new[] { run + ":" + TestContext.CurrentContext.Test.Name, route.Name, attempt.ToString(CultureInfo.InvariantCulture),
                launchPosition.ToString("F3", CultureInfo.InvariantCulture), launchSpeed.ToString("R", CultureInfo.InvariantCulture), mode, route.Destination, landed,
                Foot.y.ToString("R", CultureInfo.InvariantCulture), (Time.time - started).ToString("R", CultureInfo.InvariantCulture),
                completed ? "Passed" : "FailedOrInterrupted", completed ? "" : failure };
            File.AppendAllText(CsvPath, string.Join(",", fields.Select(field => "\"" + field.Replace("\"", "\"\"") + "\"")) + "\n");
        }
    }

    private Collider Island(string name)
    {
        var root = scene.GetRootGameObjects().SingleOrDefault(candidate => candidate.name == name);
        Assert.That(root, Is.Not.Null, "Missing scene island: " + name);
        var collider = root.GetComponent<Collider>();
        Assert.That(collider, Is.Not.Null, name + " requires a collider");
        return collider;
    }

    private void AssertIsland(string name, Vector3 center, Vector3 size)
    {
        var collider = Island(name);
        Assert.That(collider.transform.position, Is.EqualTo(center));
        Assert.That(collider.transform.lossyScale, Is.EqualTo(size));
        Assert.That(Quaternion.Angle(collider.transform.rotation, Quaternion.identity), Is.LessThan(0.001f));
        Assert.That(collider.bounds.max.y, Is.EqualTo(0).Within(0.001f));
        Assert.That(collider.enabled, Is.True);
        Assert.That(collider.isTrigger, Is.False);
        Assert.That(collider.gameObject.layer, Is.EqualTo(3));
        Assert.That(collider.attachedRigidbody, Is.Null);
        Assert.That(collider.gameObject.activeInHierarchy, Is.True);
        Assert.That(collider.GetComponent<Renderer>().enabled, Is.True);
    }

    private void AssertVisible(Route route)
    {
        foreach (var point in new[] { player.transform.position, route.Edge })
        {
            var viewport = view.WorldToViewportPoint(point);
            Assert.That(viewport.z, Is.GreaterThan(0), route.Name + " point behind camera");
            Assert.That(viewport.x, Is.InRange(0.02f, 0.98f), route.Name + " horizontal visibility at " + body.position);
            Assert.That(viewport.y, Is.InRange(0.02f, 0.98f), route.Name + " vertical visibility at " + body.position);
        }
        var follow = view.GetComponent<TopDownCameraFollow>();
        Assert.That((float)Read(follow, "pitch"), Is.EqualTo(55));
        Assert.That((float)Read(follow, "distance"), Is.EqualTo(18));
        Assert.That((float)Read(follow, "fieldOfView"), Is.EqualTo(60));
        Assert.That((float)Read(follow, "followSharpness"), Is.EqualTo(10));
    }

    private void Capture(string name)
    {
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var oldTarget = view.targetTexture;
        var oldActive = RenderTexture.active;
        Texture2D image = null;
        try
        {
            view.targetTexture = target;
            view.Render();
            RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("TestResults/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            view.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
            if (image != null) Object.Destroy(image);
        }
    }

    private static void CaptureScreen(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes("TestResults/" + name + ".png", image.EncodeToPNG()); }
        finally { Object.Destroy(image); }
    }

    private static object Read(object target, string field)
    {
        var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, field);
        return info.GetValue(target);
    }

    private static Key[] Keys(Key direction, bool space) => direction == Key.None
        ? (space ? new[] { Key.Space } : new Key[0])
        : (space ? new[] { direction, Key.Space } : new[] { direction });
    private void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
}
