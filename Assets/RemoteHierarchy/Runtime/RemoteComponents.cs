using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Needle.RemoteHierarchy
{
    public class RemoteComponents : MonoBehaviour
    {
        [SerializeReference]
        public List<RemoteHierarchy.ComponentInfo> components = new List<RemoteHierarchy.ComponentInfo>();
        public RemoteHierarchy.GameObjectInfo gameObjectInfo { get; set; }
        public Action<RemoteHierarchy.RemoteCallInfo> rpcCallback { get; set; }

        [RuntimeInitializeOnLoadMethod]
        static void MakeSureInstanceExists()
        {
            Debug.Log("[RemoteComponents] MakeSureInstanceExists");
            RemoteHierarchy.Setup();   
        }

        private void Start()
        {
            MakeSureInstanceExists();
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(RemoteComponents))]
    public class RemoteComponentsEditor : Editor
    {
        private static Texture2D scriptIcon = null;
        public override void OnInspectorGUI()
        {
            var t = target as RemoteComponents;
            
            if (!scriptIcon)
                scriptIcon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;

            foreach (var c in t.components)
            {
                var tex = AssetPreview.GetMiniTypeThumbnail(c.type);
                if (!tex) tex = scriptIcon;
                // EditorGUILayout.BeginFoldoutHeaderGroup(true, new GUIContent(c.name, tex));

                var rect = EditorGUILayout.GetControlRect();
                rect.width = 22;
                var h = rect.height;
                rect.height = 16;
                EditorGUI.LabelField(rect, new GUIContent(null, tex));
                rect.height = h;
                rect.x += rect.width;
                rect.width = 20;
                if (c is RemoteHierarchy.BehaviourInfo behaviourInfo)
                    EditorGUI.Toggle(rect, behaviourInfo.enabled);
                rect.x += rect.width;
                rect.width = 300;
                EditorGUI.LabelField(rect, c.name, EditorStyles.boldLabel);
                
                // if (c is RemoteHierarchy.BehaviourInfo behaviourInfo)
                //     EditorGUILayout.ToggleLeft("Enabled", behaviourInfo.enabled);

                EditorGUI.indentLevel += 3;
                switch (c)
                {
                    case RemoteHierarchy.MeshFilterInfo m:
                        EditorGUILayout.LongField("Vertices", m.vertexCount);
                        break;
                }
                
                // draw all ContextMenu methods for this component type
                // TODO might be better to iterate all methods here if we also want to allow calling them
                // with specific parameters without having to mark them up with an Attribute
                
                var contextMenuMethods = TypeCache.GetMethodsWithAttribute<ContextMenu>();
                foreach (var method in contextMenuMethods)
                {
                    if (method.DeclaringType == null) continue;
                    var belongsToClass = method.DeclaringType.IsAssignableFrom(c.type);
                    if (!belongsToClass) continue;
                    
                    // check if any parameters, don't draw if that's the case - not a valid ContextMenu method
                    if (method.GetParameters().Length > 0) continue;
                    
                    if (GUILayout.Button(method.Name, GUILayout.ExpandWidth(false)))
                    {
                        t.rpcCallback.Invoke(new RemoteHierarchy.RemoteCallInfo() { calls = new List<RemoteHierarchy.RemoteCall>()
                        {
                            new RemoteHierarchy.RemoteCall()
                            {
                                gameObjectInstanceId = t.gameObjectInfo.instanceId,
                                componentTypeName = c.typeName,
                                methodName = method.Name,
                            },
                        }});
                    }
                }
                
                // get public methods on this component
                var publicMethods = c.type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in publicMethods)
                {
                    // only exact types for now, could also check for anything above MonoBehaviour
                    if (method.DeclaringType != c.type) continue;
                    var parameters = method.GetParameters();
                    if (parameters.Any(x => x.ParameterType != typeof(string))) continue;
                    // Must have at least one string parameter, otherwise we're showing ALL public methods which is not intended. 
                    // If a method with no parameters is intended to be called, it should be marked up with a ContextMenu attribute
                    if (parameters.Length < 1) continue;
                    // hide getters and setters - there's probably a better way to distinguish them
                    if (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")) continue;
                    
                    if (GUILayout.Button(method.Name, GUILayout.ExpandWidth(false)))
                    {
                        var setParameters = method.GetParameters();
                        t.rpcCallback.Invoke(new RemoteHierarchy.RemoteCallInfo() { calls = new List<RemoteHierarchy.RemoteCall>()
                        {
                            new RemoteHierarchy.RemoteCall()
                            {
                                gameObjectInstanceId = t.gameObjectInfo.instanceId,
                                componentTypeName = c.typeName,
                                methodName = method.Name,
                                parameters = setParameters.Select(x => stringParameterCache[(c.type, method, x.Name)]).ToList(),
                            },
                        }});
                    }

                    EditorGUI.indentLevel++;
                    // draw method parameter fields
                    foreach (var parameter in parameters)
                    {
                        if (!stringParameterCache.ContainsKey((c.type, method, parameter.Name)))
                            stringParameterCache[(c.type, method, parameter.Name)] = "";
                        
                        var value = stringParameterCache[(c.type, method, parameter.Name)];
                        var newValue = EditorGUILayout.TextField(parameter.Name, value);
                        if (newValue != value)
                            stringParameterCache[(c.type, method, parameter.Name)] = newValue;
                    }
                    EditorGUI.indentLevel--;
                }
                
                EditorGUI.indentLevel -= 3;
            }            
        }
        
        private static Dictionary<(Type, MethodInfo, string), string> stringParameterCache = new Dictionary<(Type, MethodInfo, string), string>();
    }
#endif
}