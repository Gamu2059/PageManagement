using System;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    /// <summary>
    /// SceneをInspectorでアタッチするためのクラス
    /// https://gist.github.com/Hertzole/ac269f3148bc5192cc2eb6d472870d24
    /// </summary>
    [Serializable]
    public class SceneObject {
        [SerializeField]
        private string sceneName;

        public static implicit operator string(SceneObject sceneObject) {
            return sceneObject.sceneName;
        }

        public static implicit operator SceneObject(string sceneName) {
            return new SceneObject {sceneName = sceneName};
        }

        public override string ToString() {
            return sceneName;
        }
    }
}