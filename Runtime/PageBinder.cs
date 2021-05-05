using System;
using System.Linq;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    public abstract class PageBinder<TScene, TWindow, TScreen> : ScriptableObject
        where TScene : Enum where TWindow : Enum where TScreen : Enum {
        [Serializable]
        protected class SceneSet {
            public TScene type;
            public SceneObject scene;
        }

        [Serializable]
        protected class WindowSet {
            public TWindow type;
            public WindowPagePrefab windowPagePrefab;
        }

        [Serializable]
        protected class ScreenSet {
            public TScreen type;
            public ScreenPagePrefab screenPagePrefab;
        }

        [SerializeField]
        private SceneSet[] scenes;

        [SerializeField]
        private WindowSet[] windows;

        [SerializeField]
        private ScreenSet[] screens;

        public string GetScene(TScene type) {
            var sceneSet = scenes.FirstOrDefault(s => s != null && s.type.Equals(type));
            return sceneSet?.scene;
        }

        public WindowPage GetWindow(TWindow type) {
            var windowSet = windows.FirstOrDefault(w => w != null && w.type.Equals(type));
            return windowSet?.windowPagePrefab?.Prefab;
        }

        public ScreenPage GetScreen(TScreen type) {
            var screenSet = screens.FirstOrDefault(s => s != null && s.type.Equals(type));
            return screenSet?.screenPagePrefab?.Prefab;
        }
    }
}