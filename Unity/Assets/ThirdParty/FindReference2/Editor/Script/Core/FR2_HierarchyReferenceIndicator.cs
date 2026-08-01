using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    internal static class FR2_HierarchyReferenceIndicator
    {
#if UNITY_6000_4_OR_NEWER
        private static readonly Dictionary<ulong, ReferenceInfo> _referenceCache = new Dictionary<ulong, ReferenceInfo>();
#else
        private static readonly Dictionary<int, ReferenceInfo> _referenceCache = new Dictionary<int, ReferenceInfo>();
#endif
        private static bool _isInitialized = false;
        private static GUIStyle _numberStyle;
        private static GUIStyle _badgeNumberStyle;
        private static Texture _cachedBadgeIcon;

        private class ReferenceInfo
        {
            public readonly int referenceCount;
            public GUIContent countText;
            public Vector2 textSize;
            public Vector2 badgeTextSize;

            public ReferenceInfo(int count)
            {
                referenceCount = count;
                countText = FR2_GUIContent.From(count.ToString());
                textSize = _numberStyle.CalcSize(countText);
                if (_badgeNumberStyle != null)
                    badgeTextSize = _badgeNumberStyle.CalcSize(countText);
            }
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyGUIByEntityId;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUIByEntityId;
#else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
#endif
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            FR2_SceneCache.onReady -= OnSceneCacheReady;
            FR2_SceneCache.onReady += OnSceneCacheReady;

            _referenceCache.Clear();
            _isInitialized = true;

            EditorApplication.update -= GetNumberStyle;
            EditorApplication.update += GetNumberStyle;
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            _isInitialized = false;
            Initialize();
        }

#if UNITY_6000_4_OR_NEWER
        private static ulong _lastDrawnEntityId;
        private static float _lastDrawnRectWidth;

        private static void OnHierarchyGUIByEntityId(EntityId entityId, Rect selectionRect)
        {
            if (!_isInitialized) return;
            if (FR2_SettingExt.disable) return;
            if (!FR2_SettingExt.showHierarchyReferenceCount) return;
            if (Event.current.type != EventType.Repaint) return;
            if (!FR2_SceneCache.hasCache) return;
            if (_numberStyle == null) return;

            ulong key = FR2_Unity.EntityIdToULong(entityId);
            if (key == _lastDrawnEntityId && selectionRect.width < _lastDrawnRectWidth) return;
            _lastDrawnEntityId = key;
            _lastDrawnRectWidth = selectionRect.width;

            try
            {
                if (!_referenceCache.TryGetValue(key, out var refInfo))
                {
                    GameObject go = FR2_Unity.EntityIdToObject(entityId) as GameObject;
                    if (go == null) return;

                    int count = FR2_SceneCache.Api.GetGameObjectUsageCount(go);
                    if (count <= 0) return;

                    refInfo = new ReferenceInfo(count);
                    _referenceCache[key] = refInfo;
                }

                if (refInfo.referenceCount == 0) return;
                DrawReferenceIndicator(refInfo, selectionRect);
            }
            catch (Exception ex)
            {
                FR2_LOG.LogWarning($"[FR2] OnHierarchyGUI exception (suppressed): {ex.Message}");
            }
        }
#else
        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            if (!_isInitialized) return;
            if (FR2_SettingExt.disable) return;
            if (!FR2_SettingExt.showHierarchyReferenceCount) return;
            if (Event.current.type != EventType.Repaint) return;
            if (!FR2_SceneCache.hasCache) return;
            if (_numberStyle == null) return;

            try
            {
                if (!_referenceCache.TryGetValue(instanceID, out var refInfo))
                {
                    GameObject go = FR2_Unity.InstanceIdToObject(instanceID) as GameObject;
                    if (go == null) return;

                    int count = FR2_SceneCache.Api.GetGameObjectUsageCount(go);
                    if (count <= 0) return;

                    refInfo = new ReferenceInfo(count);
                    _referenceCache[instanceID] = refInfo;
                }

                if (refInfo.referenceCount == 0) return;
                DrawReferenceIndicator(refInfo, selectionRect);
            }
            catch (Exception ex)
            {
                FR2_LOG.LogWarning($"[FR2] OnHierarchyGUI exception (suppressed): {ex.Message}");
            }
        }
#endif

        private static void DrawReferenceIndicator(ReferenceInfo refInfo, Rect selectionRect)
        {
            var offset = FR2_SettingExt.hierarchyReferenceCountOffset;

            if (FR2_Setting.BadgeReferenceCount)
            {
                if (_badgeNumberStyle == null) return;

                var textSize = refInfo.badgeTextSize;
                var badgeSize = Mathf.Max(16f, textSize.x + 8f);
                var badgeRect = new Rect(selectionRect.xMax - badgeSize - offset - 2f, selectionRect.y - 1f, badgeSize + 4f, 18f);
                if (_cachedBadgeIcon == null)
                    _cachedBadgeIcon = EditorGUIUtility.IconContent("sv_icon_dot0_pix16_gizmo").image;

                using (FR2_Scope.GUIColor(Color.black.Alpha(0.5f)))
                    GUI.DrawTexture(badgeRect, _cachedBadgeIcon);

                GUI.Label(badgeRect, refInfo.countText, _badgeNumberStyle);
            }
            else
            {
                var textSize = refInfo.textSize;
                var indicatorRect = new Rect(selectionRect.xMax - textSize.x - offset, selectionRect.y, textSize.x, selectionRect.height);
                GUI.Label(indicatorRect, refInfo.countText, _numberStyle);
            }
        }

        private static void GetNumberStyle()
        {
            if (_numberStyle != null && _badgeNumberStyle != null)
            {
                EditorApplication.update -= GetNumberStyle;
                return;
            }

            GUIStyle miniLabel = null;
            try { miniLabel = EditorStyles.miniLabel; }
            catch { }

            if (miniLabel == null) return;

            if (_numberStyle == null)
            {
                _numberStyle = new GUIStyle(miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) }
                };
            }

            if (_badgeNumberStyle == null)
            {
                _badgeNumberStyle = new GUIStyle(miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 8
                };
            }
        }

        private static void OnSceneCacheReady()
        {
            _referenceCache.Clear();
            EditorApplication.RepaintHierarchyWindow();
        }

        public static void ClearCache()
        {
            _referenceCache.Clear();
        }

        private static void OnSelectionChanged()
        {
            if (FR2_SettingExt.disable) return;
            if (!FR2_SettingExt.isAutoRefreshEnabled) return;
            if (!FR2_Cache.isReady) return;

            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0) return;

            bool changed = false;
            for (int i = 0; i < selected.Length; i++)
            {
                if (!selected[i]) continue;
#if UNITY_6000_4_OR_NEWER
                ulong key = FR2_Unity.GetEntityIdKey(selected[i]);
                if (_referenceCache.Remove(key)) changed = true;
#else
                int key = selected[i].GetInstanceID();
                if (_referenceCache.Remove(key)) changed = true;
#endif
            }

            if (changed) EditorApplication.RepaintHierarchyWindow();
        }

        public static void SetEnabled(bool enabled)
        {
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
