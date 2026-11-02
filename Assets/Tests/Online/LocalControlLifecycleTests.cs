using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class LocalControlLifecycleTests
{
    [UnityTest]
    public IEnumerator NetworkReplicaStartsWithoutInputOrDynamicPhysics()
    {
        var scene = SceneManager.CreateScene("OnlineControlFixture");
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        SceneManager.MoveGameObjectToScene(player, scene);
        player.SetActive(false);
        var body = player.AddComponent<Rigidbody>();
        var feet = new GameObject("Feet").transform;
        feet.SetParent(player.transform, false);
        feet.localPosition = Vector3.down;
        var movement = player.AddComponent<PlayerMovement>();
        try
        {
            Set(movement, "feetTransform", feet);
            Set(movement, "groundLayer", (LayerMask)(1 << LayerMask.NameToLayer("Ground")));
            var field = typeof(PlayerMovement).GetField("requiresLocalBinding", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Online prefab needs a pre-Awake input ownership gate.");
            field.SetValue(movement, true);
            player.SetActive(true);
            yield return null;
            var actions = (PlayerInputActions)Get(movement, "playerInput");
            Assert.That(actions.Player.enabled, Is.False);
            Assert.That(body.isKinematic, Is.True);
            var bind = typeof(PlayerMovement).GetMethod("BindLocalControl");
            Assert.That(bind, Is.Not.Null);
            bind.Invoke(movement, new object[] { null, new Vector3(2f, 1f, 0f), Quaternion.identity });
            Assert.That(actions.Player.enabled, Is.True);
            Assert.That(body.isKinematic, Is.False);
            Assert.That((Vector3)Get(movement, "startingPosition"), Is.EqualTo(new Vector3(2f, 1f, 0f)));
            typeof(PlayerMovement).GetMethod("SetControlSuspended").Invoke(movement, new object[] { true });
            Assert.That(actions.Player.enabled, Is.False);
            typeof(PlayerMovement).GetMethod("SetControlSuspended").Invoke(movement, new object[] { false });
            Assert.That(actions.Player.enabled, Is.True);
            typeof(PlayerMovement).GetMethod("UnbindLocalControl").Invoke(movement, null);
            Assert.That(actions.Player.enabled, Is.False);
            Assert.That(body.isKinematic, Is.True);
        }
        finally { Object.DestroyImmediate(player); }
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator CameraCanWaitBindAndUnbindWithoutErrors()
    {
        var go = new GameObject("OnlineCamera", typeof(Camera));
        var target = new GameObject("OnlineTarget");
        go.SetActive(false);
        var follow = go.AddComponent<TopDownCameraFollow>();
        try
        {
            var wait = typeof(TopDownCameraFollow).GetMethod("ClearTarget");
            Assert.That(wait, Is.Not.Null, "Camera needs an explicit unbound waiting state.");
            wait.Invoke(follow, null);
            go.SetActive(true);
            yield return null;
            Assert.That(follow.enabled, Is.True);
            typeof(TopDownCameraFollow).GetMethod("BindTarget").Invoke(follow, new object[] { target.transform });
            yield return null;
            Assert.That(Get(follow, "target"), Is.EqualTo(target.transform));
            wait.Invoke(follow, null);
            yield return null;
            Assert.That(follow.enabled, Is.True);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(target); }
    }

    private static object Get(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
}
