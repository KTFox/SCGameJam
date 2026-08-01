using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace vietlabs.fr2
{
    [Serializable]
    internal class FR2_Setting
    {
        private static FR2_Setting d;

        [NonSerialized] private static HashSet<string> _hashIgnore;

        public bool alternateColor = true;
        public int excludeTypes; //32-bit type Mask

        public List<string> listIgnore = new List<string>();
        // public bool pingRow = true;
        public bool referenceCount = true;
        public bool badgeReferenceCount = true;
        // public bool showPackageAsset = true;
        public bool showSubAssetFileId;

        public bool showFileSize;
        public bool displayFileSize = true;
        public bool displayAtlasName;
        public bool displayAssetBundleName;

        public int treeIndent = 10;
        public bool manualRefreshSelection;

        public Color32 rowColor = new Color32(0, 0, 0, 12);

        // public Color32 ScanColor = new Color32(0, 204, 102, 255);
        public Color SelectedColor = new Color(0, 0f, 1f, 0.25f);


        //public bool scanScripts		= false;



        /*
        Doesn't have a settings option - I will include one in next update

        2. Hide the reference number - Should be in the setting above so will be coming next
        3. Cache file path should be configurable - coming next in the setting
        4. Disable / Selectable color in alternative rows - coming next in the setting panel
        5. Applied filters aren't saved - Should be fixed in next update too
        6. Hide Selection part - should be com as an option so you can quickly toggle it on or off
        7. Click whole line to ping - coming next by default and can adjustable in the setting panel

        */

        internal static FR2_Setting s
        {
            get
            {
                if (FR2_Cache.settings != null) return FR2_Cache.settings;
                if (d != null) return d;
                d = FR2_Cache.settings ?? new FR2_Setting();
                return d;
            }
        }


        public static bool ShowFileSize => s.showFileSize;

        public static int TreeIndent
        {
            get => s.treeIndent;
            set
            {
                if (s.treeIndent == value) return;
                s.treeIndent = value;
                FR2_Cache.MarkDirty();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.TreeIndent));
            }
        }

        public static bool ShowReferenceCount
        {
            get => s.referenceCount;
            set
            {
                if (s.referenceCount == value) return;
                s.referenceCount = value;
                EditorApplication.RepaintProjectWindow();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.ShowReferenceCount));
            }
        }

        public static bool BadgeReferenceCount => true;

        public static bool AlternateRowColor
        {
            get => s.alternateColor;
            set
            {
                if (s.alternateColor == value) return;
                s.alternateColor = value;
                FR2_Cache.MarkDirty();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.AlternateRowColor));
            }
        }

        public static Color32 RowColor
        {
            get => s.rowColor;
            set
            {
                if (s.rowColor.Equals(value)) return;
                s.rowColor = value;
                FR2_Cache.MarkDirty();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.RowColor));
            }
        }

        public static bool ManualRefreshSelection
        {
            get => s.manualRefreshSelection;
            set
            {
                if (s.manualRefreshSelection == value) return;
                s.manualRefreshSelection = value;
                FR2_Cache.MarkDirty();
                FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.ManualRefreshSelection));
            }
        }

        public static HashSet<string> IgnoreAsset
        {
            get
            {
                if (_hashIgnore != null) return _hashIgnore;
                _hashIgnore = new HashSet<string>();
                if (s?.listIgnore == null) return _hashIgnore;

                for (var i = 0; i < s.listIgnore.Count; i++)
                {
                    _hashIgnore.Add(s.listIgnore[i]);
                }

                return _hashIgnore;
            }
        }

        public static bool IsAssetPathIgnored(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            var h = IgnoreAsset;
            if (h.Count == 0) return false;
            
            foreach (string item in h)
            {
                if (assetPath.StartsWith(item, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        //		public static Dictionary<string, List<string>> IgnoreFiltered
        //		{
        //			get
        //			{
        //				if (_IgnoreFiltered == null)
        //				{
        //					initIgnoreFiltered();
        //				}
        //
        //				return _IgnoreFiltered;
        //			}
        //		}

        //static public bool ScanScripts
        //{
        //	get  { return s.scanScripts; }
        //	set  {
        //		if (s.scanScripts == value) return;
        //		s.scanScripts = value; setDirty();
        //	}
        //}

        public static bool HasTypeExcluded => s.excludeTypes != 0;


        //		private static void initIgnoreFiltered()
        //		{
        //			FR2_Asset.ignoreTS = Time.realtimeSinceStartup;
        //
        //			_IgnoreFiltered = new Dictionary<string, List<string>>();
        //			var lst = new List<string>(s.listIgnore);
        //			lst = lst.OrderBy(x => x.Length).ToList();
        //			int count = lst.Count;
        //			for (var i = 0; i < count; i++)
        //			{
        //				string str = lst[i];
        //				_IgnoreFiltered.Add(str, new List<string> {str});
        //				for (int j = count - 1; j > i; j--)
        //				{
        //					if (lst[j].StartsWith(str))
        //					{
        //						_IgnoreFiltered[str].Add(lst[j]);
        //						lst.RemoveAt(j);
        //						count--;
        //					}
        //				}
        //			}
        //		}

        public static void AddIgnore(string path)
        {
            if (string.IsNullOrEmpty(path) || IgnoreAsset.Contains(path) || path == "Assets") return;

            s.listIgnore.Add(path);
            _hashIgnore.Add(path);
            FR2_AssetGroupDrawer.SetDirtyIgnore();
            FR2_CacheHelper.InitIgnore();

            //initIgnoreFiltered();

            FR2_Asset.ignoreTS = Time.realtimeSinceStartup;
            FR2_Event.DispatchGlobal<IgnoreChangedEvent>();
        }


        public static void RemoveIgnore(string path)
        {
            if (!IgnoreAsset.Contains(path)) return;

            _hashIgnore.Remove(path);
            s.listIgnore.Remove(path);
            FR2_AssetGroupDrawer.SetDirtyIgnore();
            FR2_CacheHelper.InitIgnore();

            FR2_Asset.ignoreTS = Time.realtimeSinceStartup;
            FR2_Event.DispatchGlobal<IgnoreChangedEvent>();
        }

        public static bool IsTypeExcluded(int type)
        {
            return ((s.excludeTypes >> type) & 1) != 0;
        }

        public static void ToggleTypeExclude(int type)
        {
            bool v = ((s.excludeTypes >> type) & 1) != 0;
            if (v)
            {
                s.excludeTypes &= ~(1 << type);
            } else
            {
                s.excludeTypes |= 1 << type;
            }

            FR2_Cache.MarkDirty();
            FR2_Event.DispatchGlobal(new SettingChangedEvent(SettingKeys.TypeFilter));
        }

        public static int GetExcludeType()
        {
            return s.excludeTypes;
        }

        public static bool IsIncludeAllType()
        {
            // Debug.Log ((AssetType.FILTERS.Length & s.excludeTypes) + "  " + Mathf.Pow(2, AssetType.FILTERS.Length) ); 
            return s.excludeTypes == 0 || Mathf.Abs(s.excludeTypes) == Mathf.Pow(2, FR2_AssetGroupDrawer.FILTERS.Length);
        }

        public static void ExcludeAllType()
        {
            s.excludeTypes = -1;
        }

        public static void IncludeAllType()
        {
            s.excludeTypes = 0;
        }

        public void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            {
                s.alternateColor = EditorGUILayout.ToggleLeft("Alternate Row Color", s.alternateColor);
                s.referenceCount = EditorGUILayout.ToggleLeft("Usage Count in Project Panel", s.referenceCount);
                // s.pingRow = EditorGUILayout.Toggle("Ping Row", s.pingRow);
                // s.showPackageAsset = EditorGUILayout.Toggle("Show Package Assets", s.showPackageAsset);
                // s.showSubAssetFileId = EditorGUILayout.Toggle("Show Sub Asset File ID", s.showSubAssetFileId);
                // s.showFileSize = EditorGUILayout.Toggle("Show File Size", s.showFileSize);
            }
            if (EditorGUI.EndChangeCheck())
            {
                FR2_Cache.MarkDirty();
            }
        }

        
    }
}
