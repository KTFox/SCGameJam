//#define FR2_DEBUG

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
namespace vietlabs.fr2
{

    internal class FR2_SplitView
    {
        private const float SPLIT_SIZE = 2f;


        private readonly GUILayoutOption[] expandWH =
        {
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)
        };


        private readonly IWindow window;
        private Rect _rect;

        private int _visibleCount;
        internal bool dirty;

        public bool isHorz;

        private int resizeIndex = -1;
        public List<Info> splits = new List<Info>();
        
        public System.Action OnSplitterChanged;

        public FR2_SplitView(IWindow w)
        {
            window = w;
        }
        
        public void SetupSplitParents()
        {
            for (int i = 0; i < splits.Count; i++)
            {
                splits[i].SetParent(this);
            }
        }
        
        public void SetSplitVisible(int index, bool visible)
        {
            if (index < 0 || index >= splits.Count) return;
            splits[index].visible = visible;
        }

        public bool isVisible => _visibleCount > 0;

        public void CalculateWeight()
        {
            _visibleCount = 0;
            var totalEffective = 0f;

            for (var i = 0; i < splits.Count; i++)
            {
                Info info = splits[i];
                if (!info.visible) continue;

                info.stIndex = _visibleCount;
                totalEffective += info.effectiveSize;

                _visibleCount++;
            }

            if (_visibleCount == 0 || totalEffective == 0) return;

            for (var i = 0; i < splits.Count; i++)
            {
                Info info = splits[i];
                if (!info.visible) continue;

                info.normWeight = info.effectiveSize / totalEffective;
            }
        }

        public void DrawLayout()
        {
            using (var layoutScope = FR2_Scope.Layout(isHorz, expandWH))
            {
                Draw(layoutScope.rect);
            }
        }

        public void Draw(Rect rect)
        {
            bool rectChanged = false;
            if (rect.width > 0 || rect.height > 0) 
            {
                rectChanged = !_rect.Equals(rect);
                _rect = rect;
            }

            bool wasDirty = dirty;
            if (dirty) dirty = false;

            if (rectChanged || wasDirty) ApplySizePolicies();

            float sz = (_visibleCount - 1) * SPLIT_SIZE;
            float dx = _rect.x;
            float dy = _rect.y;

            for (var i = 0; i < splits.Count; i++)
            {
                Info info = splits[i];
                if (!info.visible) continue;

                var rr = new Rect
                (
                    dx, dy,
                    isHorz ? (_rect.width - sz) * info.normWeight : _rect.width,
                    isHorz ? _rect.height : (_rect.height - sz) * info.normWeight
                );

                if ((rr.width > 0) && (rr.height > 0)) info.rect = rr;

                if (info.draw != null) info.DoDraw();

                if (info.stIndex < _visibleCount - 1) DrawSpliter(i, isHorz ? info.rect.xMax : info.rect.yMax);

                if (isHorz)
                {
                    dx += info.rect.width + SPLIT_SIZE;
                } else
                {
                    dy += info.rect.height + SPLIT_SIZE;
                }
            }
        }

