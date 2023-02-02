using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;
using UnityEngine.SceneManagement;

// Make sure this assembly is always linked when making debug builds.
#if DEBUG
[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]
#endif

namespace Needle.RemoteHierarchy
{
    public static class RemoteHierarchy
    {
        public static readonly Guid kSendRequestEditorToPlayer = new Guid("34d9b47f923142ff847c0d1f8b0554d9");
        public static readonly Guid kMsgSendPlayerToEditor = new Guid("12871ffeaf0c489189579946d8e0840f");
        public static readonly Guid kRequestHierarchyResponse = new Guid("d0e5b0e0-5f1a-4b1e-9b5a-f63e0f4c506f");
        public static readonly Guid kUpdateScene = new Guid("d0e5b0f0-5f1a-4b1e-9b5a-f63e0f4c526f");
        public static readonly Guid kCallMethods = new Guid("d0e5b0f1-5f1a-4b1e-9b5a-f63e0f4c536f");

        public const string HierarchyRequest = "hierarchy";
        public const string HierarchyWithComponentsRequest = "hierarchy-with-components";

        private static bool haveSetUp = false;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        internal static void Setup()
        {
            if (haveSetUp) return;
            haveSetUp = true;
            Log("Starting RemoteHierarchy");
            
            PlayerConnection.instance.RegisterConnection(OnConnectionEvent);
            PlayerConnection.instance.RegisterDisconnection(OnDisconnectionEvent);

            PlayerConnection.instance.Register(kSendRequestEditorToPlayer, OnRequest);
            PlayerConnection.instance.Register(kUpdateScene, OnChangeSet);
            PlayerConnection.instance.Register(kCallMethods, OnCallMethods);
            
            Log("Have registered RemoteHierarchy callbacks");
        }

        // TODO how to properly tear down after RuntimeInitializeOnLoadMethod?
        // private static void OnDestroy()
        // {
        //     PlayerConnection.instance.Unregister(kSendRequestEditorToPlayer, OnRequest);
        //     PlayerConnection.instance.Unregister(kUpdateScene, OnChangeSet);
        //     PlayerConnection.instance.Unregister(kCallMethods, OnCallMethods);
        // }

        private static void OnConnectionEvent(int playerId)
        {
            Log("Connection " + playerId);
        }

        private static void OnDisconnectionEvent(int playerId)
        {
            Log("Disconnection " + playerId);
        }

        private static void OnRequest(MessageEventArgs args)
        {
            var text = Encoding.UTF8.GetString(args.data);

            Log("Message from editor: " + text);

            switch (text)
            {
                case HierarchyRequest:
                    SendHierarchy(false);
                    break;
                case HierarchyWithComponentsRequest:
                    SendHierarchy(true);
                    break;
            }
        }

        private static void OnCallMethods(MessageEventArgs arg0)
        {
            var json = Encoding.UTF8.GetString(arg0.data);
            Debug.Log("Received method calls: " + json);
            var calls = JsonUtility.FromJson<RemoteCallInfo>(json);
            ProcessMethodCalls(calls);
        }
        
        private static void OnChangeSet(MessageEventArgs args)
        {
            var json = Encoding.UTF8.GetString(args.data);
            Debug.Log("Received changeset: " + json);
            var changeSet = JsonUtility.FromJson<SceneInfo>(json);
            ProcessChangeSet(changeSet);
        }

        public static void ProcessChangeSet(SceneInfo changeSet)
        {
            foreach (var entry in changeSet.children)
            {
                // find by instance ID and update active state and name
                var instanceId = entry.instanceId;
                if (instanceIdMap.TryGetValue(instanceId, out var pair))
                {
                    var go = pair.Item2;
                    go.SetActive(entry.active);
                    Debug.Log("Updated go - now: " + entry.active);
                }
            }
        }

        public static void ProcessMethodCalls(RemoteCallInfo remoteCallInfo)
        {
            foreach (var call in remoteCallInfo.calls)
            {
                var instanceId = call.gameObjectInstanceId;
                if (instanceIdMap.TryGetValue(instanceId, out var pair))
                {
                    var go = pair.Item2;
                    var component = go.GetComponent(Type.GetType(call.componentTypeName));
                    var method = component.GetType().GetMethod(call.methodName, (BindingFlags)(-1));
                    if (method != null)
                    {
                        if (call.parameters != null && call.parameters.Any())
                        {
                            var stringParameters = new object[call.parameters.Count];
                            for (int i = 0; i < call.parameters.Count; i++)
                                stringParameters[i] = call.parameters[i];
                            
                            method.Invoke(component, stringParameters);                            
                        }
                        else
                        {
                            method.Invoke(component, null);
                        }
                    }
                }
            }
        }

