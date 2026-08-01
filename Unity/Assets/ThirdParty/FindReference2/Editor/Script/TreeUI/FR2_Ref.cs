using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal partial class FR2_Ref
    {
        public FR2_Asset addBy;
        public System.Action<FR2_Ref> OnCtrlClick;
        public System.Action<FR2_Ref> OnAltClick;
        public System.Action<FR2_Ref> OnShiftClick;
        public FR2_Asset asset;
        public Object component;
        public int depth;
        public string group;
        public int index;
        public bool isSceneRef;
        public int matchingScore;
        public int type;
        public List<SceneRefInfo> sceneReferenceInfo;
        internal HashSet<SceneRefInfo> sceneReferenceInfoSet;

        public FR2_Ref() { }

        public FR2_Ref(int index, int depth, FR2_Asset asset, FR2_Asset by)
        {
            this.index = index;
            this.depth = depth;
            this.asset = asset;
            if (asset != null) type = FR2_AssetGroupDrawer.GetIndex(asset.extension);
            addBy = by;
        }

        public FR2_Ref(int index, int depth, FR2_Asset asset, FR2_Asset by, string group) : this(index, depth, asset, by)
        {
            this.group = group;
        }

        private static int CSVSorter(FR2_Ref item1, FR2_Ref item2)
        {
            int r = item1.depth.CompareTo(item2.depth);
            if (r != 0) return r;
            int t = item1.type.CompareTo(item2.type);
            if (t != 0) return t;
            return item1.index.CompareTo(item2.index);
        }

        public static FR2_Ref[] FromDict(Dictionary<string, FR2_Ref> dict)
        {
            if (dict == null || dict.Count == 0) return null;
            var result = new List<FR2_Ref>();
            foreach (KeyValuePair<string, FR2_Ref> kvp in dict)
            {
                if (kvp.Value == null) continue;
                if (kvp.Value.asset == null && !kvp.Value.isSceneRef) continue;
                result.Add(kvp.Value);
            }
            result.Sort(CSVSorter);
            return result.ToArray();
        }

        public static FR2_Ref[] FromList(List<FR2_Ref> list)
        {
            if (list == null || list.Count == 0) return null;
            list.Sort(CSVSorter);
            var result = new List<FR2_Ref>();
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].asset == null) continue;
                result.Add(list[i]);
            }
            return result.ToArray();
        }

        public override string ToString()
        {
            if (isSceneRef) return ((FR2_SceneRef)this).scenePath;
            return asset.assetPath;
        }

        public string GetSceneObjId()
        {
            if (component == null) return string.Empty;
            return FR2_Cache.GetInstanceIdString(FR2_Unity.GetInstanceId(component));
        }

        public virtual bool isSelected()
        {
            return FR2_Bookmark.Contains(asset.guid);
        }

        public virtual void DrawToogleSelect(Rect r)
        {
            bool s = isSelected();
            r.width = 16f;
            
            Event evt = Event.current;
            bool isMouseOver = r.Contains(evt.mousePosition);
            bool isMouseDown = evt.type == EventType.MouseDown && evt.button == 0 && isMouseOver;
            
            if (isMouseDown)
            {
                bool ctrl = Application.platform == RuntimePlatform.OSXEditor ? evt.command : evt.control;
                bool alt = evt.alt;
                bool shift = evt.shift;
                
                if (shift) { OnShiftClick?.Invoke(this); evt.Use(); return; }
                if (alt) { OnAltClick?.Invoke(this); evt.Use(); return; }
                if (ctrl) { OnCtrlClick?.Invoke(this); evt.Use(); return; }
            }
            
            if (!GUI2.Toggle(r, ref s)) return;
            if (s) FR2_Bookmark.Add(this);
            else FR2_Bookmark.Remove(this);
        }

        internal List<FR2_Ref> Append(Dictionary<string, FR2_Ref> dict, params string[] guidList)
        {
            var result = new List<FR2_Ref>();
            if (!FR2_Cache.isReady)
            {
                FR2_LOG.LogWarning("Cache not yet ready! Please wait!");
                return result;
            }

            for (var i = 0; i < guidList.Length; i++)
            {
                string guid = guidList[i];
                if (dict.ContainsKey(guid)) continue;

                FR2_Asset child = FR2_Cache.GetAsset(guid);
                if (child == null) continue;

                var r = new FR2_Ref(dict.Count, depth + 1, child, asset);
                dict.Add(guid, r);
                result.Add(r);
            }

            return result;
        }

        internal void AppendUsedBy(Dictionary<string, FR2_Ref> result, List<FR2_Ref> frontier)
        {
            var h = asset.UsedByMap;
            if (h == null || h.Count == 0) return;

            foreach (KeyValuePair<string, FR2_Asset> kvp in h)
            {
                string guid = kvp.Key;
                if (result.ContainsKey(guid)) continue;

                FR2_Asset child = kvp.Value ?? FR2_Cache.GetAsset(guid);
                if (child == null) continue;
                if (child.IsMissing) continue;

                var r = new FR2_Ref(result.Count, depth + 1, child, asset);
                result.Add(guid, r);
                frontier?.Add(r);
            }
        }

        internal void AppendUsage(Dictionary<string, FR2_Ref> result, List<FR2_Ref> frontier)
        {
            Dictionary<string, HashSet<long>> h = asset.UseGUIDs;
            if (h == null || h.Count == 0) return;

            foreach (KeyValuePair<string, HashSet<long>> kvp in h)
            {
                string guid = kvp.Key;
                if (result.ContainsKey(guid)) continue;

                FR2_Asset child = FR2_Cache.GetAsset(guid);
                if (child == null || child.IsMissing) continue;

                var r = new FR2_Ref(result.Count, depth + 1, child, asset);
                result.Add(guid, r);
                frontier?.Add(r);
            }
        }
    }
}
