using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using com.Gamu2059.PageManagement.Editor.Attribute;
using com.Gamu2059.PageManagement.Editor.Property;
using UnityEditor;
using UnityEngine;

namespace com.Gamu2059.PageManagement.Editor.GeneratorWizard {
    public class GeneratorWizard : ScriptableWizard {
        [SerializeField]
        [PageType(PageType.Scene)]
        private TypeObject scene;

        [SerializeField]
        [PageType(PageType.Window)]
        private TypeObject window;

        [SerializeField]
        [PageType(PageType.Screen)]
        private TypeObject screen;

        private NamePathData pageManagerData;
        private NamePathData pageBinderData;

        private SerializedProperty sceneProp;
        private SerializedProperty windowProp;
        private SerializedProperty screenProp;

        private SerializedProperty sceneFoundProp;
        private SerializedProperty windowFoundProp;
        private SerializedProperty screenFoundProp;

        [MenuItem("Tools/Gamu2059/PageManagement/Generator")]
        public static void CreateWizard() {
            DisplayWizard<GeneratorWizard>("テンプレートの生成", "生成");
        }

        protected override bool DrawWizardGUI() {
            EditorGUILayout.LabelField("ページタイプの指定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sceneProp);
            EditorGUILayout.PropertyField(windowProp);
            EditorGUILayout.PropertyField(screenProp);

            EditorGUILayout.Space();

            pageManagerData.OnGUI(
                "PageManagerクラスの生成", "クラス名", "生成パス", "PageManagerクラスの生成パス指定");

            EditorGUILayout.Space();

            pageBinderData.OnGUI(
                "PageBinderクラスの生成", "クラス名", "生成パス", "PageBinderクラスの生成パス指定");

            return true;
        }

        protected void OnEnable() {
            var obj = new SerializedObject(this);

            sceneProp = obj.FindProperty("scene");
            windowProp = obj.FindProperty("window");
            screenProp = obj.FindProperty("screen");

            sceneFoundProp = sceneProp.FindPropertyRelative("foundType");
            windowFoundProp = windowProp.FindPropertyRelative("foundType");
            screenFoundProp = screenProp.FindPropertyRelative("foundType");

            var defaultNameSpace = EditorSettings.projectGenerationRootNamespace;
            if (string.IsNullOrEmpty(defaultNameSpace)) {
                defaultNameSpace = Application.companyName;
            }

            pageManagerData = new NamePathData(true, defaultNameSpace, string.Empty, Application.dataPath, ".cs");
            pageBinderData = new NamePathData(true, defaultNameSpace, string.Empty, Application.dataPath, ".cs");

            CheckError();
        }

        public void OnWizardUpdate() {
            CheckError();
        }

        private void CheckError() {
            isValid = true;
            errorString = string.Empty;
            var errorMessages = new List<string>();

            if (!sceneFoundProp.boolValue) {
                isValid = false;
                errorMessages.Add("Sceneの型が指定されていません。");
            }

            if (!windowFoundProp.boolValue) {
                isValid = false;
                errorMessages.Add("Windowの型が指定されていません。");
            }

            if (!screenFoundProp.boolValue) {
                isValid = false;
                errorMessages.Add("Screenの型が指定されていません。");
            }

            if (string.IsNullOrEmpty(pageManagerData.Name)) {
                isValid = false;
                errorMessages.Add("PageManagerのクラス名が指定されていません。");
            }

            if (string.IsNullOrEmpty(pageBinderData.Name)) {
                isValid = false;
                errorMessages.Add("PageBinderのクラス名が指定されていません。");
            }

            if (!isValid) {
                var builder = new StringBuilder();
                foreach (var m in errorMessages) {
                    builder.Append(m).Append(Environment.NewLine);
                }

                errorString = builder.ToString();
            }
        }

        public void OnWizardCreate() {
            CreateClass("PageManagement/Editor/GeneratorWizard/PageManagerTemplate.txt", pageManagerData);
            CreateClass("PageManagement/Editor/GeneratorWizard/PageBinderTemplate.txt", pageBinderData);
            AssetDatabase.Refresh();
        }

        private void CreateClass(string templatePath, NamePathData data) {
            var sceneNameSpaceProp = sceneProp.FindPropertyRelative("nameSpace");
            var windowNameSpaceProp = windowProp.FindPropertyRelative("nameSpace");
            var screenNameSpaceProp = screenProp.FindPropertyRelative("nameSpace");
            var sceneNameProp = sceneProp.FindPropertyRelative("name");
            var windowNameProp = windowProp.FindPropertyRelative("name");
            var screenNameProp = screenProp.FindPropertyRelative("name");

            var assetsPath = "Assets";
            var packagePath = "Packages/com.gamu2059.page-management";
            
            var assetsTemplate = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.Combine(assetsPath, templatePath));
            var packageTemplate = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.Combine(packagePath, templatePath));
            var template = assetsTemplate != null ? assetsTemplate : packageTemplate;
            if (template == null) {
                Debug.LogError($"テンプレートファイルの読み込みに失敗しました。\nPath : {templatePath}");
                return;
            }
            
            var nameSpace = data.NameSpace;
            if (string.IsNullOrEmpty(nameSpace)) {
                nameSpace = Application.companyName;
            }

            var code = template.text
                .Replace("CLASS_NAME_SPACE", nameSpace)
                .Replace("SCENE_NAME_SPACE", sceneNameSpaceProp.stringValue)
                .Replace("WINDOW_NAME_SPACE", windowNameSpaceProp.stringValue)
                .Replace("SCREEN_NAME_SPACE", screenNameSpaceProp.stringValue)
                .Replace("CLASS_NAME", data.Name)
                .Replace("SCENE_TYPE", sceneNameProp.stringValue)
                .Replace("WINDOW_TYPE", windowNameProp.stringValue)
                .Replace("SCREEN_TYPE", screenNameProp.stringValue);

            var path = AssetDatabase.GenerateUniqueAssetPath(data.CreatePath());
            File.WriteAllText(path, code);
        }
    }
}