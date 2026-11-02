using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SkyBout.Online;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Real NGO/UTP direct loopback tests, deliberately not an internet/Relay acceptance claim.
public class HostSpawnTests
{
    private GameObject hostRoot, clientRoot, cameraRoot;
    private NetworkManager host, client;
    private GameObject prefab;

    [UnityTest]
    public IEnumerator HostHasOneLocalPlayerAndLateClientGetsCurrentEliminatedState()
    {
#if UNITY_EDITOR
        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        Assert.That(prefab, Is.Not.Null);
        // The authored, idle NetworkManager may already own the static Singleton; never start it here.
        host = CreatePeer("Host", out hostRoot);
        var transport = host.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
        host.ConnectionApprovalCallback = (request, response) =>
        {
            response.Approved = host.ConnectedClients.Count < 2;
            response.CreatePlayerObject = true;
            response.Position = new Vector3(request.ClientNetworkId == 0 ? -2 : 2, 3, 0);
        };
        Assert.That(host.StartHost(), Is.True);
        for (int i = 0; i < 120 && host.LocalClient?.PlayerObject == null; i++) yield return null;
        Assert.That(host.LocalClient?.PlayerObject, Is.Not.Null);
        var local = host.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
        Assert.That(local.HasLocalControl, Is.True);
        Assert.That(local.GetComponent<Rigidbody>().isKinematic, Is.False);
        local.SendMessage("EliminatePlayer");
        yield return new WaitForFixedUpdate();
        client = CreatePeer("Client", out clientRoot);
        client.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", transport.GetLocalEndpoint().Port);
        Assert.That(client.StartClient(), Is.True);
        for (int i = 0; i < 300 && (!client.IsConnectedClient || client.SpawnManager.SpawnedObjects.Count < 2); i++) yield return null;
        Assert.That(client.IsConnectedClient, Is.True);
        Assert.That(client.SpawnManager.SpawnedObjects.Count, Is.EqualTo(2));
        foreach (var networkObject in client.SpawnManager.SpawnedObjects.Values)
        {
            var movement = networkObject.GetComponent<PlayerMovement>();
            Assert.That(movement.HasLocalControl, Is.EqualTo(networkObject.IsOwner));
            Assert.That(movement.GetComponent<Rigidbody>().isKinematic, Is.EqualTo(!networkObject.IsOwner));
            if (!networkObject.IsOwner) Assert.That(movement.IsEliminated, Is.True, "Late join must initialize current state, not wait for a new event.");
        }
        Assert.That(host.ConnectedClients.Count, Is.EqualTo(2));
#else
        Assert.Ignore("Prefab integration fixture requires the Editor.");
        yield break;
#endif
    }

    private NetworkManager CreatePeer(string name, out GameObject root)
    {
        root = new GameObject("OnlineTest" + name); root.SetActive(false);
        var transport = root.AddComponent<UnityTransport>();
        var manager = root.AddComponent<NetworkManager>();
        manager.NetworkConfig = new Unity.Netcode.NetworkConfig();
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.EnableSceneManagement = false;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.PlayerPrefab = prefab;
        manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
        cameraRoot = new GameObject("OnlineTestCamera" + name); cameraRoot.SetActive(false);
        cameraRoot.AddComponent<Camera>();
        var follow = cameraRoot.AddComponent<TopDownCameraFollow>(); follow.ClearTarget(); cameraRoot.SetActive(true);
        var bootstrap = root.AddComponent<OnlineRoomBootstrap>();
        Set(bootstrap, "manager", manager); Set(bootstrap, "cameraFollow", follow);
        root.SetActive(true);
        return manager;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (client != null) OnlineRoomBootstrap.For(client)?.CameraFollow.ClearTarget();
        if (host != null) OnlineRoomBootstrap.For(host)?.CameraFollow.ClearTarget();
        if (client != null && client.IsListening) client.Shutdown();
        if (host != null && host.IsListening) host.Shutdown();
        for (int i = 0; i < 5; i++) yield return null;
        if (clientRoot != null) Object.DestroyImmediate(clientRoot);
        if (hostRoot != null) Object.DestroyImmediate(hostRoot);
        foreach (var view in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (view.name.StartsWith("OnlineTestCamera")) Object.DestroyImmediate(view.gameObject);
    }

    private static void Set(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
}
