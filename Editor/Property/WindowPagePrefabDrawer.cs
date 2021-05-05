using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    [CustomPropertyDrawer(typeof(WindowPagePrefab))]
    public class WindowPagePrefabDrawer : PropertyDrawer {
        private int index;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var prefabs = GetWindowPrefabs();
            var options = prefabs.Select(s => $"{s.Item1.name} - \"{s.Item2.Replace("/", "\\")}\"")
                .ToArray();

            if (prefabs.Count < 1) {
                var color = GUI.color;
                GUI.color = new Color(1f, 0.39f, 0.39f);
                EditorGUI.LabelField(position, $"利用可能な {nameof(WindowPage)} のプレハブがありません");
                GUI.color = color;
                return;
            }

            index = EditorGUI.Popup(position, label.text, index, options);
            if (index >= 0 || index < prefabs.Count) {
                var script = prefabs[index];
                var scriptProp = property.FindPropertyRelative("prefab");
                scriptProp.objectReferenceValue = script.Item1;
            }
        }

        private List<(WindowPage, string)> GetWindowPrefabs() {
            var paths = AssetDatabase
                .FindAssets($"t:{nameof(GameObject)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            var list = new List<(WindowPage, string)>();
            foreach (var path in paths) {
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj == null) {
                    continue;
                }

                var page = obj.GetComponent<WindowPage>();
                if (page != null) {
                    list.Add((page, path));
                }
            }

            return list;
        }
    }
}