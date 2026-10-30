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

public class RestartTests
{
    private GameObject player, ground;
    private Rigidbody body;
    private PlayerMovement movement;
    private PlayerInputActions actions;
    private Keyboard keyboard;
    private Mouse mouse;
    private Scene previousScene, testScene;
    private SimulationMode simulation;
    private InputSettings.UpdateMode updateMode;
    private InputSettings.BackgroundBehavior background;
#if UNITY_EDITOR
    private InputSettings.EditorInputBehaviorInPlayMode editorInput;
#endif
    private readonly Vector3 start = new Vector3(200f, 1f, 200f);
    private readonly Quaternion rotation = Quaternion.Euler(0f, 35f, 0f);

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("RestartTests");
        SceneManager.SetActiveScene(testScene);
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
        mouse = InputSystem.AddDevice<Mouse>();
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = start + Vector3.down * 2f;
        ground.transform.localScale = new Vector3(32f, 2f, 32f);
        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.SetActive(false);
        player.transform.SetPositionAndRotation(start, rotation);
        body = player.AddComponent<Rigidbody>();
        body.constraints = RigidbodyConstraints.FreezeRotation;
        var foot = new GameObject("Foot").transform;
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
        Press(); Step(5);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        Physics.simulationMode = simulation;
        InputSystem.settings.updateMode = updateMode;
        InputSystem.settings.backgroundBehavior = background;
#if UNITY_EDITOR
        InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
#endif
        if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
        yield return SceneManager.UnloadSceneAsync(testScene);
    }

    [Test]
    public void RestartBindingIsKeyboardButton()
    {
        var action = actions.Player.Restart;
        Assert.That(action.type, Is.EqualTo(InputActionType.Button));
        Assert.That(action.wantsInitialStateCheck, Is.False);
        Assert.That(action.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/r"));
    }

    [Test]
    public void TenRestartsRestorePoseVelocityAndActualSupport()
    {
        int id = player.GetInstanceID();
        for (int sample = 0; sample < 10; sample++)
        {
            Eliminate();
            body.rotation = Quaternion.Euler(0f, 120f, 0f);
            body.angularVelocity = Vector3.up;
            Press(Key.R);
            movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.False);
            Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(body.rotation, rotation), Is.LessThan(0.01f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Press(); Step(Mathf.CeilToInt(2f / Time.fixedDeltaTime));
            Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.04f));
            Assert.That(Grounded(), Is.True);
            Assert.That(player.GetInstanceID(), Is.EqualTo(id));
            Press(Key.D); Step(10);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Press(); Step(10);
            Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            Press();
            for (int i = 0; i < 160 && !Grounded(); i++) Step();
            Assert.That(Grounded(), Is.True);
        }
    }

    [Test]
    public void AfterRestartBoundaryStillUsesStrictFeetComparison()
    {
        for (int relation = -1; relation <= 1; relation++)
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate(); Restart();
            Teleport(new Vector3(220f, -9f + relation * 0.01f, 200f));
            movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.EqualTo(relation < 0));
        }
    }

    [Test]
    public void MissingRestartActionIsRejected()
    {
#if UNITY_EDITOR
        var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Player_Actions.inputactions");
        using (var changed = new PlayerInputActions())
        {
            changed.Player.Restart.RemoveAction();
            Assert.That(PlayerInputRefactorTests.CollectMismatches(source, changed.asset),
                Has.Some.Contains("/Restart is missing"));
        }
#endif
    }

    [Test]
    public void LivingAndHeldRestartNeverQueuesForFutureElimination()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Press(Key.R); Step(5);
            Assert.That(movement.IsEliminated, Is.False);
            Assert.That(Vector3.Distance(body.position, start), Is.LessThan(0.04f));
            Teleport(new Vector3(220f, 4f, 200f));
            Step(3);
            Press(); Press(Key.R); Step();
            Assert.That(body.position.x, Is.GreaterThan(219f), "Living airborne R must not teleport.");
            Teleport(new Vector3(220f, -9.01f, 200f));
            movement.SendMessage("FixedUpdate");
            Step(5);
            Assert.That(movement.IsEliminated, Is.True, "Held R must not revive after death.");
            Restart();
        }
    }

    [Test]
    public void RestartHoldAndRepeatedPressesCannotResetLivingPosition()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate();
            Press(Key.R); Step(5);
            Assert.That(movement.IsEliminated, Is.False);
            Teleport(start + Vector3.right * 3f);
            Step(5);
            for (int i = 0; i < 10; i++)
            {
                Press(); Press(Key.R); Step();
                Assert.That(body.position.x, Is.EqualTo(start.x + 3f).Within(0.04f));
            }
        }
    }

    [TestCase("focus")]
    [TestCase("disable")]
    public void PendingRestartIsDiscardedAcrossLifecycleChange(string change)
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate();
            Press(Key.R);
            if (change == "focus") movement.SendMessage("OnApplicationFocus", false);
            else movement.enabled = false;
            Press();
            if (change == "focus") movement.SendMessage("OnApplicationFocus", true);
            else
            {
                movement.enabled = true;
                movement.SendMessage("OnApplicationFocus", true);
            }
            Press(); Step(5);
            Assert.That(movement.IsEliminated, Is.True, "A stale request must not survive lifecycle changes.");
            Restart();
        }
    }

    [Test]
    public void FocusReturnWithHeldRestartRequiresReleaseAndFreshPress()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate();
            movement.SendMessage("OnApplicationFocus", false);
            Press(Key.R, Key.Space);
            movement.SendMessage("OnApplicationFocus", true);
            Press(Key.R, Key.Space); Step(5);
            Assert.That(movement.IsEliminated, Is.True);
            Restart();
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f));
        }
    }

    [Test]
    public void HeldMovementResumesButHeldSpaceNeverAutoJumps()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate();
            Press(Key.D, Key.Space);
            Press(Key.D, Key.Space, Key.R); Step(10);
            Assert.That(movement.IsEliminated, Is.False);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(Grounded(), Is.True);
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f));
            Press(); Step(10);
            Press(Key.Space); Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }
    }

    [Test]
    public void ShortRestartPressIsConsumedOnceAndDeathWinsInputCollision()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update(); Step(5);
            Assert.That(movement.IsEliminated, Is.False, "A short valid press must not be lost.");
            Teleport(new Vector3(220f, -9.01f, 200f));
            Press(Key.R);
            movement.SendMessage("FixedUpdate");
            Assert.That(movement.IsEliminated, Is.True, "New death wins same-step R.");
            Step(5);
            Assert.That(movement.IsEliminated, Is.True);
            Restart();
        }
    }

    [Test]
    public void FiveEliminationsBlockTenSecondsOfNonRestartInput()
    {
        for (int sample = 0; sample < 5; sample++)
        {
            Eliminate();
            for (int i = 0; i < Mathf.CeilToInt(10f / Time.fixedDeltaTime); i++)
            {
                Press(i % 2 == 0 ? new[] { Key.W, Key.Space } : new Key[0]); Step();
                Assert.That(movement.IsEliminated, Is.True);
                Assert.That(body.linearVelocity.x, Is.Zero);
                Assert.That(body.linearVelocity.z, Is.Zero);
            }
            Restart();
        }
    }

    private void Eliminate()
    {
        Press();
        Teleport(new Vector3(220f, -9.01f, 200f));
        body.linearVelocity = new Vector3(3f, -20f, 4f);
        movement.SendMessage("FixedUpdate");
        Assert.That(movement.IsEliminated, Is.True);
    }
    private void Restart()
    {
        Press(); Press(Key.R); Step(5); Press();
        Assert.That(movement.IsEliminated, Is.False);
        Assert.That(Grounded(), Is.True);
    }
    private void Teleport(Vector3 position)
    {
        body.position = position;
        player.transform.position = position;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }
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
    private bool Grounded() => Field("state").GetValue(movement).ToString() == "Grounded";
    private FieldInfo Field(string name) => typeof(PlayerMovement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private void SetField(string name, object value) => Field(name).SetValue(movement, value);
}
