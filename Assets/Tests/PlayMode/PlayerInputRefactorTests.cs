using System;
using System.Collections;
using System.Collections.Generic;
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

public class PlayerInputRefactorTests
{
#if UNITY_EDITOR
    private const string SourceAssetPath = "Assets/Player_Actions.inputactions";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [Test]
    public void GeneratedActionsMatchSourceContract()
    {
        var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SourceAssetPath);
        Assert.That(source, Is.Not.Null, SourceAssetPath);

        using (var generated = new PlayerInputActions())
        {
            var mismatches = CollectMismatches(source, generated.asset);
            Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
        }
    }

    [Test]
    public void ContractComparisonRejectsRenamedActionAndChangedBinding()
    {
        var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SourceAssetPath);
        Assert.That(source, Is.Not.Null, SourceAssetPath);

        var renamed = UnityEngine.Object.Instantiate(source);
        renamed.name = source.name;
        try
        {
            renamed.FindAction("Move", true).Rename("MoveRenamed");
            Assert.That(CollectMismatches(source, renamed), Is.Not.Empty,
                "A renamed action must be rejected.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(renamed);
        }

        var changed = UnityEngine.Object.Instantiate(source);
        changed.name = source.name;
        try
        {
            changed.FindAction("Jump", true).ChangeBinding(0).WithPath("<Keyboard>/enter");
            Assert.That(CollectMismatches(source, changed), Is.Not.Empty,
                "A changed binding must be rejected.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(changed);
        }

        using (var missing = new PlayerInputActions())
        {
            missing.Player.Jump.ChangeBinding(0).Erase();
            Assert.That(CollectMismatches(source, missing.asset), Has.Some.Contains("binding count"),
                "A missing binding must be rejected, independently of asset name.");
        }
    }

    [TestCase("neutral")]
    [TestCase("reactivate")]
    public void InputBoundariesUseActualReleaseAndSingleRequests(string boundary)
    {
        var simulation = Physics.simulationMode;
        var update = InputSystem.settings.updateMode;
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard = null;
        Mouse mouse = null;
        GameObject player = null, floor = null;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(400f, -0.5f, 400f);
            floor.transform.localScale = new Vector3(100f, 1f, 100f);
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.SetActive(false);
            player.transform.position = new Vector3(400f, 1f, 400f);
            var body = player.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            var foot = new GameObject("GroundCheck").transform;
            foot.SetParent(player.transform, false);
            foot.localPosition = Vector3.down;
            var movement = player.AddComponent<PlayerMovement>();
            Field("groundCheckTransform").SetValue(movement, foot);
            Field("groundLayer").SetValue(movement, (LayerMask)(1 << floor.layer));
            player.SetActive(true);
            var actions = (PlayerInputActions)Field("actions").GetValue(movement);
            actions.devices = new InputDevice[] { keyboard, mouse };
            movement.SendMessage("OnApplicationFocus", true);
            Physics.SyncTransforms();

            void Press(params Key[] keys)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                InputSystem.Update();
            }
            void Step(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    movement.SendMessage("FixedUpdate");
                    Physics.Simulate(Time.fixedDeltaTime);
                }
            }

            Press(); Step(5);
            if (boundary == "neutral")
            {
                for (int sample = 0; sample < 5; sample++)
                {
                    movement.SendMessage("OnApplicationFocus", false);
                    Press(Key.W, Key.S);
                    movement.SendMessage("OnApplicationFocus", true);
                    InputSystem.Update(); Step(5);
                    Assert.That(actions.Player.Move.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
                    Assert.That((bool)Field("waitingForNeutral").GetValue(movement), Is.True);
                    Press(Key.W); Step(5);
                    Assert.That(body.linearVelocity.z, Is.Zero, "Opposing held keys are not neutral.");
                    Press();
                    Assert.That((bool)Field("waitingForNeutral").GetValue(movement), Is.False);
                    Press(Key.W); Step(1);
                    Assert.That(body.linearVelocity.z, Is.GreaterThan(0f));
                    Press(); Step(10);
                }
            }
            else
            {
                for (int cycle = 0; cycle < 20; cycle++)
                {
                    movement.enabled = false;
                    movement.enabled = true;
                    movement.SendMessage("OnApplicationFocus", true);
                }
                Press(); Step(5);
                Press(Key.Space); Press();
                Assert.That((bool)Field("pendingJump").GetValue(movement), Is.True);
                Step(1);
                float launch = body.linearVelocity.y;
                Assert.That(launch, Is.GreaterThan(0f));
                Assert.That((bool)Field("pendingJump").GetValue(movement), Is.False);
                Step(1);
                Assert.That(body.linearVelocity.y, Is.LessThan(launch));
                Step(160);
                Assert.That(Field("state").GetValue(movement).ToString(), Is.EqualTo("Grounded"));

                body.position = new Vector3(460f, -10f, 400f);
                player.transform.position = body.position;
                Physics.SyncTransforms(); Step(1);
                Assert.That(movement.IsEliminated, Is.True);
                for (int cycle = 0; cycle < 20; cycle++)
                {
                    movement.enabled = false;
                    movement.enabled = true;
                    movement.SendMessage("OnApplicationFocus", true);
                }
                Press(); Press(Key.R); Press();
                Assert.That((bool)Field("pendingRestart").GetValue(movement), Is.True);
                Step(1);
                Assert.That(movement.IsEliminated, Is.False);
                Assert.That((bool)Field("pendingRestart").GetValue(movement), Is.False);
                Assert.That(Vector3.Distance(body.position, new Vector3(400f, 1f, 400f)), Is.LessThan(0.03f));
                Press(Key.D); Step(10);
                Assert.That(body.position.x, Is.GreaterThan(400f));
                Assert.That((bool)Field("pendingRestart").GetValue(movement), Is.False);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(floor);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Physics.simulationMode = simulation;
            InputSystem.settings.updateMode = update;
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
        }
    }

    private static FieldInfo Field(string name)
    {
        var field = typeof(PlayerMovement).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return field;
    }

    [UnityTest]
    public IEnumerator SampleScenePlayerHasNoPlayerInputOrMissingScripts()
    {
        Scene existing = SceneManager.GetSceneByPath(SampleScenePath);
        bool alreadyLoaded = existing.IsValid() && existing.isLoaded;
        Scene scene = existing;
        if (!alreadyLoaded)
        {
            yield return SceneManager.LoadSceneAsync(SampleScenePath, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(SampleScenePath);
        }

        Assert.That(scene.IsValid() && scene.isLoaded, Is.True, SampleScenePath);
        GameObject player = FindPlayer(scene);
        Assert.That(player, Is.Not.Null, "SampleScene Player must exist.");
        Assert.That(player.GetComponent<PlayerMovement>(), Is.Not.Null);
        Assert.That(player.GetComponent<UnityEngine.InputSystem.PlayerInput>(), Is.Null,
            "PlayerInput must be removed from the scene player.");

        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                Assert.That(component, Is.Not.Null, "Missing script under " + root.name);

        if (!alreadyLoaded)
            yield return SceneManager.UnloadSceneAsync(scene);
    }

    private static GameObject FindPlayer(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "Player")
                    return candidate.gameObject;
        return null;
    }

    internal static List<string> CollectMismatches(InputActionAsset expected, InputActionAsset actual)
    {
        var mismatches = new List<string>();
        if (expected == null)
        {
            mismatches.Add("expected asset is missing");
            return mismatches;
        }

        if (actual == null)
        {
            mismatches.Add("actual asset is missing");
            return mismatches;
        }

        CompareString(mismatches, "asset name", expected.name, actual.name);
        if (expected.actionMaps.Count != actual.actionMaps.Count)
            mismatches.Add("map count expected " + expected.actionMaps.Count + " but was " + actual.actionMaps.Count);

        foreach (var expectedMap in expected.actionMaps)
        {
            var actualMap = actual.FindActionMap(expectedMap.name, false);
            string mapLabel = "map " + expectedMap.name;
            if (actualMap == null)
            {
                mismatches.Add(mapLabel + " is missing");
                continue;
            }

            if (expectedMap.id != actualMap.id)
                mismatches.Add(mapLabel + " id expected " + expectedMap.id + " but was " + actualMap.id);
            if (expectedMap.actions.Count != actualMap.actions.Count)
                mismatches.Add(mapLabel + " action count expected " + expectedMap.actions.Count
                    + " but was " + actualMap.actions.Count);

            foreach (var expectedAction in expectedMap.actions)
            {
                var actualAction = actualMap.FindAction(expectedAction.name, false);
                string actionLabel = mapLabel + "/" + expectedAction.name;
                if (actualAction == null)
                {
                    mismatches.Add(actionLabel + " is missing");
                    continue;
                }

                if (expectedAction.id != actualAction.id)
                    mismatches.Add(actionLabel + " id expected " + expectedAction.id + " but was " + actualAction.id);
                if (expectedAction.type != actualAction.type)
                    mismatches.Add(actionLabel + " type expected " + expectedAction.type + " but was " + actualAction.type);
                CompareString(mismatches, actionLabel + " expectedControlType",
                    expectedAction.expectedControlType, actualAction.expectedControlType);
                if (expectedAction.wantsInitialStateCheck != actualAction.wantsInitialStateCheck)
                    mismatches.Add(actionLabel + " initialStateCheck expected "
                        + expectedAction.wantsInitialStateCheck + " but was " + actualAction.wantsInitialStateCheck);
            }

            if (expectedMap.bindings.Count != actualMap.bindings.Count)
                mismatches.Add(mapLabel + " binding count expected " + expectedMap.bindings.Count
                    + " but was " + actualMap.bindings.Count);

            int bindingCount = Math.Min(expectedMap.bindings.Count, actualMap.bindings.Count);
            for (int i = 0; i < bindingCount; i++)
            {
                InputBinding expectedBinding = expectedMap.bindings[i];
                InputBinding actualBinding = actualMap.bindings[i];
                string bindingLabel = mapLabel + " binding[" + i + "]";
                CompareString(mismatches, bindingLabel + " name", expectedBinding.name, actualBinding.name);
                if (expectedBinding.id != actualBinding.id)
                    mismatches.Add(bindingLabel + " id expected " + expectedBinding.id + " but was " + actualBinding.id);
                CompareString(mismatches, bindingLabel + " path", expectedBinding.path, actualBinding.path);
                CompareString(mismatches, bindingLabel + " groups", expectedBinding.groups, actualBinding.groups);
                CompareString(mismatches, bindingLabel + " action", expectedBinding.action, actualBinding.action);
                if (expectedBinding.isComposite != actualBinding.isComposite)
                    mismatches.Add(bindingLabel + " isComposite expected " + expectedBinding.isComposite
                        + " but was " + actualBinding.isComposite);
                if (expectedBinding.isPartOfComposite != actualBinding.isPartOfComposite)
                    mismatches.Add(bindingLabel + " isPartOfComposite expected " + expectedBinding.isPartOfComposite
                        + " but was " + actualBinding.isPartOfComposite);
            }
        }

        if (expected.controlSchemes.Count != actual.controlSchemes.Count)
            mismatches.Add("control scheme count expected " + expected.controlSchemes.Count
                + " but was " + actual.controlSchemes.Count);

        foreach (var expectedScheme in expected.controlSchemes)
        {
            bool found = false;
            InputControlScheme actualScheme = default;
            foreach (var candidate in actual.controlSchemes)
            {
                if (candidate.name != expectedScheme.name)
                    continue;
                actualScheme = candidate;
                found = true;
                break;
            }

            string schemeLabel = "scheme " + expectedScheme.name;
            if (!found)
            {
                mismatches.Add(schemeLabel + " is missing");
                continue;
            }

            CompareString(mismatches, schemeLabel + " bindingGroup",
                expectedScheme.bindingGroup, actualScheme.bindingGroup);
            if (expectedScheme.deviceRequirements.Count != actualScheme.deviceRequirements.Count)
                mismatches.Add(schemeLabel + " device count expected "
                    + expectedScheme.deviceRequirements.Count + " but was "
                    + actualScheme.deviceRequirements.Count);

            int deviceCount = Math.Min(expectedScheme.deviceRequirements.Count,
                actualScheme.deviceRequirements.Count);
            for (int i = 0; i < deviceCount; i++)
            {
                var expectedDevice = expectedScheme.deviceRequirements[i];
                var actualDevice = actualScheme.deviceRequirements[i];
                string deviceLabel = schemeLabel + " device[" + i + "]";
                CompareString(mismatches, deviceLabel + " controlPath",
                    expectedDevice.controlPath, actualDevice.controlPath);
                if (expectedDevice.isOptional != actualDevice.isOptional)
                    mismatches.Add(deviceLabel + " isOptional expected " + expectedDevice.isOptional
                        + " but was " + actualDevice.isOptional);
                if (expectedDevice.isOR != actualDevice.isOR)
                    mismatches.Add(deviceLabel + " isOR expected " + expectedDevice.isOR
                        + " but was " + actualDevice.isOR);
            }
        }

        return mismatches;
    }

    private static void CompareString(List<string> mismatches, string label, string expected, string actual)
    {
        if (!string.Equals(expected ?? string.Empty, actual ?? string.Empty, StringComparison.Ordinal))
            mismatches.Add(label + " expected '" + expected + "' but was '" + actual + "'");
    }
#endif
}