        public void ApplySizePolicies()
        {
            int visible = 0;
            for (int i = 0; i < splits.Count; i++) if (splits[i].visible) visible++;
            if (visible == 0) return;

            float totalGaps = (visible - 1) * SPLIT_SIZE;
            float available = isHorz ? _rect.width : _rect.height;
            float content = Mathf.Max(0f, available - totalGaps);

            float fixedPixels = 0f;
            float flexibleBasis = 0f;
            for (int i = 0; i < splits.Count; i++)
            {
                var sp = splits[i];
                if (!sp.visible) continue;
                if (sp.sizePolicy == Info.SizePolicy.KeepPixel)
                {
                    float pref = sp.preferredPixel > 0f ? sp.preferredPixel : sp.defaultPixel;
                    pref = Mathf.Max(sp.minPixel, pref);
                    fixedPixels += Mathf.Max(0f, pref);
                }
                else
                {
                    flexibleBasis += Mathf.Max(0.0001f, sp.weight);
                }
            }

            float scale = 1f;
            if (fixedPixels > content && fixedPixels > 0f) scale = content / fixedPixels;
            float remaining = Mathf.Max(0f, content - Mathf.Min(fixedPixels, content));

            for (int i = 0; i < splits.Count; i++)
            {
                var sp = splits[i];
                if (!sp.visible) continue;
                if (sp.sizePolicy == Info.SizePolicy.KeepPixel)
                {
                    float pref = sp.preferredPixel > 0f ? sp.preferredPixel : sp.defaultPixel;
                    pref = Mathf.Max(sp.minPixel, pref);
                    sp.effectiveSize = Mathf.Max(sp.minPixel, pref * scale);
                }
            }

            if (remaining > 0f)
            {
                float minFlexibleSpace = 0f;
                int flexibleCount = 0;
                for (int i = 0; i < splits.Count; i++)
                {
                    var sp = splits[i];
                    if (!sp.visible) continue;
                    if (sp.sizePolicy == Info.SizePolicy.Flexible)
                    {
                        minFlexibleSpace += sp.minPixel;
                        flexibleCount++;
                    }
                }

                if (remaining >= minFlexibleSpace)
                {
                    float basis = Mathf.Max(0.0001f, flexibleBasis);
                    for (int i = 0; i < splits.Count; i++)
                    {
                        var sp = splits[i];
                        if (!sp.visible) continue;
                        if (sp.sizePolicy == Info.SizePolicy.Flexible)
                        {
                            float portion = sp.weight / basis;
                            sp.effectiveSize = Mathf.Max(sp.minPixel, remaining * portion);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < splits.Count; i++)
                    {
                        var sp = splits[i];
                        if (!sp.visible) continue;
                        if (sp.sizePolicy == Info.SizePolicy.Flexible)
                        {
                            if (minFlexibleSpace > 0f)
                            {
                                float portion = sp.minPixel / minFlexibleSpace;
                                sp.effectiveSize = remaining * portion;
                            }
                            else
                            {
                                sp.effectiveSize = remaining / flexibleCount;
                            }
                        }
                    }
                }
            }

            CalculateWeight();
        }

        private void RefreshSpliterPos(int index, float px)
        {
			Info sp1 = splits[index];
			int rightIndex = -1;
            for (int j = index + 1; j < splits.Count; j++)
            {
                if (splits[j].visible)
                {
                    rightIndex = j;
                    break;
                }
            }
            if (rightIndex < 0) return;
			Info sp2 = splits[rightIndex];

            Rect r1 = sp1.rect;
            Rect r2 = sp2.rect;

			float dd = isHorz ? r2.xMax - r1.xMin - SPLIT_SIZE : r2.yMax - r1.yMin - SPLIT_SIZE;
			float m = isHorz ? Event.current.mousePosition.x - r1.x : Event.current.mousePosition.y - r1.y;

			float leftMin = sp1.minPixel;
			float rightMin = sp2.minPixel;
			float lower = Mathf.Min(dd - rightMin, leftMin);
			float upper = Mathf.Max(leftMin, dd - rightMin);
			m = Mathf.Clamp(m, lower, upper);

            bool sp1Fixed = sp1.sizePolicy == Info.SizePolicy.KeepPixel;
            bool sp2Fixed = sp2.sizePolicy == Info.SizePolicy.KeepPixel;

            if (sp1Fixed && sp2Fixed)
            {
                sp1.preferredPixel = Mathf.Max(sp1.minPixel, m);
                sp2.preferredPixel = Mathf.Max(sp2.minPixel, dd - m);
            }
            else if (sp1Fixed)
            {
                sp1.preferredPixel = Mathf.Max(sp1.minPixel, m);
            }
            else if (sp2Fixed)
            {
                sp2.preferredPixel = Mathf.Max(sp2.minPixel, dd - m);
            }
            else
            {
                float w1 = sp1.weight;
                float w2 = sp2.weight;
                float tt = w1 + w2;
                float pct = Mathf.Clamp(m / dd, 0.1f, 0.9f);
                sp1.weight = tt * pct;
                sp2.weight = tt * (1 - pct);
            }

            dirty = true;
            if (window != null) window.WillRepaint = true;
            
            OnSplitterChanged?.Invoke();
        }

        private void DrawSpliter(int index, float px)
        {
            Rect dRect = _rect;

            if (isHorz)
            {
                dRect.x = px;
                dRect.width = SPLIT_SIZE;
            } else
            {
                dRect.y = px;
                dRect.height = SPLIT_SIZE;
            }

            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.MouseMove) GUI2.Rect(dRect, Color.black, 0.4f);

            Rect dRect2 = GUI2.Padding(dRect, -2f, -2f);

            EditorGUIUtility.AddCursorRect(dRect2, isHorz ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);
            if ((Event.current.type == EventType.MouseDown) && dRect2.Contains(Event.current.mousePosition))
            {
                resizeIndex = index;
                RefreshSpliterPos(index, px);
            }

            if (resizeIndex == index) RefreshSpliterPos(index, px);

            if (Event.current.type == EventType.MouseUp) resizeIndex = -1;
        }
        
