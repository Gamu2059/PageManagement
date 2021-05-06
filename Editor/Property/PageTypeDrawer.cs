using System;
using System.Linq;
using System.Reflection;
using com.Gamu2059.PageManagement.Editor.Attribute;
using UnityEditor;
using UnityEngine;

namespace com.Gamu2059.PageManagement.Editor.Property {
    [CustomPropertyDrawer(typeof(PageTypeAttribute))]
    public class PageTypeDrawer : PropertyDrawer {
        private Type[] cachedCandidateType;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var foundTypeProp = property.FindPropertyRelative("foundType");
            var indexProp = property.FindPropertyRelative("index");

            var attr = attribute as PageTypeAttribute;
            var types = GetType(attr.PageType);
            if (types == null || !types.Any()) {
                var labelSize = EditorStyles.label.CalcSize(label);
                EditorGUILayout.Space(-labelSize.y * 1.5f);
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.PrefixLabel(label);
                    var color = GUI.color;
                    GUI.color = new Color(1f, 0.39f, 0.39f);
                    EditorGUILayout.LabelField("利用可能な列挙型を見つけることができませんでした。");
                    GUI.color = color;
                }

                foundTypeProp.boolValue = false;
                return;
            }

            var options = types.Select(t => $"{t.Name} - {t.Namespace}.{t.Name}").ToArray();
            var index = indexProp.intValue;
            index = EditorGUI.Popup(position, label.text, index, options);
            if (index >= 0 || index < types.Length) {
                var type = types[index];
                var nameSpaceProp = property.FindPropertyRelative("nameSpace");
                var nameProp = property.FindPropertyRelative("name");
                nameSpaceProp.stringValue = type.Namespace;
                nameProp.stringValue = type.Name;
                foundTypeProp.boolValue = true;
                indexProp.intValue = index;
            }
        }

        private Type[] GetType(PageType pageType) {
            if (cachedCandidateType == null) {
                cachedCandidateType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => {
                        var targetAttr = type.GetCustomAttributes<PageManagement.Attribute.PageTypeAttribute>()
                            .FirstOrDefault(attr => attr.PageType == pageType);
                        return targetAttr != null;
                    })
                    .ToArray();
            }

            return cachedCandidateType;
        }
    }
}