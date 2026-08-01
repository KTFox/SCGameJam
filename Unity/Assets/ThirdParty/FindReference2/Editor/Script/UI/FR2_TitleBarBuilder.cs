using System;
using UnityEngine;

namespace vietlabs.fr2
{
    internal struct FR2_TitleBarBuilder
    {
        private const float BTN_W = 24f;
        private const float GAP = 0f;
        private const float SPACE = 2f;
        private const float DROP_W = 80f;
        private const float H = 18f;

        private float _x;
        private readonly float _y;
        private bool _changed;

        public bool Changed => _changed;
        public float X => _x;

        public FR2_TitleBarBuilder(float rightEdge, float y)
        {
            _x = rightEdge;
            _y = y;
            _changed = false;
        }

        public FR2_TitleBarBuilder(Rect titleRect, float rightEdge)
        {
            _x = rightEdge;
            _y = titleRect.y;
            _changed = false;
        }

        public FR2_TitleBarBuilder AddButton(GUIContent icon, Action onClick)
        {
            _x -= BTN_W;
            if (FR2_ToolbarButton.Button(new Rect(_x, _y, BTN_W, H), icon))
            {
                onClick?.Invoke();
            }
            _x -= GAP;
            return this;
        }

        public FR2_TitleBarBuilder AddToggle(ref bool value, GUIContent icon, Action<bool> onChange = null)
        {
            _x -= BTN_W;
            if (FR2_ToolbarButton.Toggle(new Rect(_x, _y, BTN_W, H), ref value, icon))
            {
                _changed = true;
                onChange?.Invoke(value);
            }
            _x -= GAP;
            return this;
        }

        public FR2_TitleBarBuilder AddDropdown<T>(FR2_EnumDrawer drawer, ref T value, Action<T> onChange = null, float width = DROP_W)
        {
            _x -= width;
            T v = value;
            if (drawer.Draw(new Rect(_x, _y, width, H), ref v))
            {
                value = v;
                _changed = true;
                onChange?.Invoke(v);
            }
            _x -= GAP;
            return this;
        }

        public FR2_TitleBarBuilder AddSpace(float px = SPACE)
        {
            _x -= px;
            return this;
        }
    }
}
