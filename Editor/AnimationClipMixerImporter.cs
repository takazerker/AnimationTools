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
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
#if UNITY_2020_3_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif
using UnityEngine;
using UnityEditor;

namespace AnimationTools
{

    [System.Serializable]
    class AnimationClipMixerJson
    {
        [System.Serializable]
        public struct AnimationClipSource
        {
            public AnimationClip Animation;
            public string Filter;
            public string Path;
            public bool MatchTime;
            public bool CopyEvents;
        }

        public bool Loop;
        public AnimationClipSource[] Animations = new AnimationClipSource[0];
    }

    [ScriptedImporter(1, "mixclip")]
    class AnimationClipMixerImporter : ScriptedImporter
    {
        [MenuItem("Assets/Create/Animation Clip Mixer")]
        static void CreateMixer()
        {
            ProjectWindowUtil.CreateAssetWithContent("New AnimationClipMixer.mixclip", "{}");
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var newClip = new AnimationClip();
            newClip.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

            var data = new AnimationClipMixerJson();
            
            EditorJsonUtility.FromJsonOverwrite(File.ReadAllText(ctx.assetPath), data);

            newClip.wrapMode = newClip.wrapMode;

            var events = new List<AnimationEvent>();
            AnimationClip firstAnimation = null;

            var curves = new List<KeyValuePair<EditorCurveBinding, AnimationCurve>>();

            if (data.Animations != null)
            {
                foreach (var animation in data.Animations)
                {
                    if (animation.Animation == null)
                    {
                        continue;
                    }

                    var timeScale = 1.0f;

                    if (firstAnimation == null)
                    {
                        firstAnimation = animation.Animation;
                    }
                    else if (animation.MatchTime)
                    {
                        timeScale = 0 < animation.Animation.length ? firstAnimation.length / animation.Animation.length : 1;
                    }

                    var path = AssetDatabase.GetAssetPath(animation.Animation);
                    var regex = !string.IsNullOrEmpty(animation.Filter) ? new Regex(animation.Filter) : null;

                    if (animation.CopyEvents)
                    {
                        events.AddRange(AnimationUtility.GetAnimationEvents(animation.Animation));
                    }

                    ctx.DependsOnSourceAsset(path);

                    AnimationUtility.GetAnimationEvents(animation.Animation);

                    foreach (var binding in AnimationUtility.GetCurveBindings(animation.Animation))
                    {
                        if (regex != null && !regex.IsMatch(binding.path + "/" + binding.propertyName))
                        {
                            continue;
                        }

                        var curve = AnimationUtility.GetEditorCurve(animation.Animation, binding);

                        if (timeScale != 1)
                        {
                            var keys = curve.keys;

                            for (int i = 0; i < keys.Length; ++i)
                            {
                                keys[i].time *= timeScale;
                            }

                            curve.keys = keys;
                        }

                        var newBinding = binding;
                        newBinding.path = animation.Path;

                        if (!string.IsNullOrEmpty(binding.path))
                        {
                            if (!string.IsNullOrEmpty(newBinding.path))
                            {
                                newBinding.path += "/";
                            }
                            newBinding.path += binding.path;
                        }

                        var exists = false;

                        for (int i = 0; i < curves.Count; ++i)
                        {
                            if (curves[i].Key.path == newBinding.path && curves[i].Key.propertyName == newBinding.propertyName)
                            {
                                curves[i] = new KeyValuePair<EditorCurveBinding, AnimationCurve>(newBinding, curve);
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            curves.Add(new KeyValuePair<EditorCurveBinding, AnimationCurve>(newBinding, curve));
                        }
                    }
                }
            }

            if (firstAnimation != null)
            {
                var info = AnimationUtility.GetAnimationClipSettings(firstAnimation);
                info.loopTime = data.Loop;
                AnimationUtility.SetAnimationClipSettings(newClip, info);
            }

            foreach (var pair in curves)
            {
                AnimationUtility.SetEditorCurve(newClip, pair.Key, pair.Value);
            }
            
            AnimationUtility.SetAnimationEvents(newClip, events.ToArray());

            ctx.AddObjectToAsset("Animation", newClip);
            ctx.SetMainObject(newClip);
        }
    }

}
