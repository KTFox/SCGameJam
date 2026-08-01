using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;

namespace vietlabs.fr2
{
    internal static class FR2_Badge
    {
        private static GUIStyle _numberStyle;
        private static GUIStyle numberStyle
        {
            get
            {
                if (_numberStyle != null) return _numberStyle;
                _numberStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 8
                };
                return _numberStyle;
            }
        }

        private static GUIStyle _roundStyle;
        private static GUIStyle roundStyle
        {
            get
            {
                if (_roundStyle != null) return _roundStyle;
                _roundStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(2, 2, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    fixedHeight = 0,
                    fixedWidth = 0
                };
                _roundStyle.normal.textColor = Color.white;
                return _roundStyle;
            }
        }

        private static readonly Color SelectBadgeColor = new Color(62f / 255f, 76f / 255f, 106f / 255f);
        private static float _selectBadgeWidth;
        private static Texture _cachedBadgeIcon;

        public static float SelectBadgeWidth
        {
            get
            {
                if (_selectBadgeWidth <= 0f)
                    _selectBadgeWidth = CalcRoundWidth(FR2_Icon.Pointer.image, "99+", 16f);
                return _selectBadgeWidth;
            }
        }

        public static void Draw(Vector2 pos, int number, bool rowLayout)
        {
            if (number <= 0) return;

            var content = FR2_GUIContent.FromInt(number);
            var textSize = numberStyle.CalcSize(content);
            var badgeSize = Mathf.Max(16f, textSize.x + 8f);
            var badgeRect = new Rect(pos.x - 2f, pos.y - 1f, badgeSize + 4f, 18f);
            if (_cachedBadgeIcon == null)
                _cachedBadgeIcon = EditorGUIUtility.IconContent("sv_icon_dot0_pix16_gizmo").image;
            var a = rowLayout ? 0.5f : 1f;

            using (GUIColor(Color.black.Alpha(a)))
            {
                GUI.DrawTexture(badgeRect, _cachedBadgeIcon);
            }
            GUI.Label(badgeRect, content, numberStyle);
        }

        public static bool RoundButton(Rect rect, Texture icon, string text, Color bgColor)
        {
            GUI2.Rect(rect, bgColor, 1f);

            var content = icon != null
                ? FR2_GUIContent.From(text, icon, null)
                : FR2_GUIContent.FromString(text);

            var labelRect = new Rect(rect.x + 2f, rect.y, rect.width - 2f, rect.height);
            GUI.Label(labelRect, content, roundStyle);

            return Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition);
        }

        public static float CalcRoundWidth(Texture icon, string text, float height)
        {
            var content = icon != null
                ? FR2_GUIContent.From(text, icon, null)
                : FR2_GUIContent.FromString(text);
            return roundStyle.CalcSize(content).x + 6f;
        }

        public static bool SelectBadge(Rect r, int count)
        {
            string text = count > 99 ? "99+" : count.ToString();
            float w = SelectBadgeWidth;
            float h = 14f;
            var rect = new Rect(r.xMax - w - 2f, r.y + (r.height - h) * 0.5f, w, h);
            return RoundButton(rect, FR2_Icon.Pointer.image, text, SelectBadgeColor);
        }
    }
}
