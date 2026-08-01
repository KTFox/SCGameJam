using System;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal static class GUI2
    {
        public static Color darkRed = new Color(0.5f, .0f, 0f, 1f);

        public static readonly GUILayoutOption[] GLW_20 = { GUILayout.Width(20f) };
        public static readonly GUILayoutOption[] GLW_24 = { GUILayout.Width(24f) };
        public static readonly GUILayoutOption[] GLW_50 = { GUILayout.Width(50f) };
        public static readonly GUILayoutOption[] GLW_70 = { GUILayout.Width(70f) };
        public static readonly GUILayoutOption[] GLW_80 = { GUILayout.Width(80f) };
        public static readonly GUILayoutOption[] GLW_100 = { GUILayout.Width(100f) };
        public static readonly GUILayoutOption[] GLW_120 = { GUILayout.Width(120f) };
        public static readonly GUILayoutOption[] GLW_140 = { GUILayout.Width(140f) };
        public static readonly GUILayoutOption[] GLW_150 = { GUILayout.Width(150f) };
        public static readonly GUILayoutOption[] GLW_160 = { GUILayout.Width(160f) };
        public static readonly GUILayoutOption[] GLW_320 = { GUILayout.Width(320f) };

        private static GUIStyle _miniLabelAlignRight;
        public static GUIStyle miniLabelAlignRight
        {
            get
            {
                if (_miniLabelAlignRight != null) return _miniLabelAlignRight;
                return _miniLabelAlignRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            }
        }

        public static Color Theme(Color proColor, Color indieColor)
        {
            return EditorGUIUtility.isProSkin ? proColor : indieColor;
        }

        public static void Rect(Rect r, Color c, float? alpha = null)
        {
            if (alpha != null) c.a = alpha.Value;
            using (FR2_Scope.GUIColor(c))
            {
                GUI.DrawTexture(r, Texture2D.whiteTexture);
            }
        }

        public static UnityObject[] DropZone(string title, float w, float h)
        {
            Rect rect = GUILayoutUtility.GetRect(w, h);
            GUI.Box(rect, GUIContent.none, EditorStyles.textArea);

            float cx = rect.x + w / 2f;
            float cy = rect.y + h / 2f;
            float pz = w / 3f;

            var plusRect = new Rect(cx - pz / 2f, cy - pz / 2f, pz, pz);
            using (FR2_Scope.GUIColor(Color.white.Alpha(0.1f)))
            {
                GUI.DrawTexture(plusRect, FR2_Icon.Plus.image, ScaleMode.ScaleToFit);
            }

            GUI.Label(rect, title, EditorStyles.wordWrappedMiniLabel);

            EventType eventType = Event.current.type;
            if (eventType == EventType.DragUpdated || eventType == EventType.DragPerform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (eventType == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Event.current.Use();
                    return DragAndDrop.objectReferences;
                }
                Event.current.Use();
            }

            return null;
        }

        public static bool Toggle(Rect rect, ref bool value)
        {
            bool vv = GUI.Toggle(rect, value, GUIContent.none);
            if (vv == value) return false;
            value = vv;
            Event.current.Use();
            return true;
        }

        public static Rect Padding(Rect r, float x, float y)
        {
            return new Rect(r.x + x, r.y + y, r.width - 2 * x, r.height - 2 * y);
        }

        public static Rect LeftRect(float w, ref Rect rect)
        {
            rect.x += w;
            rect.width -= w;
            return new Rect(rect.x - w, rect.y, w, rect.height);
        }

        public static Rect RightRect(float w, ref Rect rect)
        {
            rect.width -= w;
            return new Rect(rect.x + rect.width, rect.y, w, rect.height);
        }

        public static bool DrawClipped(Rect rect, Func<bool> drawLayout)
        {
            GUI.BeginClip(rect);
            GUILayout.BeginArea(new Rect(0, 0, rect.width, rect.height));
            bool result = drawLayout();
            GUILayout.EndArea();
            GUI.EndClip();
            return result;
        }
    }
}
