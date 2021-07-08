// Copyright (c) 2021 Takanori Shibasaki
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
using System.IO;
using System.Collections;
using System.Collections.Generic;
#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace AnimationTools
{
    [CustomEditor(typeof(AnimationClipMixerImporter))]
    class AnimationClipMixerImporterEditor : ScriptedImporterEditor
    {
        SerializedObject m_Editor;
        bool m_Modified;

        [SerializeField]
        bool m_Loop;

        [SerializeField]
        AnimationClipMixerJson.AnimationClipSource[] m_Animations;

        ReorderableList m_AnimationList;

        public override bool showImportedObject => false;

        public override void OnEnable()
        {
            base.OnEnable();
            LoadEditor();
        }

        void LoadEditor()
        {
            var data = new AnimationClipMixerJson();

            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(GetAssetPath()), data);

            m_Animations = data.Animations;
            m_Loop = data.Loop;
            m_Editor = new SerializedObject(this);

            hideFlags &= ~HideFlags.NotEditable;

            m_AnimationList = new ReorderableList(m_Editor, m_Editor.FindProperty(nameof(m_Animations)));
            m_AnimationList.drawElementCallback = OnDrawElement;
            m_AnimationList.elementHeightCallback = OnGetHeight;
            m_AnimationList.headerHeight = 1;
        }

        void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            rect.y += 1;
            EditorGUI.PropertyField(rect, m_Editor.FindProperty(nameof(m_Animations)).GetArrayElementAtIndex(index), true);
        }

        float OnGetHeight(int index)
        {
            return EditorGUI.GetPropertyHeight(m_Editor.FindProperty(nameof(m_Animations)).GetArrayElementAtIndex(index));
        }

        string GetAssetPath()
        {
            var path = AssetDatabase.GetAssetPath(assetSerializedObject.targetObject);
            return path;
        }

        protected override void Apply()
        {
            m_Modified = false;

            var data = new AnimationClipMixerJson();
            data.Animations = m_Animations;
            data.Loop = m_Loop;

            var json = EditorJsonUtility.ToJson(data, true);

            File.WriteAllText(GetAssetPath(), json);

            AssetDatabase.Refresh();
        }

        protected override void ResetValues()
        {
            m_Modified = false;
            LoadEditor();
        }

        public override bool HasModified()
        {
            return m_Modified;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            {
                m_Editor.Update();
                EditorGUILayout.PropertyField(m_Editor.FindProperty("m_Loop"));
                //EditorGUILayout.PropertyField(animations, GUIContent.none, true);


                m_AnimationList.DoLayoutList();

                m_Editor.ApplyModifiedProperties();
            }
            if (EditorGUI.EndChangeCheck())
            {
                m_Modified = true;
            }

            ApplyRevertGUI();
        }
    }

    [CustomPropertyDrawer(typeof(AnimationClipMixerJson.AnimationClipSource))]
    class AnimationClipSourceEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.height = EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("Animation"));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("Filter"));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("Path"));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("MatchTime"));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(position, property.FindPropertyRelative("CopyEvents"));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 5;
        }
    }

}
