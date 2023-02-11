using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEditor.SceneManagement;
using UnityEngine.Networking.PlayerConnection;

namespace Needle.RemoteHierarchy
{
    public class RemoteHierarchyWindow : EditorWindow, IHasCustomMenu
    {
        private IConnectionState attachProfilerState;
        
        [MenuItem("Window/Analysis/Remote Hierarchy")]
        private static void Init()
        {
            var window = GetWindow<RemoteHierarchyWindow>();
            window.Show();
            window.titleContent = new GUIContent("Remote Hierarchy");
        }

        private void OnEnable()
        {
            attachProfilerState = PlayerConnectionGUIUtility.GetConnectionState(this, OnConnected);
            EditorConnection.instance.Initialize();
            EditorConnection.instance.Register(RemoteHierarchy.kMsgSendPlayerToEditor, OnMessageEvent);
            EditorConnection.instance.Register(RemoteHierarchy.kRequestHierarchyResponse, OnReceivedHierarchy);
        }

        private void OnConnected(string player)
        {
            // Debug.Log(string.Format("MyWindow connected to {0}", player));
        }
        
        private void OnDisable()
        {
            attachProfilerState.Dispose();
            EditorConnection.instance.Unregister(RemoteHierarchy.kMsgSendPlayerToEditor, OnMessageEvent);
            EditorConnection.instance.Unregister(RemoteHierarchy.kRequestHierarchyResponse, OnReceivedHierarchy);
            EditorConnection.instance.DisconnectAll();
        }

        private void OnMessageEvent(MessageEventArgs args)
        {
            var text = Encoding.ASCII.GetString(args.data);
            Debug.Log("Message from player: " + text);
        }
        
        private RemoteHierarchyStage m_HierarchyStage;
        private void OnReceivedHierarchy(MessageEventArgs args)
        {
            var json = Encoding.UTF8.GetString(args.data);
            var data = JsonUtility.FromJson<RemoteHierarchy.HierarchyInfo>(json);

            ShowHierarchyData(data, SendBackToPlayer, CallMethodOnPlayer);
            
        }

        private void ShowHierarchyData(RemoteHierarchy.HierarchyInfo data, Action<RemoteHierarchy.SceneInfo> hierarchyCallback, Action<RemoteHierarchy.RemoteCallInfo> rpcCallback)
        {
            // Debug.Log("Hierarchy from player: " + data);
            
            if (!m_HierarchyStage) {
                m_HierarchyStage = CreateInstance<RemoteHierarchyStage>();
            }
            
            StageUtility.GoToStage(m_HierarchyStage, true);

            m_HierarchyStage.UpdateFrom(data, hierarchyCallback, rpcCallback);
        }
        
        private void OnGUI()
        {
            PlayerConnectionGUILayout.ConnectionTargetSelectionDropdown(attachProfilerState, EditorStyles.toolbarDropDown);
            
            var playerCount = EditorConnection.instance.ConnectedPlayers.Count;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(string.Format("{0} players connected.", playerCount));
            int i = 0;
            foreach (var p in EditorConnection.instance.ConnectedPlayers)
            {
                builder.AppendLine(string.Format("[{0}] - {1} {2}", i++, p.name, p.playerId));
            }
            EditorGUILayout.HelpBox(builder.ToString(), MessageType.Info);

            if (ShowDevButtons && GUILayout.Button("Send message to player"))
            {
                EditorConnection.instance.Send(RemoteHierarchy.kSendRequestEditorToPlayer, Encoding.UTF8.GetBytes("Hello from Editor"));
            }
            
            EditorGUI.BeginDisabledGroup(!EditorConnection.instance.ConnectedPlayers.Any());
            if (GUILayout.Button(new GUIContent("Show Hierarchy Snapshot", "Send hierarchy information from the Player to the Editor and display it as preview stage.\nRequires an active EditorConnection, select a target in the dropdown above.")))
            {
                EditorConnection.instance.Send(RemoteHierarchy.kSendRequestEditorToPlayer, Encoding.UTF8.GetBytes(RemoteHierarchy.HierarchyWithComponentsRequest));
            }
            
            if (ShowDevButtons && GUILayout.Button("Show Hierarchy Snapshot (without components)"))
            {
                EditorConnection.instance.Send(RemoteHierarchy.kSendRequestEditorToPlayer, Encoding.UTF8.GetBytes(RemoteHierarchy.HierarchyRequest));
            }
            EditorGUI.EndDisabledGroup();

            if (ShowDevButtons && GUILayout.Button("(Local) Show Hierarchy Snapshot"))
            {
                var hierarchy = RemoteHierarchy.GetHierarchy(true);
                ShowHierarchyData(hierarchy, UpdateLocally, CallMethodLocally);
            }
            
            if (ShowDevButtons && GUILayout.Button("(Local) Show Hierarchy Snapshot (without components)"))
            {
                var hierarchy = RemoteHierarchy.GetHierarchy(false);
                ShowHierarchyData(hierarchy, UpdateLocally, CallMethodLocally);
            }
        }

