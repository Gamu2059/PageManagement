using System;
using UnityEngine;

namespace com.Gamu2059.PageManagement.Editor.Property {
    [Serializable]
    public class TypeObject {
        [SerializeField]
        private string nameSpace;

        public string NameSpace => nameSpace;

        [SerializeField]
        private string name;

        public string Name => name;

        [SerializeField]
        private bool foundType;

        public bool FoundType => foundType;

        public Type CreateType() {
            return Type.GetType($"{nameSpace} {name}");
        }
    }
}