        [Serializable]
        internal class Info
        {
            public GUIContent title;
            public Rect rect;
            public float normWeight;
            public int stIndex;

            private bool _visible = true;
            private FR2_SplitView _parent;
            
            public bool visible
            {
                get => _visible;
                set
                {
                    if (_visible != value)
                    {
                        _visible = value;
                        if (_parent != null)
                        {
                            _parent.dirty = true;
                            if (_parent.window != null) _parent.window.WillRepaint = true;
                        }
                    }
                }
            }
            
            public float weight = 1f;
            public float effectiveSize;
            public Action<Rect> draw;

            public enum SizePolicy { Flexible, KeepPixel }
            public SizePolicy sizePolicy = SizePolicy.Flexible;
            public float preferredPixel;
            public float defaultPixel = 200f;
            public float minPixel = 50f;

            // Dynamic title support
            public Func<GUIContent> GetDynamicTitle;

            // Drawer dirty state support
            public Func<bool> GetDrawerDirtyState;
            
            // Refresh action support
            public Action OnRefresh;
            
            // Bookmark support
            public Func<int> GetBookmarkCount;
            public Action OnBookmarkClick;
            
            // Title bar controls hook: receives rightEdge, returns new rightEdge after drawing
            public Func<Rect, float, float> DrawTitleBarControls;
            
            internal void SetParent(FR2_SplitView parent)
            {
                _parent = parent;
            }

            public void DoDraw()
            {
                Rect drawRect = rect;
                var bottomPadding = 2f;
                GUIContent baseTitle = GetDynamicTitle?.Invoke() ?? title;

                if (baseTitle != null)
                {
                    var titleHeight = 20f;
                    var titleRect = new Rect(rect.x, rect.y, rect.width, titleHeight + bottomPadding);
                    GUI2.Rect(titleRect, Color.black, 0.2f);
                    bool isDirty = GetDrawerDirtyState?.Invoke() ?? false;
                    
                    string titleText = baseTitle.text;
                    Color? titleColor = null;
                    if (isDirty && !titleText.EndsWith("*"))
                    {
                        titleText += "*";
                        titleColor = EditorGUIUtility.isProSkin 
                            ? FR2_Theme.Dark.DirtyIndicator 
                            : FR2_Theme.Light.DirtyIndicator;
                    }

                    var rightEdge = titleRect.xMax;
                    if (OnRefresh != null)
                    {
                        var refreshWidth = 22f;
                        var refreshRect = new Rect(rightEdge - refreshWidth, titleRect.y, refreshWidth, 14f);
                        if (FR2_ToolbarButton.Button(refreshRect, EditorGUIUtility.IconContent("Refresh")))
                        {
                            OnRefresh.Invoke();
                        }
                        rightEdge -= refreshWidth;
                    }
                    
                    if (DrawTitleBarControls != null)
                    {
                        rightEdge = DrawTitleBarControls.Invoke(titleRect, rightEdge);
                    }
                    
                    if (GetBookmarkCount != null && OnBookmarkClick != null)
                    {
                        int bmCount = GetBookmarkCount.Invoke();
                        if (bmCount > 0)
                        {
                            float w = FR2_Badge.SelectBadgeWidth;
                            Rect badgeArea = new Rect(rightEdge - w, titleRect.y, w, titleHeight);
                            if (FR2_Badge.SelectBadge(badgeArea, bmCount))
                            {
                                OnBookmarkClick.Invoke();
                                Event.current.Use();
                            }
                            rightEdge -= w + 2f;
                        }
                    }

                    using (FR2_Scope.ContentColor(titleColor))
                    {
                        var labelRect = new Rect(titleRect.x + 4f, titleRect.y, rightEdge - titleRect.x - 4f, titleRect.height);
                        GUI.Label(labelRect, FR2_GUIContent.From(titleText, baseTitle.image, baseTitle.tooltip), EditorStyles.label);
                    }
                    
                    drawRect.yMin += titleHeight + bottomPadding;
                }
                
                try
                {
                    draw(drawRect);
                }
                catch (System.Exception e)
                {
                    // Log the exception but don't let it break the GUI layout system
                    UnityEngine.Debug.LogException(e);
                    
                    // Draw error message in the panel instead
                    if (Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.HelpBox(drawRect, $"Drawing error: {e.Message}", MessageType.Error);
                    }
                }
            }
        }
    }
}