        private void SendBackToPlayer(RemoteHierarchy.SceneInfo changeSet)
        {
            Debug.Log("Send changeset to player");
            var changeSetJson = JsonUtility.ToJson(changeSet, true);
            EditorConnection.instance.Send(RemoteHierarchy.kUpdateScene, Encoding.UTF8.GetBytes(changeSetJson));
        }
        
        private void CallMethodOnPlayer(RemoteHierarchy.RemoteCallInfo obj)
        {
            Debug.Log("Send RPC to player");
            var changeSetJson = JsonUtility.ToJson(obj, true);
            EditorConnection.instance.Send(RemoteHierarchy.kCallMethods, Encoding.UTF8.GetBytes(changeSetJson));
        }
        
        private void UpdateLocally(RemoteHierarchy.SceneInfo changeSet) => RemoteHierarchy.ProcessChangeSet(changeSet);
        private void CallMethodLocally(RemoteHierarchy.RemoteCallInfo obj) => RemoteHierarchy.ProcessMethodCalls(obj);
        
        private class RemoteHierarchyStage : PreviewSceneStage
        {
            protected override GUIContent CreateHeaderContent()
            {
                return new GUIContent("Remote Hierarchy");
            }
            
            protected override bool OnOpenStage()
            {
                base.OnOpenStage();
                
                // listen to hierarchy change events
                EditorApplication.hierarchyChanged += HierarchyChanged;
                EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGui;
                
                return true;
            }

            private void HierarchyWindowItemOnGui(int instanceId, Rect selectionRect)
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                if (!obj) return;
                
                // get length of already drawn label
                var label = EditorGUIUtility.ObjectContent(obj, obj.GetType());
                var labelWidth = EditorStyles.label.CalcSize(label).x;
                var offset = 40;
                var labelRect = new Rect(selectionRect.x + labelWidth - offset, selectionRect.y, selectionRect.width - labelWidth + offset, selectionRect.height);
                
                // draw vertex count if it's a mesh
                var meshFilter = obj.GetComponent<RemoteComponents>();
                if (meshFilter)
                {
                    var mi = meshFilter.components.FirstOrDefault(x => x is RemoteHierarchy.MeshFilterInfo);
                    if (mi is RemoteHierarchy.MeshFilterInfo mif)
                        EditorGUI.LabelField(labelRect, mif.vertexCount.ToString(), EditorStyles.miniLabel);
                }
            }

