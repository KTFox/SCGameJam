using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal enum ToolbarButtonState { Normal, Active, Warning }

    internal static class FR2_ToolbarButton
    {
        private static readonly Color ActiveBgColor = new Color(0.7f, 1f, 0.7f, 1f);
        private static readonly Color WarningBgColor = Color.yellow;

        internal static bool Button(Rect r, GUIContent content, ToolbarButtonState state = ToolbarButtonState.Normal)
        {
            Color? bgColor = state == ToolbarButtonState.Active ? ActiveBgColor
                : state == ToolbarButtonState.Warning ? WarningBgColor
                : (Color?)null;
            Color contentColor = state == ToolbarButtonState.Active
                ? FR2_Theme.Current.IconActiveColor
                : FR2_Theme.Current.IconColor;

            using (FR2_Scope.BGColor(bgColor))
            using (FR2_Scope.ContentColor(contentColor))
            {
                if (!GUI.Button(r, content, EditorStyles.toolbarButton)) return false;
                Event.current.Use();
                return true;
            }
        }

        internal static bool Toggle(Rect r, ref bool value, GUIContent content, ToolbarButtonState offState = ToolbarButtonState.Normal)
        {
            var state = value ? ToolbarButtonState.Active : offState;
            Color? bgColor = state == ToolbarButtonState.Active ? ActiveBgColor
                : state == ToolbarButtonState.Warning ? WarningBgColor
                : (Color?)null;
            Color contentColor = state == ToolbarButtonState.Active
                ? FR2_Theme.Current.IconActiveColor
                : FR2_Theme.Current.IconColor;

            using (FR2_Scope.BGColor(bgColor))
            using (FR2_Scope.ContentColor(contentColor))
            {
                bool newValue = GUI.Toggle(r, value, content, EditorStyles.toolbarButton);
                if (newValue == value) return false;
                value = newValue;
                Event.current.Use();
                return true;
            }
        }

        internal static bool ToggleLayout(ref bool value, GUIContent content, ToolbarButtonState offState = ToolbarButtonState.Normal)
        {
            var state = value ? ToolbarButtonState.Active : offState;
            Color? bgColor = state == ToolbarButtonState.Active ? ActiveBgColor
                : state == ToolbarButtonState.Warning ? WarningBgColor
                : (Color?)null;
            Color contentColor = state == ToolbarButtonState.Active
                ? FR2_Theme.Current.IconActiveColor
                : FR2_Theme.Current.IconColor;

            using (FR2_Scope.BGColor(bgColor))
            using (FR2_Scope.ContentColor(contentColor))
            {
                bool newValue = GUILayout.Toggle(value, content, EditorStyles.toolbarButton, GUI2.GLW_24);
                if (newValue == value) return false;
                value = newValue;
                Event.current.Use();
                return true;
            }
        }
    }
}
