using System;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal static class FR2_RowDrawer
    {
        private const float ICON_SIZE = 16f;
        private const float ICON_GAP = 2f;
        private const float MIN_GAP = 20f;
        private const float MAX_RIGHT_COL_RATIO = 0.3f;
        private const float MIN_RIGHT_COL_W = 50f;
        private static GUIContent _missingContent;
        private static GUIContent MissingContent => _missingContent ??= new GUIContent("(missing)");
        private const float HOVER_BTN_W = 20f;

        public static void DrawSimple(Rect r, Texture icon, string name, string ext, bool highlight, Action onPing, Action onSelect)
        {
            Event evt = Event.current;
            bool isRepaint = evt.type == EventType.Repaint;

            if (highlight && isRepaint)
            {
                Rect selRect = new Rect(0f, r.y, r.xMax, r.height);
                EditorGUI.DrawRect(selRect, RowSelection.Blue.color);
            }

            if (icon != null)
            {
                Rect iconRect = new Rect(r.x, r.y, ICON_SIZE, ICON_SIZE);
                GUI.DrawTexture(iconRect, icon);
                if (evt.type == EventType.MouseDown && evt.button == 0 && iconRect.Contains(evt.mousePosition))
                {
                    onPing?.Invoke();
                    evt.Use();
                    return;
                }
                r.xMin += ICON_SIZE + ICON_GAP;
            }

            GUIContent nameContent = FR2_GUIContent.FromString(name);
            float nameW = EditorStyles.label.CalcSize(nameContent).x;
            GUIContent extContent = null;
            float extW = 0f;
            if (!string.IsNullOrEmpty(ext))
            {
                extContent = FR2_GUIContent.FromString(ext);
                extW = EditorStyles.miniLabel.CalcSize(extContent).x;
            }

            if (isRepaint)
            {
                Rect nameRect = r;
                nameRect.width = Mathf.Min(nameW, r.width - extW);
                GUI.Label(nameRect, nameContent, EditorStyles.label);
                if (extContent != null && nameRect.width + extW <= r.width)
                {
                    Rect extRect = r;
                    extRect.xMin = nameRect.xMin + nameRect.width;
                    extRect.y += 1f;
                    GUI.Label(extRect, extContent, FR2_Theme.Current.AssetPathNormal);
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && r.Contains(evt.mousePosition))
            {
                if (evt.clickCount == 2) onPing?.Invoke();
                else onSelect?.Invoke();
                evt.Use();
            }
        }


        public static float Draw(Rect r, RowDrawData data, MetadataColumn[] leftColumns = null, MetadataColumn[] rightColumns = null)
        {
            if (data == null) return 0f;

            Event evt = Event.current;
            bool isRepaint = evt.type == EventType.Repaint;
            bool isMouse = evt.isMouse;
            if (!isRepaint && !isMouse) return FR2_Theme.Current.TreeItemHeight;

            bool isHover = r.Contains(evt.mousePosition);
            r.height = FR2_Theme.Current.TreeItemHeight;

            if (isRepaint && data.selection.selected)
            {
                var theme = FR2_Theme.Current;
                float selH = theme.TreeItemHeight + theme.TreeItemSpacing;
                float selX = r.x - data.selectionPadLeft;
                Rect selRect = new Rect(selX, r.y, r.x + r.width - selX + theme.ScrollBarWidth, selH);
                EditorGUI.DrawRect(selRect, data.selection.color);
            }

            if (data.isMissing)
            {
                if (isRepaint) GUI.Label(r, data.nameContent ?? MissingContent, EditorStyles.whiteBoldLabel);
                return FR2_Theme.Current.TreeItemHeight;
            }

            if (data.showCheckbox) DrawCheckbox(ref r, data);
            DrawColumnsLeft(ref r, data, leftColumns, isRepaint);
            float mouseStartX = r.x;
            DrawIcon(ref r, data, isRepaint);
            r.xMax -= 8f;

            float rightColW = CalculateColumnsWidth(rightColumns);
            float maxRightColW = r.width * MAX_RIGHT_COL_RATIO;
            bool useShortRight = false;
            if (rightColW > maxRightColW)
            {
                if (maxRightColW <= MIN_RIGHT_COL_W)
                {
                    useShortRight = true;
                    rightColW = CalculateColumnsShortWidth(rightColumns, data);
                }
                else
                {
                    rightColW = maxRightColW;
                }
            }
            float hoverW = data.hoverActionCount * (HOVER_BTN_W + 2f);
            float btnStartX = r.xMax - rightColW - hoverW;

            float mouseW = r.xMax - mouseStartX - rightColW - (isHover ? hoverW : 0f);
            Rect mouseRect = new Rect(mouseStartX, r.y, mouseW, r.height);
            HandleMouse(mouseRect, data, evt);

            if (isMouse && data.onRightColumnClick != null && rightColW > 0f)
            {
                Rect rightClickRect = new Rect(r.xMax - rightColW, r.y, rightColW, r.height);
                if (evt.type == EventType.MouseDown && evt.button == 0 && rightClickRect.Contains(evt.mousePosition))
                {
                    data.onRightColumnClick.Invoke();
                    evt.Use();
                    return FR2_Theme.Current.TreeItemHeight;
                }
            }

            if (isRepaint)
            {
                Rect rightRect = r;
                DrawColumnsRight(ref rightRect, data, rightColumns, rightColW, useShortRight);

                Rect leftRect = r;
                leftRect.xMax = rightRect.xMax - (isHover ? hoverW : 0f) - MIN_GAP;
                DrawLabels(leftRect, data);
            }

            if (isHover && data.hoverActionCount > 0)
            {
                Rect btnArea = new Rect(btnStartX, r.y, hoverW, r.height);
                for (int i = 0; i < data.hoverActionCount; i++)
                {
                    Rect btnRect = new Rect(btnArea.x + i * (HOVER_BTN_W + 2f), btnArea.y, HOVER_BTN_W, btnArea.height);
                    if (isRepaint)
                        GUI.Button(btnRect, data.hoverLabels[i], EditorStyles.miniButton);
                    else if (isMouse && GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
                    {
                        data.hoverActions[i]?.Invoke();
                        return FR2_Theme.Current.TreeItemHeight;
                    }
                }
            }

            return FR2_Theme.Current.TreeItemHeight;
        }

        private static float CalculateColumnsWidth(MetadataColumn[] columns)
        {
            if (columns == null) return 0f;
            float w = 0f;
            for (int i = 0; i < columns.Length; i++)
            {
                if (!columns[i].visible || columns[i].width <= 0f) continue;
                w += columns[i].width + 4f;
            }
            return w;
        }

        private static float CalculateColumnsShortWidth(MetadataColumn[] columns, RowDrawData data)
        {
            if (columns == null) return 0f;
            float w = 0f;
            for (int i = 0; i < columns.Length; i++)
            {
                if (!columns[i].visible || columns[i].width <= 0f) continue;
                if (data.rightColumnShortValues[i] != null)
                    w += data.rightColumnShortWidths[i] + 4f;
                else
                    w += columns[i].width + 4f;
            }
            return w;
        }

        private static void DrawColumnsLeft(ref Rect r, RowDrawData data, MetadataColumn[] columns, bool isRepaint)
        {
            if (columns == null || columns.Length == 0) return;

            for (int i = 0; i < columns.Length; i++)
            {
                var col = columns[i];
                if (!col.visible || col.width <= 0f) continue;

                Rect colRect = GUI2.LeftRect(col.width + 4f, ref r);

                if (isRepaint && i < data.leftColumnValueCount && data.leftColumnValues[i] != null)
                {
                    using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
                    {
                        var style = col.style ?? (col.align == ColumnAlign.Right ? GUI2.miniLabelAlignRight : EditorStyles.miniLabel);
                        GUI.Label(colRect, data.leftColumnValues[i], style);
                    }
                }
            }
        }

        private static void DrawColumnsRight(ref Rect r, RowDrawData data, MetadataColumn[] columns, float maxWidth, bool useShort)
        {
            if (columns == null || columns.Length == 0) return;

            Rect area = GUI2.RightRect(maxWidth, ref r);

            using (FR2_Scope.GUIColor(FR2_Theme.Current.SecondaryTextColor))
            {
                float currentX = area.x;
                for (int i = 0; i < columns.Length; i++)
                {
                    var col = columns[i];
                    if (!col.visible || col.width <= 0f) continue;
                    if (i >= data.rightColumnValueCount || data.rightColumnValues[i] == null) continue;

                    bool hasShort = useShort && data.rightColumnShortValues[i] != null;
                    var content = hasShort ? data.rightColumnShortValues[i] : data.rightColumnValues[i];
                    float colW = hasShort ? data.rightColumnShortWidths[i] + 4f : col.width + 4f;
                    float available = area.xMax - currentX;
                    if (available <= 0f) break;

                    float drawW = Mathf.Min(colW, available);
                    Rect colRect = new Rect(currentX, area.y, drawW, area.height);

                    if (colW > available)
                    {
                        GUI.BeginClip(colRect);
                        GUI.Label(new Rect(0, 0, colW, colRect.height), content, EditorStyles.miniLabel);
                        GUI.EndClip();
                    }
                    else
                    {
                        GUI.Label(colRect, content, EditorStyles.miniLabel);
                    }

                    if (!string.IsNullOrEmpty(content.tooltip))
                        GUI.Label(colRect, FR2_GUIContent.Tooltip(content.tooltip), GUIStyle.none);

                    currentX += drawW;
                }
            }
        }


        private const float CHECKBOX_SIZE = 16f;

        private static void DrawCheckbox(ref Rect r, RowDrawData data)
        {
            Rect cbRect = GUI2.LeftRect(CHECKBOX_SIZE, ref r);
            GUI2.LeftRect(2f, ref r);
            
            if (data.checkboxDisabled) return;
            
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && cbRect.Contains(evt.mousePosition))
            {
                bool ctrl = Application.platform == RuntimePlatform.OSXEditor ? evt.command : evt.control;
                if (evt.shift && data.onCheckboxShiftClick != null) { data.onCheckboxShiftClick(); evt.Use(); return; }
                if (evt.alt && data.onCheckboxAltClick != null) { data.onCheckboxAltClick(); evt.Use(); return; }
                if (ctrl && data.onCheckboxCtrlClick != null) { data.onCheckboxCtrlClick(); evt.Use(); return; }
            }
            
            bool newVal = GUI.Toggle(cbRect, data.checkboxValue, GUIContent.none);
            if (newVal != data.checkboxValue)
            {
                data.onCheckboxChanged?.Invoke(newVal);
                Event.current.Use();
            }
        }

        private static void DrawIcon(ref Rect r, RowDrawData data, bool isRepaint)
        {
            Rect iconRect = GUI2.LeftRect(ICON_SIZE, ref r);
            GUI2.LeftRect(ICON_GAP, ref r);
            if (isRepaint && data.icon != null)
                GUI.DrawTexture(iconRect, data.icon, ScaleMode.ScaleToFit);
        }

        private static void DrawLabels(Rect r, RowDrawData data)
        {
            float overlapW = 2f;
            var theme = FR2_Theme.Current;
            float currentX = r.x;

            bool hasSecondary = data.secondaryContent != null && data.secondaryWidth > 0f;
            float maxLeftWidth = data.pathWidth + data.nameWidth + (hasSecondary ? data.secondaryWidth : 0f);
            bool showSecondary = hasSecondary;
            if (hasSecondary && !data.secondaryHighPriority && r.width < maxLeftWidth)
                showSecondary = false;

            if (data.showPath && data.pathContent != null)
            {
                float pathW = r.width - data.nameWidth - (showSecondary ? data.secondaryWidth : 0f);
                if (pathW > 0f)
                {
                    var pathRect = new Rect(currentX, r.y, pathW, r.height);
                    float actualPathWidth = ClippedLabel.Draw(pathRect, data.pathContent.text, theme.AssetPathNormal);
                    currentX += actualPathWidth - overlapW;
                }
            }

            float secW = showSecondary ? data.secondaryWidth : 0f;
            float nameAvail = Mathf.Max(0f, r.xMax - currentX - secW);
            var nameRect = new Rect(currentX, r.y, Mathf.Min(data.nameWidth + 2f, nameAvail), r.height);

            bool isSelected = data.selection.selected;
            bool isActive = data.state == RowState.Active;

            GUIStyle nameStyle;
            if (isSelected) nameStyle = theme.AssetNameSelected;
            else nameStyle = theme.AssetNameNormal;

            if (data.nameContent != null && nameRect.width > 0f)
            {
                Color? effectiveColor = null;
                if (isActive) effectiveColor = new Color(0.4f, 0.7f, 1f, 1f);
                if (data.nameColor.HasValue) effectiveColor = data.nameColor.Value;

                using (FR2_Scope.ContentColor(effectiveColor))
                {
                    ClippedLabel.Draw(nameRect, data.nameContent.text, nameStyle);
                }
            }

            if (showSecondary)
            {
                var secStyle = isSelected ? theme.AssetPathSelected : theme.AssetPathNormal;
                float secX = nameRect.x + nameRect.width - overlapW;
                float secMax = Mathf.Min(secX + data.secondaryWidth, r.xMax);
                var secRect = new Rect(secX, r.y + 1f, Mathf.Max(0f, secMax - secX), r.height);
                if (secRect.width > 0f) GUI.Label(secRect, data.secondaryContent, secStyle);
            }
        }

        private static void HandleMouse(Rect r, RowDrawData data, Event evt)
        {
            if (evt.type != EventType.MouseDown || !r.Contains(evt.mousePosition)) return;

            if (evt.button == 1)
            {
                data.onContextMenu?.Invoke();
                evt.Use();
                return;
            }

            if (evt.button == 0)
            {
                if (evt.clickCount == 2) { data.onOpen?.Invoke(); return; }
                data.onPing?.Invoke();
            }
        }
    }
}