        [Serializable]
        public class HierarchyInfo
        {
            public List<SceneInfo> scenes = new List<SceneInfo>();
        }

        [Serializable]
        public class SceneInfo
        {
            public string name;
            public List<GameObjectInfo> children = new List<GameObjectInfo>();
        }

        [Serializable]
        public class RemoteCallInfo
        {
            public List<RemoteCall> calls = new List<RemoteCall>();
        }

        [Serializable]
        public class RemoteCall
        {
            public int gameObjectInstanceId;
            public string componentTypeName;
            public string methodName;
            public List<string> parameters;
        }

        [Serializable]
        public class ComponentInfo
        {
            public string name;
            public string typeName;

            public Type type => Type.GetType(typeName);

            public ComponentInfo(Component c)
            {
                name = c.GetType().Name;
                typeName = c.GetType().AssemblyQualifiedName;
            }

            public static ComponentInfo FromComponent(Component c)
            {
                return c switch
                {
                    MeshFilter _ => new MeshFilterInfo(c),
                    Behaviour _ => new BehaviourInfo(c),
                    _ => new ComponentInfo(c)
                };
            }
        }

        [Serializable]
        public class BehaviourInfo : ComponentInfo
        {
            public bool enabled;
            public BehaviourInfo(Component c) : base(c)
            {
                var b = c as Behaviour;
                enabled = b.enabled;
            }
        }

        [Serializable]
        public class MeshFilterInfo : ComponentInfo
        {
            public long vertexCount;
            public MeshFilterInfo(Component c) : base(c)
            {
                var mf = c as MeshFilter;
                vertexCount = mf.sharedMesh?.vertexCount ?? -1;
            }
        }

        [Serializable]
        public class TransformInfo
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        
        [Serializable]
        public class GameObjectInfo
        {
            public string name;
            public int instanceId;
            public bool active;
            [CanBeNull]
            public TransformInfo transform;
            [CanBeNull]
            public List<ComponentInfo> components;
            [CanBeNull]
            public List<GameObjectInfo> children;

            public static GameObjectInfo FromGameObject(GameObject go, bool captureComponents)
            {
                var info = new GameObjectInfo
                {
                    name = go.name,
                    active = go.activeSelf,
                    instanceId = go.GetInstanceID(),
                    transform = new TransformInfo() { position = go.transform.position, rotation = go.transform.rotation, scale = go.transform.localScale },
                };
                
                if (captureComponents)
                    info.components = go.GetComponents(typeof(Component)).Where(x => !(x is Transform)).Select(ComponentInfo.FromComponent).ToList();
                
                instanceIdMap.Add(info.instanceId, (info, go));

                if (go.transform.childCount > 0)
                {
                    info.children = new List<GameObjectInfo>();
                    for (int i = 0; i < go.transform.childCount; i++)
                    {
                        info.children.Add(FromGameObject(go.transform.GetChild(i).gameObject, captureComponents));
                    }
                }

                return info;
            }
        }

        private static void SendHierarchy(bool captureComponents)
        {
            PlayerConnection.instance.Send(kRequestHierarchyResponse,
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(GetHierarchy(captureComponents), true)));
        }

        private static Dictionary<int, (GameObjectInfo, GameObject)> instanceIdMap = new Dictionary<int, (GameObjectInfo, GameObject)>();
        private static List<GameObject> rootGameObjects = new List<GameObject>();

        public static HierarchyInfo GetHierarchy(bool captureComponents)
        {
            var hierarchy = new HierarchyInfo();
            instanceIdMap.Clear();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var sceneInfo = new SceneInfo() { name = scene.name };
                hierarchy.scenes.Add(sceneInfo);
                scene.GetRootGameObjects(rootGameObjects);
                for (int j = 0; j < scene.rootCount; j++)
                {
                    // traverse roots and collect recursively
                    sceneInfo.children.Add(GameObjectInfo.FromGameObject(rootGameObjects[j], captureComponents));
                }
            }

            return hierarchy;
        }

        private static void OnSendToEditor()
        {
            PlayerConnection.instance.Send(kMsgSendPlayerToEditor, Encoding.UTF8.GetBytes("Hello from Player"));
        }

        private static void Log(string log)
        {
            Debug.Log($"[{nameof(RemoteHierarchy)}] {log}");
        }
    }
}