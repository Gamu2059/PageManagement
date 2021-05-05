using System;
using UnityEditor;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    /// <summary>
    /// SceneをInspectorで描画するためのクラス
    /// https://gist.github.com/Hertzole/ac269f3148bc5192cc2eb6d472870d24
    /// </summary>
    [CustomPropertyDrawer(typeof(SceneObject))]
    public class SceneObjectDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var sceneNameProp = property.FindPropertyRelative("sceneName");

            var sceneAsset = GetSceneObject(sceneNameProp.stringValue);
            var newScene = EditorGUI.ObjectField(position, label, sceneAsset, typeof(SceneAsset), false);
            if (newScene == null) {
                sceneNameProp.stringValue = string.Empty;
            } else if (newScene.name != sceneNameProp.stringValue) {
                sceneAsset = GetSceneObject(newScene.name);
                if (sceneAsset != null) {
                    sceneNameProp.stringValue = newScene.name;
                }
            }
        }

        private SceneAsset GetSceneObject(string sceneObjectName) {
            if (string.IsNullOrEmpty(sceneObjectName)) {
                return null;
            }

            foreach (var scene in EditorBuildSettings.scenes) {
                if (scene.path.IndexOf(sceneObjectName, StringComparison.Ordinal) != -1) {
                    return AssetDatabase.LoadAssetAtPath(scene.path, typeof(SceneAsset)) as SceneAsset;
                }
            }

            Debug.LogWarning(
                $"Scene ({sceneObjectName}) cannot be used.\nAdd this scene to the 'Scenes in the Build' in the build settings.");
            return null;
        }
    }
}