using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static vietlabs.fr2.FR2_Scope;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal class FR2_Bookmark : IRefDraw
    {
        internal static readonly HashSet<string> guidSet = new HashSet<string>();
        internal static readonly HashSet<string> instSet = new HashSet<string>(); // Do not reference directly to SceneObject (which might be destroyed anytime)

        // ------------ instance
        private static bool dirty;
        internal readonly FR2_RefDrawer drawer;
        internal Dictionary<string, FR2_Ref> refs = new Dictionary<string, FR2_Ref>();

        public FR2_Bookmark(IWindow window, Func<FR2_RefDrawer.Sort> getSortMode, Func<string> getGroupMode)
        {
            this.window = window;
			drawer = new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
			{
				window = window,
				getSortMode = getSortMode,
				getGroupMode = () => FR2_RefDrawer.GroupMode.None,
				showFullPath = false,
				showFileSize = false,
				showExtension = true,
				showUsageType = false,
				showAssetBundleName = false,
				showAtlasName = false,
				showToggle = true,
				showHighlight = false,
				selectionPadLeft = FR2_WindowAll.BOOKMARK_SELECTION_PAD_LEFT,
				shouldShowExtension = () => true,
				shouldShowDetailButton = () => false,
				onCacheInvalidated = () => { }
			})
            {
                messageNoRefs = "Do bookmark something!",
                groupDrawer =
                {
                    hideGroupIfPossible = true
                },
                level0Group = string.Empty,
                customGetGroup = _ => string.Empty,
                paddingLeft = -4f
            };

            dirty = true;
            drawer.SetDirty();
        }

        public static int Count => guidSet.Count + instSet.Count;

        public IWindow window { get; set; }

        public int ElementCount()
        {
            return refs == null ? 0 : refs.Count;
        }

        public bool DrawLayout()
        {
            if (dirty) RefreshView();
            return drawer.DrawLayout();
        }

        public bool Draw(Rect rect)
        {
            if (dirty) RefreshView();
            if (refs == null)
            {
                FR2_LOG.LogWarning("Refs is null!");
                return false;
            }

            var bottomRect = new Rect(rect.x + 1f, rect.yMax - 16f, rect.width - 2f, 16f);
            DrawButtons(bottomRect);

            rect.yMax -= 16f;
            return drawer.Draw(rect);
        }

        public static bool Contains(string guidOrInstID)
        {
            return guidSet.Contains(guidOrInstID) || instSet.Contains(guidOrInstID);
        }

        public static bool Contains(UnityObject sceneObject)
        {
            var id = FR2_Unity.GetInstanceId(sceneObject).ToString();
            return instSet.Contains(id);
        }
        public static bool Contains(FR2_Ref rf)
        {
            if (rf.isSceneRef)
                return rf.component != null && Contains(rf.component);
            if (guidSet == null) return false;
            return guidSet.Contains(rf.asset.guid);
        }
        public static void Add(UnityObject sceneObject)
        {
            if (sceneObject == null) return;
            var id = FR2_Unity.GetInstanceId(sceneObject).ToString();
            instSet.Add(id);
            dirty = true;
        }

        public static void Add(string guid)
        {
            if (guidSet.Contains(guid)) return;
            string assetPath = FR2_Cache.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                FR2_LOG.LogWarning("Invalid GUID: " + guid);
                return;
            }

            guidSet.Add(guid);
            dirty = true;
        }

        public static void Remove(UnityObject sceneObject)
        {
            if (sceneObject == null) return;
            var id = FR2_Unity.GetInstanceId(sceneObject).ToString();
            instSet.Remove(id);
            dirty = true;
        }

        public static void Remove(string guidOrInstID)
        {
            guidSet.Remove(guidOrInstID);
            instSet.Remove(guidOrInstID);
            dirty = true;
        }

        public static void Clear()
        {
            guidSet.Clear();
            instSet.Clear();
            dirty = true;
            
            // Invalidate all drawer caches so group toggles update correctly
            InvalidateAllDrawerCaches();
        }

        public static void Add(FR2_Ref rf)
        {
            if (rf.isSceneRef)
            {
                if (rf.component != null) Add(rf.component);
                InvalidateAllDrawerCaches();
                return;
            }
            Add(rf.asset.guid);
            InvalidateAllDrawerCaches();
        }

        public static void Remove(FR2_Ref rf)
        {
            if (rf.isSceneRef)
            {
                if (rf.component != null) Remove(rf.component);
                InvalidateAllDrawerCaches();
                return;
            }
            Remove(rf.asset.guid);
            InvalidateAllDrawerCaches();
        }

        public static void Commit()
        {
            var list = new HashSet<UnityObject>();

            foreach (string guid in guidSet)
            {
                string path = FR2_Cache.GUIDToAssetPath(guid);
                UnityObject obj = AssetDatabase.LoadAssetAtPath(path, typeof(UnityObject));
                if (obj != null) list.Add(obj);
            }

            foreach (string instID in instSet)
            {
                int id = int.Parse(instID);
                UnityObject obj = FR2_Unity.InstanceIdToObject(id);
                if (obj == null) continue;
                list.Add(obj is Component c ? c.gameObject : obj);
            }

            Selection.objects = list.ToArray();
        }

        public void SetDirty()
        {
            drawer.SetDirty();
        }

        private void DrawButtons(Rect rect)
        {
            var (selectRect, temp) = rect.ExtractLeft(64f, 4f);
            var (clearRect, exportRect) = temp.ExtractLeft(64f, 4f);
            using (GUIEnable((refs != null) && (refs.Count > 0)))
            {
                if (GUI.Button(selectRect, FR2_GUIContent.FromString("Select", "Select items in Project or Hierarchy panel"))) Commit();
                if (GUI.Button(clearRect, FR2_GUIContent.FromString("Clear", "Clear all bookmarks"))) Clear();
                if (GUI.Button(exportRect, FR2_GUIContent.FromString("CSV", "Export bookmarked items as CSV"))) FR2_Export.ExportCSV(FR2_Ref.FromDict(refs));    
            }
            
			// if (GUI.Button(right, FR2_Icon.Refresh.image)) RefreshView();
        }

        public void RefreshView()
        {
			refs = new Dictionary<string, FR2_Ref>();

			//foreach (KeyValuePair<string, List<string>> item in FR2_Setting.IgnoreFiltered)
            foreach (string guid in guidSet)
            {
				FR2_Asset asset = FR2_Cache.GetAsset(guid, false);
				if (asset == null)
				{
					FR2_LOG.LogWarning("Invalid asset guid: " + guid);
					continue;
				}
				refs.Add(guid, new FR2_Ref(0, 1, asset, null));
            }

			foreach (string instID in instSet)
            {
				int id;
				if (!int.TryParse(instID, out id)) continue;
				var obj = FR2_Unity.InstanceIdToObject(id);
				if (obj == null) continue;
				refs.Add(instID, new FR2_SceneRef(0, obj));
            }

            drawer.SetRefs(refs);
            dirty = false;
        }

        internal void RefreshSort()
        {
            drawer.RefreshSort();
            dirty = true;
        }

        
        private static void InvalidateAllDrawerCaches()
        {
            FR2_Event.DispatchGlobal<BookmarkChangedEvent>();
        }
    }
}
