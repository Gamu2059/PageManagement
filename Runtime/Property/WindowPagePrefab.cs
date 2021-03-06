using System;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    [Serializable]
    public class WindowPagePrefab {
        [SerializeField]
        private WindowPage prefab;

        public WindowPage Prefab => prefab;

        [SerializeField]
        private int index;

        public static implicit operator WindowPagePrefab(WindowPage prefab) {
            return new WindowPagePrefab {prefab = prefab};
        }
    }
}