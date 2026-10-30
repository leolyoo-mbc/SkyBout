using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RestartSceneTests
{
    private Scene previousScene, scene;
    private Keyboard keyboard;
    private Mouse mouse;
    private InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
    private PlayerMovement player;
    private Rigidbody body;
    private Camera camera;

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
        mouse = InputSystem.AddDevice<Mouse>();
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
        var input = (PlayerInputActions)Read(player, "actions");
        input.devices = new InputDevice[] { keyboard, mouse };
        player.SendMessage("OnApplicationFocus", true);
        Press();
        for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
#else
        Assert.Ignore("SampleScene integration requires Unity Editor.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
    }

    [UnityTest]
    public IEnumerator TwentyNaturalFallsRestartSameInstancesWithImmediateCameraAndReadableOverlay()
    {
        int playerId = player.GetInstanceID(), cameraId = camera.GetInstanceID();
        int rootCount = scene.rootCount;
        Vector3 start = body.position;
        Quaternion rotation = body.rotation;
        var follow = camera.GetComponent<TopDownCameraFollow>();
        Assert.That(Read(follow, "target"), Is.EqualTo(player.transform));
        Assert.That(Read(player, "cameraFollow"), Is.EqualTo(follow));
        Assert.That((float)Read(player, "fallDeathHeight"), Is.EqualTo(-10f));
        Assert.That(Read(player, "state").ToString(), Is.EqualTo("Grounded"));
        System.IO.Directory.CreateDirectory("TestResults");
        System.IO.File.WriteAllText("TestResults/restart-timings.txt", "Same SampleScene instance, natural falls and restart\n");
        for (int sample = 0; sample < 20; sample++)
        {
            // Exercise normal jump/landing before each fall, not just teleported state resets.
            Press(); yield return null;
            Press(Key.Space); yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Press();
            for (int i = 0; i < 160 && Read(player, "state").ToString() != "Grounded"; i++) yield return new WaitForFixedUpdate();
            Assert.That(Read(player, "state").ToString(), Is.EqualTo("Grounded"));
            body.position = new Vector3(17.5f, 1f, 0f);
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            Press(Key.D);
            for (int i = 0; i < 200 && !player.IsEliminated; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.True);
            float diedAt = Time.realtimeSinceStartup;
            Press();
            if (sample == 9)
            {
                follow.enabled = false;
                body.position += Vector3.right * 1000f;
                player.transform.position = body.position;
                Physics.SyncTransforms();
                Assert.That(camera.WorldToViewportPoint(body.position).x, Is.GreaterThan(1f));
            }
            yield return new WaitForEndOfFrame();
            float visibleAfter = Time.realtimeSinceStartup - diedAt;
            Assert.That(visibleAfter, Is.LessThan(1f));
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(WhitePanelPixels(texture), Is.GreaterThan(40), "Readable high contrast text must render.");
                if (sample == 0 || sample == 9)
                    System.IO.File.WriteAllBytes("TestResults/restart-overlay-" + sample + ".png", texture.EncodeToPNG());
            }
            finally { Object.Destroy(texture); }
            follow.enabled = true;
            float requestedAt = Time.realtimeSinceStartup;
            Press(Key.R);
            for (int i = 0; i < 100 && player.IsEliminated; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.False);
            Assert.That(Time.realtimeSinceStartup - requestedAt, Is.LessThan(2f));
            Press();
            yield return new WaitForEndOfFrame();
            var restoredFrame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                AssertNoOverlayPanel(restoredFrame);
                if (sample == 0 || sample == 9)
                    System.IO.File.WriteAllBytes("TestResults/restart-restored-" + sample + ".png", restoredFrame.EncodeToPNG());
            }
            finally { Object.Destroy(restoredFrame); }
            Vector3 expected = player.transform.position - camera.transform.forward * (float)Read(follow, "distance");
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.1f));
            Vector3 viewport = camera.WorldToViewportPoint(player.transform.position);
            Assert.That(viewport.z, Is.GreaterThan(0f));
            Assert.That(viewport.x, Is.InRange(0.1f, 0.9f));
            Assert.That(viewport.y, Is.InRange(0.1f, 0.9f));
            Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.04f));
            Assert.That(Quaternion.Angle(body.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(player.GetInstanceID(), Is.EqualTo(playerId));
            Assert.That(camera.GetInstanceID(), Is.EqualTo(cameraId));
            Assert.That(scene.rootCount, Is.EqualTo(rootCount));
            Assert.That(Read(follow, "target"), Is.EqualTo(player.transform));
            Assert.That((float)Read(player, "fallDeathHeight"), Is.EqualTo(-10f));
            System.IO.File.AppendAllText("TestResults/restart-timings.txt",
                $"sample={sample}, overlay={visibleAfter:F4}s, restart={Time.realtimeSinceStartup - requestedAt:F4}s\n");
            if (sample < 10)
            {
                yield return new WaitForSeconds(2f);
                Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.04f));
                Assert.That(Read(player, "state").ToString(), Is.EqualTo("Grounded"));
                Assert.That(player.IsEliminated, Is.False);
            }
        }
    }

    [UnityTest]
    public IEnumerator FocusAndHeldKeysDoNotReviveOrJumpWithoutFreshInput()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            body.position = new Vector3(20f, -10f, 0f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.True);
            player.SendMessage("OnApplicationFocus", false);
            Press(Key.R, Key.Space);
            yield return null;
            player.SendMessage("OnApplicationFocus", true);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.True);
            Press(Key.Space); yield return null;
            Press(Key.Space, Key.R);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsEliminated, Is.False);
            Assert.That(Read(player, "state").ToString(), Is.EqualTo("Grounded"));
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f));
            Press(); yield return null;
            Press(Key.Space); yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Press(); yield return null;
        }
    }

    private static int WhitePanelPixels(Texture2D texture)
    {
        int width = Mathf.Min(380, (int)(texture.width * 0.8f));
        int height = Mathf.Min(80, texture.height);
        var pixels = texture.GetPixels((texture.width - width) / 2, (texture.height - height) / 2, width, height);
        int white = 0, black = 0;
        foreach (var pixel in pixels)
        {
            if (pixel.r > 0.85f && pixel.g > 0.85f && pixel.b > 0.85f) white++;
            if (pixel.r < 0.08f && pixel.g < 0.08f && pixel.b < 0.08f) black++;
        }
        Assert.That(black, Is.GreaterThan(pixels.Length / 2));
        return white;
    }

    private static void AssertNoOverlayPanel(Texture2D texture)
    {
        // Same area as the eliminated frame; the opaque dark panel must be gone.
        int width = Mathf.Min(380, (int)(texture.width * 0.8f));
        int height = Mathf.Min(80, texture.height);
        var pixels = texture.GetPixels((texture.width - width) / 2, (texture.height - height) / 2, width, height);
        int black = 0;
        foreach (var pixel in pixels)
            if (pixel.r < 0.08f && pixel.g < 0.08f && pixel.b < 0.08f) black++;
        Assert.That(black, Is.LessThan(pixels.Length / 2), "The first restored Game view must not retain the elimination panel.");
    }

    private static object Read(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return field.GetValue(target);
    }
    private void Press(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
}
