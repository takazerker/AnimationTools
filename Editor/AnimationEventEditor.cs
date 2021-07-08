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
using UnityEngine;
using UnityEditor;

namespace AnimationTools
{
    public struct AnimationEventProperty
    {
        public SerializedProperty FunctionName;
        public SerializedProperty IntParameter;
        public SerializedProperty FloatParameter;
        public SerializedProperty StringParameter;
        public SerializedProperty ObjectReferenceParameter;
        public SerializedProperty MessageOptions;
    }

    public class AnimationEventEditor
    {
        public virtual AnimationEvent DefaultAnimationEvent { get => new AnimationEvent(); }

        public virtual bool IsValidFor(AnimationClip clip)
        {
            return true;
        }

        public virtual Color GetMarkerColor(AnimationEvent evt)
        {
            return Color.white;
        }

        public virtual void OnGUI(AnimationEventProperty property)
        {
            EditorGUILayout.PropertyField(property.FunctionName);
            EditorGUILayout.PropertyField(property.FloatParameter);
            EditorGUILayout.PropertyField(property.IntParameter);
            EditorGUILayout.PropertyField(property.StringParameter);
            EditorGUILayout.PropertyField(property.ObjectReferenceParameter);
            EditorGUILayout.PropertyField(property.MessageOptions);
        }
    }
}