            private void HierarchyChanged()
            {
                var changeSet = new RemoteHierarchy.SceneInfo
                {
                    children = new List<RemoteHierarchy.GameObjectInfo>()
                };

                // check which objects have changed compared to the last hierarchy and send a list of changes to the player
                foreach (var pair in instanceIdsToGameObjects)
                {
                    if (pair.Key.active == pair.Value.activeSelf) continue;
                    
                    changeSet.children.Add(new RemoteHierarchy.GameObjectInfo()
                    {
                        instanceId = pair.Key.instanceId,
                        active = pair.Value.activeSelf,
                    });
                        
                    // update locally so we have the assumed right state -
                    // this will be overwritten by the player when it sends its hierarchy
                    pair.Key.active = pair.Value.activeSelf;
                }
                
                var changeCount = changeSet.children.Count;
                Debug.Log("Hierarchy changed – changes: " + changeCount);
                
                // serialize and send to player
                if (changeCount > 0)
                {
                    UpdateRemoteHierarchy(changeSet);
                }
            }

            private void UpdateRemoteHierarchy(RemoteHierarchy.SceneInfo changeSet)
            {
                gameObjectStateCallback(changeSet);
            }

            protected override void OnCloseStage()
            {
                EditorApplication.hierarchyChanged -= HierarchyChanged;
                EditorApplication.hierarchyWindowItemOnGUI -= HierarchyWindowItemOnGui;
                base.OnCloseStage();
            }

            private RemoteHierarchy.HierarchyInfo current;
            private Action<RemoteHierarchy.SceneInfo> gameObjectStateCallback;
            private Action<RemoteHierarchy.RemoteCallInfo> rpcCallback;
            
            Dictionary<RemoteHierarchy.GameObjectInfo, GameObject> instanceIdsToGameObjects = new Dictionary<RemoteHierarchy.GameObjectInfo, GameObject>();

            public void UpdateFrom(RemoteHierarchy.HierarchyInfo data, Action<RemoteHierarchy.SceneInfo> callback, Action<RemoteHierarchy.RemoteCallInfo> rpcCallback)
            {
                current = data;
                gameObjectStateCallback = callback;
                this.rpcCallback = rpcCallback;
                instanceIdsToGameObjects.Clear();
                
                void PlaceInStage(Transform parent, RemoteHierarchy.GameObjectInfo gameObjectInfo)
                {
                    var newGo = new GameObject(gameObjectInfo.name);
                    instanceIdsToGameObjects[gameObjectInfo] = newGo;
                    newGo.transform.SetParent(parent);
                    if (gameObjectInfo.transform != null)
                    {
                        newGo.transform.localPosition = gameObjectInfo.transform.position;
                        newGo.transform.localRotation = gameObjectInfo.transform.rotation;
                        newGo.transform.localScale = gameObjectInfo.transform.scale;
                    }
                    newGo.SetActive(gameObjectInfo.active);

                    if (gameObjectInfo.components != null && gameObjectInfo.components.Count > 0)
                    {
                        var componentWrapper = newGo.AddComponent<RemoteComponents>();
                        componentWrapper.rpcCallback = rpcCallback;
                        componentWrapper.components = gameObjectInfo.components.ToList();
                        componentWrapper.gameObjectInfo = gameObjectInfo;
                    }
                    
                    if (gameObjectInfo.children != null)
                    {
                        foreach (var child in gameObjectInfo.children)
                        {
                            PlaceInStage(newGo.transform, child);
                        }
                    }
                }
                
                // cleanup
                var roots = this.scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    DestroyImmediate(root);
                }
                
                // traverse data and place objects in stage
                foreach (var scene in data.scenes)
                {
                    var sceneGo = new GameObject("# " + scene.name);
                    StageUtility.PlaceGameObjectInCurrentStage(sceneGo);
                    
                    foreach (var go in scene.children)
                    {
                        PlaceInStage(sceneGo.transform, go);
                    }
                }
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Show Dev Buttons"), ShowDevButtons, () => { ShowDevButtons = !ShowDevButtons; });
        }

        public bool ShowDevButtons
        {
            get => SessionState.GetBool($"{nameof(RemoteHierarchyWindow)}_{nameof(ShowDevButtons)}", false);
            set => SessionState.SetBool($"{nameof(RemoteHierarchyWindow)}_{nameof(ShowDevButtons)}", value);
        }
    }
}