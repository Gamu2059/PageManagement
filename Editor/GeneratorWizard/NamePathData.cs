using UnityEditor;
using UnityEngine;

namespace com.Gamu2059.PageManagement.Editor.GeneratorWizard {
    internal class NamePathData {
        public string NameSpace { get; private set; }
        public string Name { get; private set; }
        public string Path { get; private set; }

        public string Suffix { get; }

        private bool useNameSpace;

        public NamePathData(bool useNameSpace, string nameSpace, string name, string path, string suffix) {
            this.useNameSpace = useNameSpace;
            NameSpace = nameSpace;
            Name = name;
            Path = path;
            Suffix = suffix;
        }

        public void OnGUI(string title, string nameLabel, string pathLabel, string folderTitle) {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PrefixLabel(nameLabel);
                Name = EditorGUILayout.TextField(Name);
            }

            if (useNameSpace) {
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.PrefixLabel("ネームスペース");
                    NameSpace = EditorGUILayout.TextField(NameSpace);
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PrefixLabel(pathLabel);
                using (new EditorGUILayout.HorizontalScope()) {
                    var splitIdx = Path.IndexOf("Assets");
                    EditorGUILayout.LabelField(Path.Substring(splitIdx));
                    if (GUILayout.Button("パス指定")) {
                        Path = EditorUtility.OpenFolderPanel(folderTitle, Application.dataPath, string.Empty);
                        splitIdx = Path.IndexOf("Assets");
                        Path = Path.Substring(splitIdx);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PrefixLabel("プレビュー");
                EditorGUILayout.LabelField(CreatePath());
            }
        }

        public string CreatePath() {
            var splitIdx = Path.IndexOf("Assets");
            var path = Path.Substring(splitIdx);
            return System.IO.Path.Combine(path, Name + Suffix);
        }
    }
}