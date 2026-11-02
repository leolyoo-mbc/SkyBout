using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GliderSceneTests
{
    private Scene previousScene, scene;
    private bool ownsScene;
    private Keyboard keyboard;
    private PlayerMovement player;
    private Rigidbody body;
    private Camera camera;
    private PlayerInputActions actions;
    private InputDevice[] oldDevices;
    private InputSettings.BackgroundBehavior background;
    private StringBuilder flightDiagnostics;
    private object reader;
    private PropertyInfo readerFocus;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        background = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
        editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        keyboard = InputSystem.AddDevice<Keyboard>();
        scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        ownsScene = !scene.IsValid() || !scene.isLoaded;
        if (ownsScene)
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        scene = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        SceneManager.SetActiveScene(scene);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.TryGetComponent<PlayerMovement>(out var candidate)) player = candidate;
            if (root.TryGetComponent<Camera>(out var view)) camera = view;
        }
        Assert.That(player, Is.Not.Null);
        Assert.That(camera, Is.Not.Null);
        body = player.GetComponent<Rigidbody>();
        reader = Read("inputReader");
        readerFocus = reader.GetType().GetProperty("HasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
        actions = (PlayerInputActions)Read("playerInput");
        oldDevices = actions.devices.HasValue ? actions.devices.Value.ToArray() : null;
        actions.devices = new InputDevice[] { keyboard };
        player.SendMessage("OnApplicationFocus", true);
        Press(); yield return null;
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
#else
        Assert.Ignore("Scene integration requires Unity Editor.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (flightDiagnostics != null)
        {
            System.IO.Directory.CreateDirectory("TestResults");
            System.IO.File.WriteAllText("TestResults/007-hold-flight-diagnostics.csv", flightDiagnostics.ToString());
            flightDiagnostics = null;
        }
        if (actions != null && player != null) actions.devices = oldDevices;
        if (ownsScene && scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
    }

    [UnityTest]
    public IEnumerator FreshAirPressShowsNonPhysicalWingWithinPointTwoSeconds()
    {
        var visual = (GameObject)Read("gliderVisual");
        Assert.That(visual, Is.Not.Null, "SampleScene needs the wing reference.");
        Assert.That(visual.transform.IsChildOf(player.transform), Is.True);
        Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
        Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        Assert.That(visual.GetComponentInChildren<Renderer>(true), Is.Not.Null);
        Assert.That(visual.activeSelf, Is.False);
        Press(Key.Space); yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        Assert.That(visual.activeSelf, Is.False);
        Press(); yield return null;
        float requested = Time.realtimeSinceStartup;
        Press(Key.Space);
        for (int i = 0; i < 10 && !visual.activeSelf; i++) yield return null;
        Assert.That(visual.activeSelf, Is.True);
        Assert.That(Time.realtimeSinceStartup - requested, Is.LessThanOrEqualTo(0.2f));
        yield return new WaitForEndOfFrame();
        AssertWingRendered(visual, "TestResults/007-hold-deployed.png");
    }

    [UnityTest]
    public IEnumerator HoldingAtTwoMetresThenReleaseLandsAndFreshGroundPressJumps()
    {
        var visual = (GameObject)Read("gliderVisual");
        Press(Key.Space); yield return null;
        for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
        Press(); yield return null;
        Press(Key.Space); yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(visual.activeSelf, Is.True);
        Assert.That((float)Read("glideTargetFootHeight"), Is.EqualTo(2f));
        Assert.That((float)Read("jumpHeight"), Is.EqualTo(2f));
        for (int i = 0; i < 250; i++) yield return new WaitForFixedUpdate();
        Assert.That(body.position.y - 1f, Is.EqualTo(2f).Within(0.1f));
        Assert.That(player.IsGliding, Is.True);
        Press();
        yield return null;
        Assert.That(visual.activeSelf, Is.False, "Releasing Space must close before landing");
        Assert.That(body.useGravity, Is.True);
        int ticks = 0;
        while (Read("state").ToString() != "Grounded" && ticks++ < 250) yield return new WaitForFixedUpdate();
        Assert.That(ticks, Is.LessThan(250), "Only actual support closes the glider.");
        yield return new WaitForEndOfFrame();
        Assert.That(visual.activeSelf, Is.False);
        Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
        Assert.That(body.useGravity, Is.True);
        var viewport = camera.WorldToViewportPoint(player.transform.position);
        Assert.That(viewport.z, Is.GreaterThan(0f));
        Assert.That(viewport.x, Is.InRange(0.1f, 0.9f));
        Assert.That(viewport.y, Is.InRange(0.1f, 0.9f));
        var frame = ScreenCapture.CaptureScreenshotAsTexture();
        try { System.IO.File.WriteAllBytes("TestResults/007-hold-landed.png", frame.EncodeToPNG()); }
        finally { Object.Destroy(frame); }
        Press(Key.Space); yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        Assert.That(visual.activeSelf, Is.False);
        System.IO.File.WriteAllText("TestResults/007-hold-scene-landing.md",
            $"Actual SampleScene held at foot Y=2, then Space release restored gravity and landed in {ticks} fixed waits: Grounded, wing closed, fresh ground Space jumped. No proximity-grounding change.\n");
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator TenNaturalWalkoffsHoldTwoMetresReleaseFallAndRestartSameInstances()
    {
        var visual = (GameObject)Read("gliderVisual");
        int playerId = player.GetInstanceID(), cameraId = camera.GetInstanceID();
        int children = player.GetComponentsInChildren<Transform>(true).Length, roots = scene.rootCount;
        Vector3 start = body.position;
        Quaternion rotation = body.rotation;
        System.IO.Directory.CreateDirectory("TestResults");
        System.IO.File.WriteAllText("TestResults/007-hold-repeat-timings.csv", "sample,deployGameTime,holdGameTime,deathGameTime,overlaySeconds,restartSeconds\n");
        flightDiagnostics = new StringBuilder("sample,phase,tick,gameTime,footY,velocityY,state,readerFocus,applicationFocus,spacePressed,simulationMode,inputUpdateMode\n");
        for (int sample = 0; sample < 10; sample++)
        {
            Assert.That(visual.activeSelf, Is.False);
            Press(Key.D); yield return null;
            int ticks = 0;
            while (Read("state").ToString() == "Grounded" && ticks++ < 250) yield return new WaitForFixedUpdate();
            Assert.That(ticks, Is.LessThan(250), "Must actually walk beyond island support.");
            Press(); yield return null;
            Press(Key.Space); yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(visual.activeSelf, Is.True);
            Assert.That(body.useGravity, Is.False);
            float deployed = Time.time;
            Press(Key.Space);
            // Move farther from the edge using normal input so side contacts cannot count as landing.
            Press(Key.Space, Key.D);
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate();
                RecordFlight(sample, "depart", i);
            }
            Press(Key.Space);
            for (int i = 0; i < 250; i++)
            {
                yield return new WaitForFixedUpdate();
                RecordFlight(sample, "converge", i);
            }
            Assert.That(body.position.y - 1f, Is.EqualTo(2f).Within(0.1f),
                $"Sample {sample}: state={Read("state")}, focus={readerFocus.GetValue(reader)}, " +
                $"Space={keyboard.spaceKey.isPressed}. See TestResults/007-hold-flight-diagnostics.csv.");
            float held = Time.time;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 500; i++)
            {
                yield return new WaitForFixedUpdate();
                RecordFlight(sample, "hold", i);
                float foot = body.position.y - 1f;
                min = Mathf.Min(min, foot); max = Mathf.Max(max, foot);
                Assert.That(foot, Is.EqualTo(2f).Within(0.1f));
                Assert.That(player.IsGliding, Is.True);
            }
            Assert.That(max - min, Is.LessThanOrEqualTo(0.1f));
            // Real release, real gravity, real fall; no forced boundary placement.
            Press();
            ticks = 0;
            while (!player.IsEliminated && ticks++ < 400) yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.True);
            float deathGameTime = Time.time;
            float died = Time.realtimeSinceStartup;
            yield return new WaitForEndOfFrame();
            Assert.That(visual.activeSelf, Is.False);
            float visibleAfter = Time.realtimeSinceStartup - died;
            Assert.That(visibleAfter, Is.LessThan(1f));
            var deadFrame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                AssertOverlay(deadFrame, true);
                if (sample == 0 || sample == 9)
                    System.IO.File.WriteAllBytes("TestResults/007-hold-eliminated.png", deadFrame.EncodeToPNG());
            }
            finally { Object.Destroy(deadFrame); }
            float requested = Time.realtimeSinceStartup;
            Press(Key.R); yield return null;
            ticks = 0;
            while (player.IsEliminated && ticks++ < 100) yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.False);
            float restoredAfter = Time.realtimeSinceStartup - requested;
            Assert.That(restoredAfter, Is.LessThan(2f));
            yield return new WaitForEndOfFrame();
            Assert.That(visual.activeSelf, Is.False);
            var restoredFrame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                AssertOverlay(restoredFrame, false);
                if (sample == 0 || sample == 9)
                    System.IO.File.WriteAllBytes("TestResults/007-hold-restored.png", restoredFrame.EncodeToPNG());
            }
            finally { Object.Destroy(restoredFrame); }
            Assert.That(body.useGravity, Is.True);
            Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.04f));
            Assert.That(Quaternion.Angle(body.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(player.GetInstanceID(), Is.EqualTo(playerId));
            Assert.That(camera.GetInstanceID(), Is.EqualTo(cameraId));
            Assert.That(scene.rootCount, Is.EqualTo(roots));
            Assert.That(player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
            var follow = camera.GetComponent<TopDownCameraFollow>();
            var distance = (float)typeof(TopDownCameraFollow).GetField("distance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(follow);
            Assert.That(Vector3.Distance(camera.transform.position, player.transform.position - camera.transform.forward * distance), Is.LessThan(0.1f));
            System.IO.File.AppendAllText("TestResults/007-hold-repeat-timings.csv",
                System.FormattableString.Invariant($"{sample},{deployed:R},{held:R},{deathGameTime:R},{visibleAfter:R},{restoredAfter:R}\n"));
            Press(); yield return null;
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
            // Each restarted attempt must still accept a fresh jump and fresh airborne deployment.
            Press(Key.Space); yield return null;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Press(); yield return null;
            Press(Key.Space); yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsGliding, Is.True);
            Press();
            ticks = 0;
            while (Read("state").ToString() != "Grounded" && ticks++ < 250) yield return new WaitForFixedUpdate();
            Assert.That(Read("state").ToString(), Is.EqualTo("Grounded"));
        }
    }

    private static void AssertOverlay(Texture2D frame, bool expected)
    {
        int width = Mathf.Min(380, (int)(frame.width * 0.8f)), height = Mathf.Min(80, frame.height);
        var pixels = frame.GetPixels((frame.width - width) / 2, (frame.height - height) / 2, width, height);
        int black = 0, white = 0;
        foreach (var p in pixels)
        {
            if (p.r < 0.08f && p.g < 0.08f && p.b < 0.08f) black++;
            if (p.r > 0.85f && p.g > 0.85f && p.b > 0.85f) white++;
        }
        if (expected)
        {
            Assert.That(black, Is.GreaterThan(pixels.Length / 2));
            Assert.That(white, Is.GreaterThan(40));
        }
        else Assert.That(black, Is.LessThan(pixels.Length / 2), "First restored frame must not retain the old panel.");
    }

    private void AssertWingRendered(GameObject visual, string path)
    {
        // Render with/without the wing using the actual scene camera. A state flag is not visual proof.
        var target = RenderTexture.GetTemporary(640, 360, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        Texture2D withWing = null, withoutWing = null;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            withWing = new Texture2D(640, 360, TextureFormat.RGB24, false);
            withWing.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); withWing.Apply();
            visual.SetActive(false);
            camera.Render();
            withoutWing = new Texture2D(640, 360, TextureFormat.RGB24, false);
            withoutWing.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); withoutWing.Apply();
            var a = withWing.GetPixels32(); var b = withoutWing.GetPixels32();
            int changed = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 30) changed++;
            Assert.That(changed, Is.GreaterThan(40), "Wing must affect visible pixels, not just be active.");
            System.IO.Directory.CreateDirectory("TestResults");
            System.IO.File.WriteAllBytes(path, withWing.EncodeToPNG());
        }
        finally
        {
            visual.SetActive(true);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            if (withWing != null) Object.Destroy(withWing);
            if (withoutWing != null) Object.Destroy(withoutWing);
        }
    }

    private object Read(string name)
    {
        var field = typeof(PlayerMovement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing player field: " + name);
        return field.GetValue(player);
    }

    private void RecordFlight(int sample, string phase, int tick)
    {
        flightDiagnostics.AppendLine(System.FormattableString.Invariant(
            $"{sample},{phase},{tick},{Time.time:R},{body.position.y - 1f:R},{body.linearVelocity.y:R},{Read("state")},{readerFocus.GetValue(reader)},{Application.isFocused},{keyboard.spaceKey.isPressed},{Physics.simulationMode},{InputSystem.settings.updateMode}"));
    }

    private void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
}
