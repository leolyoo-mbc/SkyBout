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

public class MovementAndCameraTests
{
    private GameObject player;
    private GameObject ground;
    private GameObject cameraObject;
    private Rigidbody body;
    private PlayerMovement movement;
    private PlayerInput playerInput;
    private InputActionAsset actions;
    private Keyboard keyboard;
    private Mouse mouse;
    private SimulationMode previousSimulation;
    private InputSettings.UpdateMode previousInputMode;
    private Scene previousScene;
    private Scene testScene;
    private readonly Vector3 start = new Vector3(200f, 1f, 200f);

    [SetUp]
    public void SetUp()
    {
        previousScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("SkyBoutMovementTests");
        SceneManager.SetActiveScene(testScene);
        previousSimulation = Physics.simulationMode;
        previousInputMode = InputSystem.settings.updateMode;
        Physics.simulationMode = SimulationMode.Script;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();

        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "TestGround";
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = new Vector3(200f, -0.5f, 200f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);

        player = new GameObject("TestPlayer");
        player.SetActive(false);
        player.transform.position = start;
        body = player.AddComponent<Rigidbody>();
        body.mass = 80f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        capsule.radius = 0.5f;
#if UNITY_EDITOR
        capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/PlayerFriction.physicMaterial");
        actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Player_Actions.inputactions"));
#endif
        Assert.That(actions, Is.Not.Null, "The actual scene input asset must be available in the Editor.");
        var foot = new GameObject("GroundCheck").transform;
        foot.SetParent(player.transform, false);
        foot.localPosition = Vector3.down;
        movement = player.AddComponent<PlayerMovement>();
        SetField(movement, "groundCheckTransform", foot);
        SetField(movement, "groundLayer", (LayerMask)(1 << ground.layer));
        playerInput = player.AddComponent<PlayerInput>();
        playerInput.actions = actions;
        playerInput.defaultActionMap = "Player";
        playerInput.notificationBehavior = PlayerNotifications.SendMessages;
        playerInput.neverAutoSwitchControlSchemes = true;
        player.SetActive(true);
        playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
        playerInput.ActivateInput();
        playerInput.SwitchCurrentActionMap("Player");
        movement.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
        Physics.SyncTransforms();
        Press();
        Step(5); // Actual contacts must exist before a grounded-start assertion.
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(player);
        Object.DestroyImmediate(ground);
        Object.DestroyImmediate(actions);
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
        Physics.simulationMode = previousSimulation;
        InputSystem.settings.updateMode = previousInputMode;
        if (previousScene.IsValid() && previousScene.isLoaded)
            SceneManager.SetActiveScene(previousScene);
        if (testScene.IsValid() && testScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(testScene);
    }

    [Test]
    public void StartupAccelerationDoesNotDependOnBraking()
    {
        SetRates(10f, 50f);
        Press(Key.W);
        Step();
        float first = body.linearVelocity.z;
        ResetPlayer();
        SetRates(10f, 100f);
        Press(Key.W);
        Step();
        Assert.That(body.linearVelocity.z, Is.EqualTo(first).Within(0.0001f), "Braking must not boost the first acceleration step.");
        Assert.That(first, Is.EqualTo(10f * Time.fixedDeltaTime).Within(0.0001f));
    }

    [Test]
    public void EightDirectionsShareTopSpeedAndOpposingKeysCancel()
    {
        Key[][] keys = { new[] { Key.W }, new[] { Key.S }, new[] { Key.A }, new[] { Key.D },
            new[] { Key.W, Key.D }, new[] { Key.W, Key.A }, new[] { Key.S, Key.D }, new[] { Key.S, Key.A } };
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) };
        for (int i = 0; i < keys.Length; i++)
        {
            ResetPlayer();
            Press(keys[i]);
            Step(60);
            Vector3 horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            Assert.That(horizontal.magnitude, Is.EqualTo(8f).Within(0.001f));
            Assert.That(Vector3.Dot(horizontal.normalized, directions[i].normalized), Is.GreaterThan(0.999f));
        }
        ResetPlayer();
        Press(Key.W, Key.S, Key.D);
        Step(60);
        Assert.That(body.linearVelocity.x, Is.GreaterThan(7.99f));
        Assert.That(Mathf.Abs(body.linearVelocity.z), Is.LessThan(0.001f));
        Press(Key.W, Key.S, Key.A, Key.D);
        Step(30);
        Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.LessThan(0.001f));
        Press(Key.W);
        Step(60);
        Press();
        Step(30);
        Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.LessThan(0.001f));
    }

    [Test]
    public void FocusRegainRequiresReleasingKeysBeforeMoving()
    {
        Press(Key.W);
        Step(60);
        movement.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
        Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
        movement.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
        InputSystem.Update();
        Step(10);
        Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
        Press();
        Press(Key.W);
        Step();
        Assert.That(body.linearVelocity.z, Is.GreaterThan(0f));
        movement.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
        Press();
        movement.SendMessage("OnApplicationFocus", true, SendMessageOptions.DontRequireReceiver);
        InputSystem.Update();
        Step();
        Assert.That(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude, Is.Zero);
        Press(Key.W);
        Step();
        Assert.That(body.linearVelocity.z, Is.GreaterThan(0f));
    }

    [Test]
    public void GroundedStateIsRefreshedInThePhysicsStepAndGravityIsPreserved()
    {
        Step();
        player.transform.position = start + Vector3.up * 5f;
        body.linearVelocity = new Vector3(0f, -3f, 0f);
        Physics.SyncTransforms();
        Assert.That(player.transform.GetChild(0).position.y, Is.EqualTo(5f).Within(0.001f));
        Press(Key.W);
        movement.SendMessage("FixedUpdate");
        Assert.That(body.linearVelocity.z, Is.EqualTo(10f * Time.fixedDeltaTime).Within(0.0001f));
        Assert.That(body.linearVelocity.y, Is.EqualTo(-3f).Within(0.0001f));
        Physics.Simulate(Time.fixedDeltaTime);
        Assert.That(body.linearVelocity.y, Is.LessThan(-3f));
    }

    [UnityTest]
    public IEnumerator CameraStartsOnTargetTracksAndIgnoresCursor()
    {
        cameraObject = new GameObject("TestCamera");
        cameraObject.SetActive(false);
        var camera = cameraObject.AddComponent<Camera>();
        var follow = cameraObject.AddComponent<TopDownCameraFollow>();
        SetField(follow, "target", player.transform);
        cameraObject.SetActive(true);
        yield return null;
        Assert.That(Vector3.Distance(camera.transform.position, player.transform.position), Is.EqualTo(18f).Within(0.001f));
        Vector3 initial = camera.transform.position;
        player.transform.position += Vector3.right * 8f;
        Physics.SyncTransforms();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10f, 10f) });
        InputSystem.Update();
        follow.SendMessage("LateUpdate");
        Vector3 firstFollow = camera.transform.position;
        Assert.That(firstFollow.x, Is.GreaterThan(initial.x), "LateUpdate must actually follow the moved target.");
        camera.transform.position = initial;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(900f, 300f) });
        InputSystem.Update();
        follow.SendMessage("LateUpdate");
        Assert.That(Vector3.Distance(camera.transform.position, firstFollow), Is.LessThan(0.0001f), "Cursor changes must not affect a moving camera either.");
        for (int i = 0; i < 60; i++)
            follow.SendMessage("LateUpdate");
        Assert.That(camera.transform.position.x, Is.GreaterThan(initial.x + 7f));
        camera.transform.position = player.transform.position - camera.transform.forward * 18f;
        Vector3 settled = camera.transform.position;
        Quaternion rotation = camera.transform.rotation;
        float fov = camera.fieldOfView;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(900f, 300f) });
        InputSystem.Update();
        follow.SendMessage("LateUpdate");
        Assert.That(Vector3.Distance(camera.transform.position, settled), Is.LessThan(0.0001f));
        Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(0.0001f));
        Assert.That(camera.fieldOfView, Is.EqualTo(fov));
    }

    [UnityTest]
    public IEnumerator CameraKeepsActualJumpAndNearbySurfaceVisible()
    {
        cameraObject = new GameObject("JumpCamera");
        cameraObject.SetActive(false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.aspect = 16f / 9f;
        var follow = cameraObject.AddComponent<TopDownCameraFollow>();
        SetField(follow, "target", player.transform);
        cameraObject.SetActive(true);
        yield return null;
        foreach (float height in new[] { 2f, 4f })
        {
            ResetPlayer();
            SetField(movement, "jumpHeight", height);
            Press(Key.Space);
            Step();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            bool rose = false, fell = false;
            for (int step = 0; step < 110; step++)
            {
                Step();
                rose |= body.linearVelocity.y > 0.1f;
                fell |= body.linearVelocity.y < -0.1f;
                follow.SendMessage("LateUpdate");
                Bounds bounds = player.GetComponent<Collider>().bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    AssertVisible(camera, point);
                }
                foreach (var offset in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                    AssertVisible(camera, new Vector3(player.transform.position.x, 0f, player.transform.position.z) + offset);
                Vector3 before = camera.transform.position;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(10, 10) });
                InputSystem.Update(); follow.SendMessage("LateUpdate");
                Vector3 first = camera.transform.position;
                camera.transform.position = before;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(900, 300) });
                InputSystem.Update(); follow.SendMessage("LateUpdate");
                Assert.That(Vector3.Distance(first, camera.transform.position), Is.LessThan(0.0001f));
            }
            Assert.That(rose && fell, Is.True);
            Press();
        }
    }

    private static void AssertVisible(Camera camera, Vector3 point)
    {
        Vector3 viewport = camera.WorldToViewportPoint(point);
        Assert.That(viewport.z, Is.GreaterThan(0f));
        Assert.That(viewport.x, Is.InRange(0f, 1f));
        Assert.That(viewport.y, Is.InRange(0f, 1f));
    }

    [Test]
    public void GroundMaskMustIncludeTheGroundLayer()
    {
        SetField(movement, "groundLayer", (LayerMask)(1 << LayerMask.NameToLayer("Water")));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("^PlayerMovement: check"));
        movement.SendMessage("OnValidate");
        Assert.That(movement.enabled, Is.False);
    }

    private void Press(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
        if (keys.Length == 1 && keys[0] != Key.Space)
        {
            Assert.That(keyboard[keys[0]].isPressed, Is.True, "The virtual keyboard event must be processed.");
            var move = playerInput.actions["Move"];
            Assert.That(move.ReadValue<Vector2>().sqrMagnitude, Is.GreaterThan(0f),
                $"Move enabled={move.enabled}, controls={move.controls.Count}, devices={playerInput.devices.Count}, active={playerInput.inputIsActive}, scheme={playerInput.currentControlScheme}");
        }
    }

    private void Step(int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            movement.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            movement.SendMessage("FixedUpdate");
            Physics.Simulate(Time.fixedDeltaTime);
        }
    }

    private void ResetPlayer()
    {
        Press();
        player.transform.position = start;
        body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms();
        Step(5);
    }

    private void SetRates(float acceleration, float braking)
    {
        SetField(movement, "acceleration", acceleration);
        SetField(movement, "deceleration", braking);
        SetField(movement, "turnAcceleration", braking);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}
