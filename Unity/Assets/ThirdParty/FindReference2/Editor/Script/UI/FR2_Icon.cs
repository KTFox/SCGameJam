using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace vietlabs.fr2
{
    internal static class FR2_Icon
    {
        public static GUIContent Lock => TryGet("LockIcon-On", "Click to unlock");
        public static GUIContent Unlock => TryGet("LockIcon", "Click to lock");

        public static GUIContent Refresh => TryGet("d_Refresh@2x");

        public static GUIContent Selection => TryGet("d_Selectable Icon");
        public static GUIContent Pointer => TryGet("d_pick", "Select in Unity");
        public static GUIContent Details => TryGet("d_UnityEditor.SceneHierarchyWindow");
        public static GUIContent Favorite => TryGet("d_Favorite");
        public static GUIContent Setting => TryGet("d_SettingsIcon");
        public static GUIContent Ignore => TryGet("ShurikenCheckMarkMixed");
        public static GUIContent Plus => TryGet("ShurikenPlus");

        public static GUIContent Visibility => TryGet("ClothInspector.ViewValue");
        public static GUIContent Panel => TryGet("VerticalSplit");
        public static GUIContent Layout => TryGet("FreeformLayoutGroup Icon");
        public static GUIContent Sort => TryGet("AlphabeticalSorting"); //d_DefaultSorting

        public static GUIContent CustomTool => TryGet("CustomTool", "Advanced Tools");

        public static GUIContent Filter => TryGet("d_ToggleUVOverlay@2x");

        public static GUIContent Group => TryGet("EditCollider");
        public static GUIContent Delete => TryGet("d_TreeEditor.Trash");
        public static GUIContent Split => TryGet("VerticalSplit");
        public static GUIContent Close => TryGet("LookDevClose");
        public static GUIContent Prefab => TryGet("d_Prefab Icon");
        public static GUIContent Asset => TryGet("Folder Icon");
        public static GUIContent Warning => TryGet("console.warnicon");
        public static GUIContent Info => TryGet("console.warnicon");
        public static GUIContent Filesize => TryGet("Download-Available@2x", "Show File Size");
        public static GUIContent FileExtension => TryGet("d_curvekeyframeweighted", "Show File Extension");

        public static GUIContent AssetBundle => TryGet("CloudConnect");
        public static GUIContent Script => TryGet("dll Script Icon");
        public static GUIContent Material => TryGet("d_TreeEditor.Material");
        public static GUIContent Scene => TryGet("UnityLogo");
        public static GUIContent Atlas => TryGet("SpriteAtlas Icon");
        public static GUIContent Folder => TryGet("Project");

        public static GUIContent FullPath => TryGet("UnityEditor.HierarchyWindow", "Show full asset path");
        public static GUIContent Hierarchy => TryGet("UnityEditor.HierarchyWindow");

        public static GUIContent Organize => TryGet("FolderEmpty Icon");
        public static GUIContent Package => TryGet("d_Package Manager@2x", "Show Packages & Built-in assets");

        private static readonly MethodInfo _loadIconMethod = typeof(EditorGUIUtility)
            .GetMethod("LoadIcon", BindingFlags.Static | BindingFlags.NonPublic);
        
        private static readonly Dictionary<string, GUIContent> _cache = new Dictionary<string, GUIContent>();
        private static GUIContent TryGet(string id, string tooltip = null)
        {
            if (_cache.TryGetValue(id, out GUIContent result)) return result ?? GUIContent.none;
            Texture2D tex = null;
            if (_loadIconMethod != null)
                tex = _loadIconMethod.Invoke(null, new object[] { id }) as Texture2D;
            GUIContent icon = tex != null
                ? new GUIContent(tex)
                : new GUIContent(Texture2D.whiteTexture);
            if (!string.IsNullOrEmpty(tooltip)) icon.tooltip = tooltip;
            _cache.Add(id, icon);
            return icon;
        }
    }
}
