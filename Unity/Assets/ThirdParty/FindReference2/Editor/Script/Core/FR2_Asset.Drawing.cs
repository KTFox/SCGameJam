using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal partial class FR2_Asset
    {
        // ----------------------------- UI DRAWING & USER ACTIONS ---------------------------------------
        // PERFORMANCE OPTIMIZATIONS APPLIED:
        // 1. Per-asset GUI content caching using FR2_GUIContent shared cache
        // 2. Pre-calculated text widths to eliminate CalcSize() calls during drawing
        // 3. Cached icons, file sizes, and usage icons
        // 4. Extracted methods to reduce main Draw() complexity and branching
        // 5. Optimized color operations and removed duplicate GUI calls
        // 6. Early return for non-repaint events
        // 7. Proper cache invalidation when asset data changes

        internal void PopulateRowData(RowDrawData row, bool showPath, IWindow window,
            Action onShowDetails = null, FR2_Ref refContext = null, Color? nameColor = null, bool showExtension = true)
        {
            var cache = GetDrawCache();

            row.nameContent = cache.assetNameContent;
            row.secondaryContent = showExtension ? cache.extensionContent : null;
            row.icon = cache.cachedIcon;
            row.nameWidth = cache.assetNameWidthLabel;
            row.secondaryWidth = showExtension ? cache.extensionWidth : 0f;
            row.secondaryHighPriority = true;
            row.showPath = showPath;
            row.pathContent = showPath ? FR2_GUIContent.FromString(assetFolder) : null;
            row.pathWidth = showPath ? cache.assetFolderWidth : 0f;
            row.nameColor = nameColor;
            row.isMissing = IsMissing;

            row.state = RowState.Normal;
            row.selection = IsInUnitySelection() ? RowSelection.Blue 
                : !isBuiltIn && FR2_Bookmark.Contains(guid) ? RowSelection.Green 
                : RowSelection.None;

            row.onPing = Ping;
            row.onOpen = Open;
            row.onContextMenu = ShowContextMenu;

            row.ClearHoverActions();
            if (onShowDetails != null)
                row.AddHoverAction(FR2_GUIContent.FromString("...", "Show Details"), onShowDetails);
#if UNITY_2022_3_OR_NEWER
            row.AddHoverAction(FR2_GUIContent.FromString("P", "Open Properties"), OpenProperties);
#endif

            row.ClearColumns();
        }

        internal void SetColumnUsedByCount(RowDrawData row, int colIndex, MetadataColumn col)
        {
            int count = _usedByMap?.Count ?? 0;
            if (count <= 0) return;
            row.SetLeftColumnValue(colIndex, FR2_GUIContent.FromInt(count));
        }

        internal void SetColumnFileSize(RowDrawData row, int colIndex, MetadataColumn col)
        {
            if (fileSize <= 0) return;
            var cache = GetDrawCache();
            if (cache.fileSizeContent == null) return;
            row.SetRightColumnValue(colIndex, cache.fileSizeContent);
            col.UpdateWidth(cache.fileSizeWidth);
        }

        internal void SetColumnAddressable(RowDrawData row, int colIndex, MetadataColumn col)
        {
            var cache = GetDrawCache();
            if (cache.addressableContent == null) return;
            row.SetRightColumnValue(colIndex, cache.addressableContent);
            col.UpdateWidth(cache.addressableWidth);
        }

        internal void SetColumnAtlas(RowDrawData row, int colIndex, MetadataColumn col)
        {
            var cache = GetDrawCache();
            if (cache.atlasContent == null) return;
            row.SetRightColumnValue(colIndex, cache.atlasContent);
            col.UpdateWidth(cache.atlasWidth);
        }

        internal void SetColumnAssetBundle(RowDrawData row, int colIndex, MetadataColumn col)
        {
            var cache = GetDrawCache();
            if (cache.assetBundleContent == null) return;
            row.SetRightColumnValue(colIndex, cache.assetBundleContent);
            col.UpdateWidth(cache.assetBundleWidth);
        }
        
        internal GenericMenu AddArray(
            GenericMenu menu, System.Collections.Generic.List<string> list, string prefix, string title,
            string emptyTitle, bool showAsset, int max = 10)
        {
            menu.AddItem(FR2_GUIContent.FromString(emptyTitle), true, null);
            return menu;
        }

        internal void CopyGUID()
        {
            EditorGUIUtility.systemCopyBuffer = guid;
            Debug.Log(guid);
        }

        internal void CopyName()
        {
            EditorGUIUtility.systemCopyBuffer = m_assetName;
            Debug.Log(m_assetName);
        }

        internal void CopyAssetPath()
        {
            EditorGUIUtility.systemCopyBuffer = m_assetPath;
            Debug.Log(m_assetPath);
        }

        internal void CopyAssetPathFull()
        {
            string fullName = new FileInfo(m_assetPath).FullName;
            EditorGUIUtility.systemCopyBuffer = fullName;
            Debug.Log(fullName);
        }


        internal void RemoveFromSelection()
        {
            if (FR2_Bookmark.Contains(guid)) FR2_Bookmark.Remove(guid);
        }

        internal void AddToSelection()
        {
            if (!FR2_Bookmark.Contains(guid)) FR2_Bookmark.Add(guid);
        }

        private bool IsInUnitySelection()
        {
            var manager = FR2_SelectionManager.Instance;
            if (manager == null) return false;
            return manager.AssetSelection.Contains(guid);
        }

        internal void Ping()
        {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject)));
        }

        internal void Open()
        {
            var path = m_assetPath;
            EditorApplication.delayCall += () =>
            {
                var obj = AssetDatabase.LoadAssetAtPath(path, typeof(UnityObject));
                if (obj != null) AssetDatabase.OpenAsset(obj);
            };
        }

        internal void OpenProperties()
        {
#if UNITY_2022_3_OR_NEWER
            var obj = AssetDatabase.LoadAssetAtPath(m_assetPath, typeof(UnityObject));
            if (obj != null)
            {
                EditorUtility.OpenPropertyEditor(obj);
            }
#endif
        }

        private void ShowContextMenu()
        {
            var menu = new GenericMenu();
            if (extension == ".prefab") menu.AddItem(FR2_GUIContent.FromString("Edit in Scene"), false, EditPrefab);

            menu.AddItem(FR2_GUIContent.FromString("Open"), false, Open);
            menu.AddItem(FR2_GUIContent.FromString("Ping"), false, Ping);
            #if UNITY_2022_3_OR_NEWER
            menu.AddItem(FR2_GUIContent.FromString("Properties..."), false, OpenProperties);
            #endif
            menu.AddItem(FR2_GUIContent.FromString(guid), false, CopyGUID);

            menu.AddSeparator(string.Empty);
            menu.AddItem(FR2_GUIContent.FromString("Copy path"), false, CopyAssetPath);
            menu.AddItem(FR2_GUIContent.FromString("Copy full path"), false, CopyAssetPathFull);

            menu.ShowAsContext();
        }

        internal void EditPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m_assetPath);
            if (prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
            }
        }
    }
} 