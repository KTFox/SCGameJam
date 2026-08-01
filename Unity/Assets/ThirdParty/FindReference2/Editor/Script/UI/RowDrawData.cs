using System;
using UnityEngine;

namespace vietlabs.fr2
{
    internal enum ColumnAlign { Left, Right }

    internal enum RowState
    {
        Normal = 0,
        Active = 1,
    }

    internal struct RowSelection
    {
        public bool selected;
        public Color color;

        public static readonly RowSelection None = new RowSelection { selected = false };
        public static readonly RowSelection Blue = new RowSelection { selected = true, color = new Color(0.24f, 0.48f, 0.9f, 0.3f) };
        public static readonly RowSelection Green = new RowSelection { selected = true, color = new Color(0.24f, 0.5f, 0.24f, 0.3f) };
    }

    internal class MetadataColumn
    {
        public string key;
        public float width;
        public ColumnAlign align;
        public GUIStyle style;
        public bool visible;

        public MetadataColumn(string key, float width, ColumnAlign align = ColumnAlign.Right, GUIStyle style = null, bool visible = true)
        {
            this.key = key;
            this.width = width;
            this.align = align;
            this.style = style;
            this.visible = visible;
        }

        public void UpdateWidth(float newWidth)
        {
            if (newWidth <= width) return;
            width = newWidth;
            widthChanged = true;
        }

        public bool widthChanged;
    }

    internal class RowDrawData
    {
        public Texture icon;
        public GUIContent nameContent;
        public GUIContent secondaryContent;
        public GUIContent pathContent;

        public float nameWidth;
        public float secondaryWidth;
        public float pathWidth;
        public bool secondaryHighPriority;

        public bool showPath;
        public bool isMissing;
        public Color? nameColor;
        public RowState state;
        public RowSelection selection;
        public float selectionPadLeft;

        public Action onPing;
        public Action onOpen;
        public Action onContextMenu;

        public bool showCheckbox;
        public bool checkboxDisabled;
        public bool checkboxValue;
        public Action<bool> onCheckboxChanged;
        public Action onCheckboxShiftClick;
        public Action onCheckboxAltClick;
        public Action onCheckboxCtrlClick;

        public Action[] hoverActions;
        public GUIContent[] hoverLabels;
        public int hoverActionCount;

        private const int MAX_COLUMNS = 8;
        private const int MAX_HOVER = 4;

        public int leftColumnValueCount;
        public GUIContent[] leftColumnValues;

        public int rightColumnValueCount;
        public GUIContent[] rightColumnValues;
        public GUIContent[] rightColumnShortValues;
        public float[] rightColumnShortWidths;
        public Action onRightColumnClick;

        public RowDrawData()
        {
            leftColumnValues = new GUIContent[MAX_COLUMNS];
            rightColumnValues = new GUIContent[MAX_COLUMNS];
            rightColumnShortValues = new GUIContent[MAX_COLUMNS];
            rightColumnShortWidths = new float[MAX_COLUMNS];
            hoverActions = new Action[MAX_HOVER];
            hoverLabels = new GUIContent[MAX_HOVER];
        }

        public void ClearColumns()
        {
            leftColumnValueCount = 0;
            rightColumnValueCount = 0;
            for (int i = 0; i < MAX_COLUMNS; i++)
            {
                leftColumnValues[i] = null;
                rightColumnValues[i] = null;
                rightColumnShortValues[i] = null;
                rightColumnShortWidths[i] = 0f;
            }
        }

        public void SetLeftColumnValue(int index, GUIContent content)
        {
            if (index < 0 || index >= MAX_COLUMNS) return;
            leftColumnValues[index] = content;
            if (index >= leftColumnValueCount) leftColumnValueCount = index + 1;
        }

        public void SetRightColumnValue(int index, GUIContent content)
        {
            if (index < 0 || index >= MAX_COLUMNS) return;
            rightColumnValues[index] = content;
            if (index >= rightColumnValueCount) rightColumnValueCount = index + 1;
        }

        public void ClearHoverActions()
        {
            hoverActionCount = 0;
        }

        public void AddHoverAction(GUIContent label, Action action)
        {
            if (hoverActionCount >= MAX_HOVER) return;
            hoverLabels[hoverActionCount] = label;
            hoverActions[hoverActionCount] = action;
            hoverActionCount++;
        }
    }
}
