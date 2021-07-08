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
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AnimationTools
{
    [System.Serializable]
    struct SerializedAnimationEvent : System.IEquatable<SerializedAnimationEvent>
    {
        public string FunctionName;
        public float Time;
        public string StringParameter;
        public int IntParameter;
        public float FloatParameter;
        public Object ObjectReferenceParameter;
        public SendMessageOptions MessageOptions;

        public SerializedAnimationEvent(AnimationEvent e)
        {
            FunctionName = e.functionName;
            Time = e.time;
            StringParameter = e.stringParameter;
            IntParameter = e.intParameter;
            FloatParameter = e.floatParameter;
            ObjectReferenceParameter = e.objectReferenceParameter;
            MessageOptions = e.messageOptions;
        }

        public bool Equals(SerializedAnimationEvent other)
        {
            return FunctionName == other.FunctionName
                && Time == other.Time
                && StringParameter == other.StringParameter
                && IntParameter == other.IntParameter
                && FloatParameter == other.FloatParameter
                && ObjectReferenceParameter == other.ObjectReferenceParameter
                && MessageOptions == other.MessageOptions;
        }

        public static implicit operator AnimationEvent(SerializedAnimationEvent src)
        {
            var result = new AnimationEvent();
            result.functionName = src.FunctionName;
            result.time = src.Time;
            result.stringParameter = src.StringParameter;
            result.floatParameter = src.FloatParameter;
            result.intParameter = src.IntParameter;
            result.objectReferenceParameter = src.ObjectReferenceParameter;
            result.messageOptions = src.MessageOptions;
            return result;
        }
    }

    [System.Serializable]
    class AnimationEventJson
    {
        public SerializedAnimationEvent[] Events;
    }

    [InitializeOnLoad]
    class AnimationEventEditorWindow : EditorWindow, IHasCustomMenu
    {
        static class Styles
        {
            public const float TimeRulerHeight = 20;
            public const float TickerStep = 50;
            public const float KeySize = 16;

            public static readonly Color SelectedKeyColor = new Color(1, 0.5f, 0, 1);
            public static readonly Color NormalKeyColor = new Color(1, 1, 1, 1);
            public static readonly Color SeekbarColor = new Color(1, 1, 1, 1);

            public static readonly Color ShadowColor = new Color(0, 0, 0, 0.25f);
            public static readonly Color OutOfRangeColor = new Color(0, 0, 0, 0.05f);

            public static readonly Color MajorTickColor = new Color(0, 0, 0, 0.1f);
            public static readonly Color TimeColor = new Color(0, 0, 0, 1);

            public static readonly GUIStyle AssetNameLabel;
            public static readonly GUIStyle TimeLabel;

            public static Texture2D DopeKeyIcon;

            static Styles()
            {
                DopeKeyIcon = LoadIcon("blendKey");

                AssetNameLabel = new GUIStyle(EditorStyles.boldLabel);
                AssetNameLabel.clipping = TextClipping.Clip;

                TimeLabel = new GUIStyle(GUI.skin.label);
                TimeLabel.alignment = TextAnchor.LowerLeft;
                TimeLabel.fontSize = 8;
                TimeLabel.clipping = TextClipping.Overflow;
            }
        }

        ref struct DopesheetData
        {
            public AnimationClip Clip;
            public float Length;
            public float FrameTime;
            public float MaxWidth;
            public float FrameRate;
            public float TickerStep;
        }

        struct AnimationEventLayout
        {
            public int ControlID;
            public Rect Rect;
            public float Time;
        }

        const float FrameTimePrecision = 100;

        delegate Texture2D LoadIconDelegate(string name);

        static System.Type AnimationClipEditorType;
        static System.Type InspectorWindowType;
        static PropertyInfo NormalizedTimeProperty;
        static LoadIconDelegate LoadIcon;

        static AnimationEventEditorWindow m_Instance;

        const float MinPropertyPaneWidth = 250;

        object m_TimeControl;
        float m_PropertyPaneWidth = 250;
        Vector2 m_Scroll = new Vector2(-50, 0);

        float m_Zoom = 100;

        [System.NonSerialized]
        AnimationEventLayout[] m_EventLayouts = new AnimationEventLayout[0];

        AnimationEventEditData m_EditorState
        {
            get
            {
                if (m_TargetData != null)
                {
                    return m_TargetData;
                }
                return null;
            }
        }

        AnimationEventEditor m_EditorInterface
        {
            get
            {
                if (m_UseCustomEditor)
                {
                    return m_CustomEditor;
                }
                return null;
            }
        }

        bool m_UseCustomEditor = true;
        bool m_Snap = true;

        float m_LastNormalizedTime;
        Editor m_AnimationClipEditor;

        [System.NonSerialized]
        AnimationClip m_TargetAnimationClip;

        [System.NonSerialized]
        AnimationEventEditData m_TargetData;

        [System.NonSerialized]
        bool m_Modified;

        [System.NonSerialized]
        bool m_DraggingEvent;

        [System.NonSerialized]
        Vector2 m_DragStart;

        [System.NonSerialized]
        float m_DragStartTime;

        [System.NonSerialized]
        float m_PrevPropertyPaneWidth;

        [System.NonSerialized]
        int m_PressedEvent;

        List<AnimationEventEditData> m_Animations = new List<AnimationEventEditData>();

        static AnimationEventEditor m_CustomEditor;

        SerializedObject m_SerializedEventObjects;
        SerializedAnimationEventObject[] m_AnimationEventObjects;

        static AnimationEventEditorWindow()
        {
            Editor.finishedDefaultHeaderGUI += HeaderGUI;

            AnimationClipEditorType = typeof(Editor).Assembly.GetType("UnityEditor.AnimationClipEditor");
            InspectorWindowType = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");

            var loadIconMethod = typeof(EditorGUIUtility).GetMethod("LoadIcon", BindingFlags.Static | BindingFlags.NonPublic);
            LoadIcon = loadIconMethod.CreateDelegate(typeof(LoadIconDelegate)) as LoadIconDelegate;

            LoadCustomEditor();
        }

        static void LoadCustomEditor()
        {
            var editorType = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(y => y.IsSubclassOf(typeof(AnimationEventEditor)) && !y.IsAbstract)
                .FirstOrDefault();

            m_CustomEditor = editorType != null ? System.Activator.CreateInstance(editorType) as AnimationEventEditor : null;
        }

        static void HeaderGUI(Editor obj)
        {
            if (obj.GetType() != AnimationClipEditorType || 2 <= obj.targets.Length)
            {
                return;
            }

            if (GUILayout.Button("Open Animation Event Editor"))
            {
                GetWindow<AnimationEventEditorWindow>("Animation Events", true);
            }

            if (m_Instance != null)
            {
                var clip = GetEditingAnimationClip(obj);

                if (clip != m_Instance.m_TargetAnimationClip)
                {
                    m_Instance.SetAnimationClip(clip);
                }

                if (m_Instance.m_AnimationClipEditor != obj)
                {
                    m_Instance.m_AnimationClipEditor = obj;
                    m_Instance.Repaint();
                }
            }
        }

        static AnimationClip GetEditingAnimationClip(Editor editor)
        {
            var clipInfoField = editor.GetType().GetField("m_ClipInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var clipInfo = clipInfoField.GetValue(editor);
            var targetClip = editor.target as AnimationClip;

            string takeName;

            if (clipInfo != null)
            {
                var takeNameProperty = clipInfo.GetType().GetProperty("takeName", BindingFlags.Instance | BindingFlags.Public);
                takeName = takeNameProperty.GetValue(clipInfo) as string;
            }
            else
            {
                takeName = targetClip.name;
            }

            var path = AssetDatabase.GetAssetPath(editor.target);

            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is AnimationClip clip && clip.name == takeName)
                {
                    return clip;
                }
            }

            return null;
        }

        void SetAnimationClip(AnimationClip clip)
        {
            m_TargetAnimationClip = clip;

            AnimationEventEditData animationData = null;

            foreach (var data in m_Animations)
            {
                if (data.AnimationClip == clip)
                {
                    animationData = data;
                    break;
                }
            }

            if (animationData == null)
            {
                animationData = AnimationEventEditData.Create(clip);
                m_Animations.Add(animationData);
            }

            m_TargetData = animationData;
            Repaint();
        }

        void ShowApplyRevertDialog()
        {
            if (m_Modified && EditorUtility.DisplayDialog("Animation Events Has Changed", "Do you want to save the changes?", "Save", "Discard Changes"))
            {
                OnApplyClick();
            }

            m_Modified = false;
        }

        void ReloadTimeControl(Editor editor)
        {
            if (editor == null)
            {
                m_TimeControl = null;
                return;
            }

            var previewField = editor.GetType().GetField("m_AvatarPreview", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var avatarPreview = previewField.GetValue(editor);

            if (avatarPreview == null)
            {
                return;
            }

            var timeControlField = avatarPreview.GetType().GetField("timeControl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m_TimeControl = timeControlField.GetValue(avatarPreview);

            if (NormalizedTimeProperty == null)
            {
                NormalizedTimeProperty = m_TimeControl.GetType().GetProperty("normalizedTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
        }

        void OnEnable()
        {
            if (m_Instance == null)
            {
                m_Instance = this;
            }

            m_Modified = false;

            if (!bool.TryParse(EditorUserSettings.GetConfigValue(nameof(AnimationEventEditorWindow) + "." + nameof(m_Snap)), out m_Snap))
            {
                m_Snap = true;
            }

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            EditorUserSettings.SetConfigValue(nameof(AnimationEventEditorWindow) + "." + nameof(m_Snap), m_Snap.ToString());

            if (m_Instance == this)
            {
                m_Instance = null;
            }

            m_TimeControl = null;

            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnDestroy()
        {
            foreach (var data in m_Animations)
            {
                DestroyImmediate(data);
            }

            m_Animations.Clear();
        }

        void OnInspectorUpdate()
        {
            if (m_TimeControl == null)
            {
                return;
            }

            var time = GetNormalizedTime();

            if (m_LastNormalizedTime != time)
            {
                m_LastNormalizedTime = time;
                Repaint();
            }
        }

        float GetNormalizedTime()
        {
            return NormalizedTimeProperty != null && m_TimeControl != null ? (float)NormalizedTimeProperty.GetValue(m_TimeControl) : 0;
        }

        void SetNormalizedTime(float normalizedTime)
        {
            if (m_TimeControl == null)
            {
                return;
            }

            normalizedTime = Mathf.Clamp01(normalizedTime);

            NormalizedTimeProperty.SetValue(m_TimeControl, normalizedTime);
            RepaintEditor();
        }

        DopesheetData InitDopesheetData()
        {
            var result = new DopesheetData();

            if (m_EditorState != null && m_EditorState.AnimationClip != null)
            {
                result.Clip = m_EditorState.AnimationClip;
                result.Length = m_EditorState.AnimationClip.length;
                result.FrameTime = 1.0f / m_EditorState.AnimationClip.frameRate;
                result.MaxWidth = result.Length * m_Zoom;
                result.FrameRate = m_EditorState.AnimationClip.frameRate;

                result.TickerStep = result.FrameTime * m_Zoom;

                if (result.TickerStep < Styles.TickerStep)
                {
                    result.TickerStep *= Mathf.Ceil(Styles.TickerStep / result.TickerStep);
                }

                return result;
            }

            result.TickerStep = Styles.TickerStep;

            return result;
        }

        private void OnSelectionChange()
        {
            ShowApplyRevertDialog();

            m_TargetAnimationClip = null;
            m_TargetData = null;

            Repaint();
        }

        private void OnGUI()
        {
            ReloadTimeControl(m_AnimationClipEditor);

            var contentsRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            var dopeSheetRect = contentsRect;
            dopeSheetRect.xMax -= m_PropertyPaneWidth;

            var data = InitDopesheetData();

            DrawDopesheet(dopeSheetRect, ref data);
            DoSplitter(new Rect(contentsRect.width - m_PropertyPaneWidth, 0, 4, Screen.height));
            DrawPropertyArea(new Rect(contentsRect.width - m_PropertyPaneWidth, 0, m_PropertyPaneWidth, Screen.height), ref data);
        }

        void IHasCustomMenu.AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(EditorGUIUtility.TrTextContent("Snap To Frame"), m_Snap, () =>
            {
                m_Snap = !m_Snap;
            });

            if (m_CustomEditor != null)
            {
                menu.AddItem(EditorGUIUtility.TrTextContent("Use Custom Editor"), m_UseCustomEditor, () =>
                {
                    m_UseCustomEditor = !m_UseCustomEditor;
                    Repaint();
                });
            }
        }

        void DoSplitter(Rect position)
        {
            var splitterID = GUIUtility.GetControlID(FocusType.Passive);

            EditorGUIUtility.AddCursorRect(position, MouseCursor.SplitResizeLeftRight, splitterID);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && position.Contains(Event.current.mousePosition))
            {
                m_PrevPropertyPaneWidth = m_PropertyPaneWidth;
                m_DragStart = Event.current.mousePosition;
                GUIUtility.hotControl = splitterID;
                Event.current.Use();
            }
            else if (GUIUtility.hotControl == splitterID)
            {
                if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Ignore)
                {
                    GUIUtility.hotControl = 0;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    var delta = Event.current.mousePosition - m_DragStart;

                    m_PropertyPaneWidth = Mathf.Max(m_PrevPropertyPaneWidth - delta.x, MinPropertyPaneWidth);

                    Event.current.Use();
                    Repaint();
                }
            }

            m_PropertyPaneWidth = Mathf.Clamp(m_PropertyPaneWidth, MinPropertyPaneWidth, Screen.width - 10);

        }

        static Color ConvertColor(Color color)
        {
            if (EditorGUIUtility.isProSkin)
            {
                return new Color(1 - color.r, 1 - color.g, 1 - color.b, color.a);
            }
            return color;
        }

        static void DrawColorRect(Rect position, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(position, Texture2D.whiteTexture);
        }

        void DrawDopesheet(Rect position, ref DopesheetData data)
        {
            GUI.BeginGroup(position);

            DrawTicks(ref data);

            DrawRange(ref data);

            if (m_EditorState != null)
            {
                DrawEvents(new Rect(0, Styles.TimeRulerHeight, position.width, position.height), ref data);
            }

            DrawSeekbar(ref data);

            DrawTimeLabels(ref data);

            if (Event.current.type == EventType.Repaint)
            {

            }
            else if (Event.current.type == EventType.MouseDrag && (Event.current.button == 2 || Event.current.alt))
            {
                m_Scroll -= Event.current.delta;
                Repaint();
                Event.current.Use();
            }
            else if (Event.current.type == EventType.ScrollWheel && GUIUtility.hotControl == 0)
            {
                m_Zoom = Mathf.Max(m_Zoom * (1.0f + Event.current.delta.y * 0.1f), 1);
                Repaint();
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                ClearSelection();
                Event.current.Use();
                Repaint();
            }

            GUI.EndGroup();
        }

        void RepaintEditor()
        {
            foreach (var inspector in Resources.FindObjectsOfTypeAll(InspectorWindowType))
            {
                var editor = inspector as EditorWindow;
                editor.Repaint();
            }
        }

        void OnRulerClick(ref DopesheetData data)
        {
            var time = (Event.current.mousePosition.x + m_Scroll.x) / m_Zoom;

            if (m_Snap)
            {
                time = Mathf.Floor(time / data.FrameTime) * data.FrameTime;
            }

            var normalizedTime = Mathf.Clamp01(time / data.Length);

            SetNormalizedTime(normalizedTime);
        }

        void DrawTicks(ref DopesheetData data)
        {
            var rulerID = GUIUtility.GetControlID(FocusType.Passive);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && 0 < data.MaxWidth && Event.current.mousePosition.y < Styles.TimeRulerHeight)
            {
                ClearSelection();
                GUIUtility.hotControl = rulerID;
                OnRulerClick(ref data);
                Event.current.Use();
            }
            else if (GUIUtility.hotControl == rulerID)
            {
                if (Event.current.type == EventType.Ignore || Event.current.type == EventType.MouseUp)
                {
                    GUIUtility.hotControl = 0;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    OnRulerClick(ref data);
                    Event.current.Use();
                }
            }

            DrawColorRect(new Rect(0, Styles.TimeRulerHeight, Screen.width, 1), Styles.ShadowColor);

            var tickRect = new Rect(0, 0, 1, Styles.TimeRulerHeight);
            tickRect.yMin += tickRect.height * 0.5f;

            var lineRect = new Rect(0, Styles.TimeRulerHeight + 1, 1, Screen.height - Styles.TimeRulerHeight - 1);

            var color = ConvertColor(Styles.MajorTickColor);

            for (var x = -m_Scroll.x; x < Screen.width; x += data.TickerStep)
            {
                var t = (x + m_Scroll.x) / m_Zoom;

                tickRect.x = x;
                DrawColorRect(tickRect, color);

                if (t < data.Length)
                {
                    lineRect.x = x;
                    DrawColorRect(lineRect, color);
                }
            }
        }

        void DrawTimeLabels(ref DopesheetData data)
        {
            if (Event.current.type == EventType.Repaint)
            {
                var tickRect = new Rect(0, 0, 1, Styles.TimeRulerHeight);
                tickRect.yMin += tickRect.height * 0.5f;

                for (var x = -m_Scroll.x; x < Screen.width; x += data.TickerStep)
                {
                    var t = (x + m_Scroll.x) / m_Zoom;
                    tickRect.x = x;
                    GUI.color = ConvertColor(Styles.TimeColor);
                    Styles.TimeLabel.Draw(tickRect, FormatTime(t), false, false, false, false);
                }
            }
        }

        public string FormatTime(float time)
        {
            return string.Format("{0:F2}", time);
        }

        void LayoutEvents(Rect position, ref DopesheetData data)
        {
            var iconRect = new Rect(0, Styles.TimeRulerHeight + 6, Styles.KeySize, Styles.KeySize);

            if (m_EventLayouts.Length != m_EditorState.Events.Length)
            {
                m_EventLayouts = new AnimationEventLayout[m_EditorState.Events.Length];
            }

            for (int i = 0; i < m_EventLayouts.Length; ++i)
            {
                m_EventLayouts[i].ControlID = GUIUtility.GetControlID(FocusType.Passive);

                m_EventLayouts[i].Rect = iconRect;
                m_EventLayouts[i].Rect.x = m_EditorState.Events[i].Time * m_Zoom - m_Scroll.x - iconRect.width / 2 + 2;
            }

            for (int i = 0; i < m_EventLayouts.Length; ++i)
            {
                for (int j = i + 1; j < m_EventLayouts.Length; ++j)
                {
                    if (m_EventLayouts[j].Rect.xMin < m_EventLayouts[i].Rect.xMax && m_EventLayouts[i].Rect.xMin < m_EventLayouts[j].Rect.xMax)
                    {
                        m_EventLayouts[j].Rect.y = m_EventLayouts[i].Rect.yMax;
                    }
                }
            }
        }

        int FindEvent(Vector2 position)
        {
            for (int i = 0; i < m_EventLayouts.Length; ++i)
            {
                if (m_EventLayouts[i].Rect.Contains(position))
                {
                    return i;
                }
            }

            return -1;
        }

        void SelectEvent(int index, bool toggle)
        {
            GUIUtility.keyboardControl = 0;

            Undo.RecordObject(m_EditorState, "Select Event");

            if (!toggle)
            {
                m_EditorState.Selection.Clear();
            }

            if (toggle)
            {
                if (!m_EditorState.Selection.Contains(index))
                {
                    m_EditorState.Selection.Add(index);
                }
                else
                {
                    m_EditorState.Selection.Remove(index);
                }
            }
            else if (!m_EditorState.Selection.Contains(index))
            {
                m_EditorState.Selection.Add(index);
            }
        }

        bool IsEventSelected(int index)
        {
            return m_EditorState.Selection.Contains(index);
        }

        void ClearSelection()
        {
            if (m_EditorState != null)
            {
                Undo.RecordObject(m_EditorState, "Clear Selection");
                m_EditorState.Selection.Clear();
            }
        }

        void RecordEventTimes()
        {
            for (int i = 0; i < m_EventLayouts.Length; ++i)
            {
                m_EventLayouts[i].Time = m_EditorState.Events[i].Time;
            }
        }

        void OffsetEvents(float delta)
        {
            Undo.RecordObject(m_EditorState, "Move Events");

            foreach (var index in m_EditorState.Selection)
            {
                m_EditorState.Events[index].Time = Mathf.Clamp(m_EventLayouts[index].Time + delta, 0, m_EditorState.AnimationClip.length);
            }

            m_Modified = true;
        }

        void DrawEvents(Rect position, ref DopesheetData data)
        {
            LayoutEvents(position, ref data);

            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            var selectionRect = new Rect(0, 0, 0, 0);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                var index = FindEvent(Event.current.mousePosition);

                if (0 <= index)
                {
                    if (Event.current.control)
                    {
                        SelectEvent(index, Event.current.control);
                    }
                    else
                    {
                        if (!IsEventSelected(index))
                        {
                            SelectEvent(index, false);
                        }

                        m_PressedEvent = index;
                        m_DragStartTime = m_EditorState.Events[index].Time;
                        m_DraggingEvent = true;
                        m_DragStart = Event.current.mousePosition;
                        RecordEventTimes();

                        GUIUtility.hotControl = m_EventLayouts[index].ControlID;
                    }
                    Event.current.Use();
                    Repaint();
                }
                else
                {
                    m_DragStart = Event.current.mousePosition;
                    GUIUtility.hotControl = controlID;

                    ClearSelection();
                    Event.current.Use();
                    Repaint();
                }
            }
            else if (m_DraggingEvent)
            {
                if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Ignore)
                {
                    if (m_EditorState.Events[m_PressedEvent].Time == m_DragStartTime)
                    {
                        if (!Event.current.control)
                        {
                            SelectEvent(m_PressedEvent, false);
                        }
                    }

                    m_DraggingEvent = false;
                    GUIUtility.hotControl = 0;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    var delta = Event.current.mousePosition - m_DragStart;
                    var time = delta.x / m_Zoom + m_DragStartTime;

                    if (m_Snap)
                    {
                        time = Mathf.Round(time / data.FrameTime) * data.FrameTime;
                    }

                    OffsetEvents(time - m_DragStartTime);
                    SetNormalizedTime(time / m_EditorState.AnimationClip.length);

                    Event.current.Use();
                    Repaint();
                }
            }
            else if (GUIUtility.hotControl == controlID)
            {
                var min = Vector2.Min(Event.current.mousePosition, m_DragStart);
                var max = Vector2.Max(Event.current.mousePosition, m_DragStart);
                selectionRect = new Rect(min, max - min);

                if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.Ignore)
                {
                    int group = Undo.GetCurrentGroup();
                    ClearSelection();

                    for (int i = 0; i < m_EventLayouts.Length; ++i)
                    {
                        if (RectIntersects(selectionRect, m_EventLayouts[i].Rect))
                        {
                            SelectEvent(i, true);
                        }
                    }
                    Undo.CollapseUndoOperations(group);

                    GUIUtility.hotControl = 0;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseDrag)
                {
                    Event.current.Use();
                    Repaint();
                }
                else if (Event.current.type == EventType.Repaint)
                {
                    GUI.color = new Color(0, 1, 1, 0.25f);
                    GUI.DrawTexture(selectionRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                }
            }

            DrawColorRect(position, new Color(0, 0, 0, 0.1f));

            var icon = Styles.DopeKeyIcon;

            GUI.color = Color.white;

            for (int i = 0; i < m_EventLayouts.Length; ++i)
            {
                if (m_EditorState.Selection.Contains(i))
                {
                    GUI.color = Styles.SelectedKeyColor;
                }
                else if (m_EditorInterface != null)
                {
                    GUI.color = m_EditorInterface.GetMarkerColor(m_EditorState.Events[i]);
                }
                else
                {
                    GUI.color = Styles.NormalKeyColor;
                }

                if (RectIntersects(selectionRect, m_EventLayouts[i].Rect))
                {
                    GUI.color = new Color(1, 1, 0, 1);
                }

                GUI.DrawTexture(m_EventLayouts[i].Rect, icon, ScaleMode.StretchToFill);
            }
        }

        static bool RectIntersects(Rect a, Rect b)
        {
            return a.xMin < b.xMax &&
                    a.yMin < b.yMax &&
                    a.xMax > b.xMin &&
                    a.yMax > b.yMin;
        }

        void DrawRange(ref DopesheetData data)
        {
            DrawColorRect(new Rect(0, 0, -m_Scroll.x, Screen.height), Styles.OutOfRangeColor);

            if (0 < data.MaxWidth)
            {
                var max = data.MaxWidth - m_Scroll.x;

                DrawColorRect(new Rect(max, 0, Screen.width - max, Screen.height), Styles.OutOfRangeColor);
            }
        }

        void DrawSeekbar(ref DopesheetData data)
        {
            DrawColorRect(new Rect(GetNormalizedTime() * data.MaxWidth - m_Scroll.x - 1, 0, 1, Screen.height), Styles.SeekbarColor);
        }

        void DrawPropertyArea(Rect position, ref DopesheetData data)
        {
            GUI.BeginGroup(position);

            DrawColorRect(new Rect(0, 0, 1, position.height), Styles.ShadowColor);

            if (m_EditorState != null && m_EditorState.AnimationClip != null)
            {
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                GUILayout.BeginArea(new Rect(1, 0, position.width - 1, position.height));
                {
                    GUILayout.BeginHorizontal(EditorStyles.toolbar);
                    {
                        if (GUILayout.Button(m_EditorState.AnimationClip.name, Styles.AssetNameLabel, GUILayout.Width(1), GUILayout.ExpandWidth(true)))
                        {
                            EditorGUIUtility.PingObject(m_EditorState.AnimationClip);
                        }

                        GUI.enabled = data.Clip != null && m_Modified;

                        if (GUILayout.Button("Apply", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        {
                            OnApplyClick();
                        }

                        if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(50)))
                        {
                            OnRevertClick();
                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUI.enabled = data.Clip != null;

                        if (GUILayout.Button("Add", EditorStyles.miniButtonLeft, GUILayout.Width(55), GUILayout.ExpandWidth(true)))
                        {
                            OnAddEventClick();
                        }

                        GUI.enabled = data.Clip != null && 0 < m_EditorState.Selection.Count;

                        if (GUILayout.Button("Remove", EditorStyles.miniButtonMid, GUILayout.Width(55), GUILayout.ExpandWidth(true)))
                        {
                            OnRemoveEventsClick();
                        }

                        if (GUILayout.Button("Copy", EditorStyles.miniButtonMid, GUILayout.Width(55), GUILayout.ExpandWidth(true)))
                        {
                            OnCopyEventsClick();
                        }

                        GUI.enabled = data.Clip != null && CanPaste();

                        if (GUILayout.Button("Paste", EditorStyles.miniButtonRight, GUILayout.Width(55), GUILayout.ExpandWidth(true)))
                        {
                            OnPasteEventsClick();
                        }

                        GUI.enabled = true;
                    }
                    GUILayout.EndHorizontal();

                    EditorGUILayout.Space();

                    DrawProperties(ref data);
                }
                GUILayout.EndArea();
            }
            GUI.EndGroup();
        }

        void DrawTimeField(ref DopesheetData data, SerializedAnimationEvent[] selection)
        {
            var times = selection.Select(x => x.Time).ToArray();
            EditorGUI.showMixedValue = 2 <= times.Length;

            EditorGUI.BeginChangeCheck();

            times[0] = EditorGUILayout.FloatField("Time", times[0]);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_EditorState, "Change Time");
                times[0] = Mathf.Clamp(times[0], 0, m_EditorState.AnimationClip.length);

                foreach (var index in m_EditorState.Selection)
                {
                    m_EditorState.Events[index].Time = times[0];
                }

                m_Modified = true;
            }

            EditorGUI.BeginChangeCheck();

            var frame = EditorGUILayout.FloatField("Frame", Mathf.Round(times[0] / data.FrameTime * FrameTimePrecision) / FrameTimePrecision);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_EditorState, "Change Time");

                times[0] = Mathf.Clamp(frame * data.FrameTime, 0, m_EditorState.AnimationClip.length);

                foreach (var index in m_EditorState.Selection)
                {
                    m_EditorState.Events[index].Time = times[0];
                }

                m_Modified = true;
            }
        }

        void DrawPropertyFields(ref DopesheetData data, SerializedAnimationEvent[] selection)
        {
            {
                var functionNames = selection.Select(x => x.FunctionName).Distinct().ToArray();
                EditorGUI.showMixedValue = 2 <= functionNames.Length;
                EditorGUI.BeginChangeCheck();

                functionNames[0] = EditorGUILayout.TextField("Function", functionNames[0]);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_EditorState, "Change Function");

                    foreach (var index in m_EditorState.Selection)
                    {
                        m_EditorState.Events[index].FunctionName = functionNames[0];
                    }

                    m_Modified = true;
                }
            }

            {
                var floatParams = selection.Select(x => x.FloatParameter).Distinct().ToArray();
                EditorGUI.showMixedValue = 2 <= floatParams.Length;
                EditorGUI.BeginChangeCheck();

                floatParams[0] = EditorGUILayout.FloatField("Float", floatParams[0]);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_EditorState, "Change Parameter");

                    foreach (var index in m_EditorState.Selection)
                    {
                        m_EditorState.Events[index].FloatParameter = floatParams[0];
                    }

                    m_Modified = true;
                }
            }

            {
                var intParams = selection.Select(x => x.IntParameter).Distinct().ToArray();
                EditorGUI.showMixedValue = 2 <= intParams.Length;
                EditorGUI.BeginChangeCheck();

                intParams[0] = EditorGUILayout.IntField("Int", intParams[0]);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_EditorState, "Change Parameter");

                    foreach (var index in m_EditorState.Selection)
                    {
                        m_EditorState.Events[index].IntParameter = intParams[0];
                    }

                    m_Modified = true;
                }
            }

            {
                var stringParams = selection.Select(x => x.StringParameter).Distinct().ToArray();
                EditorGUI.showMixedValue = 2 <= stringParams.Length;
                EditorGUI.BeginChangeCheck();

                stringParams[0] = EditorGUILayout.TextField("String", stringParams[0]);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_EditorState, "Change Parameter");

                    foreach (var index in m_EditorState.Selection)
                    {
                        m_EditorState.Events[index].StringParameter = stringParams[0];
                    }

                    m_Modified = true;
                }
            }

            {
                var objectParams = selection.Select(x => x.ObjectReferenceParameter).Distinct().ToArray();
                EditorGUI.showMixedValue = 2 <= objectParams.Length;
                EditorGUI.BeginChangeCheck();

                objectParams[0] = EditorGUILayout.ObjectField("Object", objectParams[0], typeof(Object), false);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_EditorState, "Change Parameter");

                    foreach (var index in m_EditorState.Selection)
                    {
                        m_EditorState.Events[index].ObjectReferenceParameter = objectParams[0];
                    }

                    m_Modified = true;
                }
            }

            EditorGUILayout.Space();

            {
                var messageOptions = selection.Select(x => x.MessageOptions).Distinct().ToArray();
                EditorGUI.showMixedValue = 2 <= messageOptions.Length;
                EditorGUI.BeginChangeCheck();

                messageOptions[0] = (SendMessageOptions)EditorGUILayout.EnumPopup("Option", messageOptions[0]);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_EditorState, "Change Option");

                    foreach (var index in m_EditorState.Selection)
                    {
                        m_EditorState.Events[index].MessageOptions = messageOptions[0];
                    }

                    m_Modified = true;
                }
            }
        }

        void DrawCustomDrawer(ref DopesheetData data, SerializedAnimationEvent[] selection)
        {
            System.Array.Resize(ref m_AnimationEventObjects, selection.Length);

            for (int i = 0; i < selection.Length; ++i)
            {
                if (m_AnimationEventObjects[i] == null)
                {
                    m_AnimationEventObjects[i] = CreateInstance<SerializedAnimationEventObject>();
                }

                m_AnimationEventObjects[i].Data = selection[i];
            }

            if (m_SerializedEventObjects == null || m_SerializedEventObjects.targetObjects.Length != selection.Length)
            {
                m_SerializedEventObjects = new SerializedObject(m_AnimationEventObjects);
            }

            m_SerializedEventObjects.Update();

            var property = m_SerializedEventObjects.FindProperty("Data");

            AnimationEventProperty properties;
            properties.FunctionName = property.FindPropertyRelative("FunctionName");
            properties.FloatParameter = property.FindPropertyRelative("FloatParameter");
            properties.IntParameter = property.FindPropertyRelative("IntParameter");
            properties.StringParameter = property.FindPropertyRelative("StringParameter");
            properties.ObjectReferenceParameter = property.FindPropertyRelative("ObjectReferenceParameter");
            properties.MessageOptions = property.FindPropertyRelative("MessageOptions");

            m_EditorInterface.OnGUI(properties);

            m_SerializedEventObjects.ApplyModifiedPropertiesWithoutUndo();

            bool dirty = false;

            for (int i = 0; i < selection.Length; ++i)
            {
                if (!selection[i].Equals(m_AnimationEventObjects[i].Data))
                {
                    selection[i] = m_AnimationEventObjects[i].Data;
                    dirty = true;
                }
            }

            if (dirty)
            {
                Undo.RecordObject(m_EditorState, "Edit Events");

                for (int i = 0, j = 0; i < m_EditorState.Selection.Count; ++i, ++j)
                {
                    var index = m_EditorState.Selection[i];
                    m_EditorState.Events[index] = selection[j];
                }

                Repaint();
                m_Modified = true;
            }
        }

        void DrawProperties(ref DopesheetData data)
        {
            if (m_EditorState.Selection.Count <= 0)
            {
                return;
            }

            var selection = m_EditorState.Selection.Select(x => m_EditorState.Events[x]).ToArray();

            EditorGUIUtility.labelWidth = 60;

            DrawTimeField(ref data, selection);

            EditorGUILayout.Space();

            if (m_EditorInterface != null)
            {
                DrawCustomDrawer(ref data, selection);
            }
            else
            {
                DrawPropertyFields(ref data, selection);
            }
        }

        int AddEvent(AnimationEvent e)
        {
            Undo.RecordObject(m_EditorState, "Add Event");

            ArrayUtility.Add(ref m_EventLayouts, default);
            ArrayUtility.Add(ref m_EditorState.Events, new SerializedAnimationEvent(e));

            m_EditorState.Selection.Clear();
            m_EditorState.Selection.Add(m_EditorState.Events.Length - 1);

            m_Modified = true;

            return m_EventLayouts.Length - 1;
        }

        void OnAddEventClick()
        {
            var e = new AnimationEvent();

            if (m_EditorInterface != null)
            {
                e = m_EditorInterface.DefaultAnimationEvent;
            }

            e.time = GetNormalizedTime() * m_EditorState.AnimationClip.length;

            AddEvent(e);
        }

        void RemoveSelectedEvents()
        {
            Undo.RecordObject(m_EditorState, "Remove Event");

            var toRemove = new List<int>(m_EditorState.Selection);
            toRemove.Sort();
            toRemove.Reverse();

            foreach (var index in toRemove)
            {
                ArrayUtility.RemoveAt(ref m_EventLayouts, index);
                ArrayUtility.RemoveAt(ref m_EditorState.Events, index);
            }

            m_EditorState.Selection.Clear();

            m_Modified = true;
        }

        void OnRemoveEventsClick()
        {
            RemoveSelectedEvents();
        }

        void OnCopyEventsClick()
        {
            var copy = new AnimationEventJson();
            copy.Events = m_EditorState.Events.Where((val, index) => m_EditorState.Selection.Contains(index)).ToArray();

            var json = EditorJsonUtility.ToJson(copy, true);
            GUIUtility.systemCopyBuffer = json;
        }

        bool CanPaste()
        {
            try
            {
                var copyBuffer = GUIUtility.systemCopyBuffer;

                if (!string.IsNullOrEmpty(copyBuffer))
                {
                    var copy = JsonUtility.FromJson<AnimationEventJson>(copyBuffer);
                    return copy != null && copy.Events != null && 0 < copy.Events.Length;
                }
            }
            catch (System.Exception)
            {
            }

            return false;
        }

        void OnPasteEventsClick()
        {
            var copy = new AnimationEventJson();

            EditorJsonUtility.FromJsonOverwrite(GUIUtility.systemCopyBuffer, copy);

            if (copy.Events == null || copy.Events.Length <= 0)
            {
                return;
            }

            var minTime = copy.Events.OrderBy(x => x.Time).First().Time;
            var startTime = GetNormalizedTime() * m_EditorState.AnimationClip.length;

            var group = Undo.GetCurrentGroup();

            Undo.RecordObject(m_EditorState, "Paste Events");

            m_EditorState.Selection.Clear();

            foreach (var e in copy.Events)
            {
                var newEvent = e;
                newEvent.Time = newEvent.Time - minTime + startTime;

                ArrayUtility.Add(ref m_EventLayouts, default);
                ArrayUtility.Add(ref m_EditorState.Events, new SerializedAnimationEvent(newEvent));
                m_EditorState.Selection.Add(m_EventLayouts.Length - 1);
            }

            m_Modified = true;
        }

        void OnApplyClick()
        {
            var events = new AnimationEvent[m_EventLayouts.Length];

            for (int i = 0; i < events.Length; ++i)
            {
                events[i] = m_EditorState.Events[i];
                events[i].time /= m_EditorState.AnimationClip.length;
            }

            var path = AssetDatabase.GetAssetPath(m_EditorState.AnimationClip);

            if (AssetImporter.GetAtPath(path) is ModelImporter model)
            {
                var clipAnimations = model.clipAnimations;
                var clipInfo = clipAnimations.Select(x => x).Where(x => x.takeName == m_EditorState.AnimationClip.name).FirstOrDefault();

                if (clipAnimations == null)
                {
                    return;
                }

                clipInfo.events = events;
                model.clipAnimations = clipAnimations;
                model.SaveAndReimport();
            }
            else
            {
                AnimationUtility.SetAnimationEvents(m_EditorState.AnimationClip, events);
                EditorUtility.SetDirty(m_EditorState.AnimationClip);
            }

            ClearEditData();
            m_Modified = false;
        }

        void OnRevertClick()
        {
            ClearEditData();
            m_Modified = false;
        }

        void OnUndoRedo()
        {
            Repaint();
        }

        void ClearEditData()
        {
            var index = m_Animations.IndexOf(m_EditorState);

            var newData = AnimationEventEditData.Create(m_EditorState.AnimationClip);
            DestroyImmediate(m_EditorState);
            m_Animations[index] = newData;
            m_TargetData = newData;
        }
    }
}
