using System;
using UnityEngine;

namespace com.Gamu2059.PageManagement {
    [Serializable]
    public class ScreenPagePrefab {
        [SerializeField]
        private ScreenPage prefab;

        public ScreenPage Prefab => prefab;

        [SerializeField]
        private int index;

        public static implicit operator ScreenPagePrefab(ScreenPage prefab) {
            return new ScreenPagePrefab {prefab = prefab};
        }
    }
}