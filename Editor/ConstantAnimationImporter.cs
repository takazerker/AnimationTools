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
    // This script converts imported animation curves to constant interpolation mode.
    class ConstantAnimationImporter : AssetPostprocessor
    {
        const string ConstantAnimationSuffix = "CONSTANT";

        public override int GetPostprocessOrder()
        {
            return -1;
        }

        public override uint GetVersion()
        {
            return 1;
        }

        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!clip.name.EndsWith(ConstantAnimationSuffix, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var keys = curve.keys;

                if (IsFlatCurve(keys))
                {
                    continue;
                }

                for (int j = 0; j < keys.Length; ++j)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, j, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, j, AnimationUtility.TangentMode.Constant);
                }

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }
        }

        static bool IsFlatCurve(Keyframe[] keys)
        {
            for (int i = 1; i < keys.Length; ++i)
            {
                if (keys[i].value != keys[0].value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
