using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;


namespace vietlabs.fr2
{
    internal partial class FR2_SceneRef : FR2_Ref
    {
        internal static readonly Dictionary<string, Type> CacheType = new Dictionary<string, Type>();
        private static readonly Dictionary<Type, string> typeNameCache = new Dictionary<Type, string>();

        private GUIContent assetNameGC;
        private GUIContent assetTypeGC;

        public Func<bool> drawFullPath;
        public string sceneFullPath = "";
        public string scenePath = "";
        public string targetType;
        
        public List<SceneRefInfo> sourceRefs;
        public List<SceneRefInfo> backwardRefs;
        
        private List<ComponentGroup> _cachedGroups;
        private bool _groupingDirty = true;
        private const int MAX_VISIBLE_GROUPS = 3;

        public FR2_SceneRef(int index, int depth, FR2_Asset asset, FR2_Asset by) : base(index, depth, asset, by)
        {
            isSceneRef = true;
            sourceRefs = new List<SceneRefInfo>();
            backwardRefs = new List<SceneRefInfo>();
            string tooltipPath = asset?.assetPath ?? "Unknown";
            assetNameGC = FR2_GUIContent.FromString(asset?.assetName ?? "Unknown", tooltipPath);
            assetTypeGC = FR2_GUIContent.FromString("");
        }
        
        public FR2_SceneRef(int depth, UnityObject target) : base(0, depth, null, null)
        {
            component = target;
            this.depth = depth;
            isSceneRef = true;
            sourceRefs = new List<SceneRefInfo>();
            backwardRefs = new List<SceneRefInfo>();
            InitializeTargetInfo(target);
        }

        static bool ShouldPrefixSceneName(Scene scene)
        {
            if (!scene.IsValid()) return false;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return false;
            return FR2_Unity.LoadedSceneCount > 1;
        }

        void InitializeTargetInfo(UnityObject target)
        {
            if (target == null)
            {
                targetType = "Missing";
                scenePath = "";
                sceneFullPath = "Missing Object";
                assetNameGC = FR2_GUIContent.FromString("Missing Object", "Object has been destroyed");
                assetTypeGC = FR2_GUIContent.FromString("Missing");
                return;
            }

            if (target is GameObject targetGO)
            {
                targetType = nameof(GameObject);
                scenePath = FR2_Unity.GetGameObjectPath(targetGO, false);
                string pathWithSlash = string.IsNullOrEmpty(scenePath) ? "" : scenePath + "/";
                sceneFullPath = pathWithSlash + targetGO.name;
                assetNameGC = FR2_GUIContent.FromString(targetGO.name, sceneFullPath);
                assetTypeGC = GUIContent.none;
            }
            else if (target is Component targetComp)
            {
                targetType = GetCachedTypeName(component.GetType());
                var go = targetComp.gameObject;
                scenePath = FR2_Unity.GetGameObjectPath(go, false);
                string pathWithSlash = string.IsNullOrEmpty(scenePath) ? "" : scenePath + "/";
                sceneFullPath = pathWithSlash + go.name;
                assetNameGC = FR2_GUIContent.FromString(go.name, sceneFullPath);
                assetTypeGC = FR2_GUIContent.FromString(GetCachedTypeName(component.GetType()));
            }
        }

        string GetPathForDisplay(bool drawFullPath, Scene scene)
        {
            if (!drawFullPath) return scenePath;
            if (!ShouldPrefixSceneName(scene)) return scenePath;
            string sceneName = scene.name;
            if (string.IsNullOrEmpty(sceneName)) return scenePath;
            return string.IsNullOrEmpty(scenePath) ? sceneName : sceneName + "/" + scenePath;
        }

        static string GetCachedTypeName(Type type)
        {
            if (typeNameCache.TryGetValue(type, out string cachedName)) return cachedName;
            cachedName = type.Name;
            typeNameCache.Add(type, cachedName);
            return cachedName;
        }

        public override bool isSelected()
        {
            return component != null && FR2_Bookmark.Contains(component);
        }

        static Texture s_defaultIcon;
        
        static Texture GetDefaultIcon()
        {
            if (s_defaultIcon != null) return s_defaultIcon;
            s_defaultIcon = EditorGUIUtility.ObjectContent(null, typeof(DefaultAsset)).image;
            return s_defaultIcon;
        }

        static bool IsBlankIcon(Texture icon)
        {
            if (icon == null) return true;
            var def = GetDefaultIcon();
            if (def != null && icon == def) return true;
            string n = icon.name;
            if (n.Contains("DefaultAsset") || n.Contains("d_DefaultAsset")) return true;
            return false;
        }

        static Texture GetComponentIcon(Component comp)
        {
            var icon = EditorGUIUtility.ObjectContent(comp, comp.GetType()).image;
            if (!IsBlankIcon(icon)) return icon;
            
            var go = comp.gameObject;
            if (comp is Renderer)
            {
                var ps = go.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    icon = EditorGUIUtility.ObjectContent(ps, typeof(ParticleSystem)).image;
                    if (!IsBlankIcon(icon)) return icon;
                }
            }
            
            return AssetPreview.GetMiniThumbnail(go);
        }
        
        (Texture icon, string tooltip) GetTargetIcon()
        {
            if (component == null) return (null, "");
            
            if (component is GameObject iconGO)
                return (AssetPreview.GetMiniThumbnail(iconGO), "GameObject");
            if (component is Component iconComp)
                return (GetComponentIcon(iconComp), GetCachedTypeName(iconComp.GetType()));
            
            return (AssetPreview.GetMiniThumbnail(component), component.GetType().Name);
        }

        private bool IsInUnitySelection()
        {
            if (component == null) return false;
            var manager = FR2_SelectionManager.Instance;
            if (manager == null) return false;
            int id = component is Component c ? FR2_Unity.GetInstanceId(c.gameObject) : FR2_Unity.GetInstanceId(component);
            return manager.SceneSelection.Contains(id);
        }

        internal void PopulateRowData(RowDrawData row, bool showFullPathValue)
        {
            var (icon, iconTooltip) = GetTargetIcon();

            row.icon = icon;
            row.nameContent = assetNameGC;
            row.secondaryContent = assetTypeGC != GUIContent.none ? assetTypeGC : null;
            row.nameWidth = EditorStyles.label.CalcSize(assetNameGC).x;
            row.secondaryWidth = row.secondaryContent != null ? EditorStyles.miniLabel.CalcSize(assetTypeGC).x : 0f;
            row.secondaryHighPriority = false;

            Scene scene = component is GameObject go ? go.scene : (component as Component)?.gameObject.scene ?? default;
            string pathToDraw = GetPathForDisplay(showFullPathValue, scene);
            row.showPath = showFullPathValue && !string.IsNullOrEmpty(pathToDraw);
            row.pathContent = row.showPath ? FR2_GUIContent.FromString(pathToDraw + "/") : null;
            row.pathWidth = row.showPath ? EditorStyles.miniLabel.CalcSize(row.pathContent).x : 0f;

            PopulateRowInteraction(row);
        }

        internal static void PopulateRowDataAsComponent(RowDrawData row, Component comp)
        {
            if (comp == null) return;
            
            var compType = comp.GetType();
            string typeName = GetCachedTypeName(compType);
            string goName = comp.gameObject.name;
            
            row.icon = GetComponentIcon(comp);
            row.nameContent = FR2_GUIContent.FromString(typeName);
            row.nameWidth = EditorStyles.label.CalcSize(row.nameContent).x;
            row.secondaryContent = null;
            row.secondaryWidth = 0f;
            row.secondaryHighPriority = false;
            row.showPath = true;
            row.pathContent = FR2_GUIContent.FromString(goName + "/");
            row.pathWidth = EditorStyles.miniLabel.CalcSize(row.pathContent).x;

            row.nameColor = null;
            row.isMissing = false;
            row.state = RowState.Normal;
            row.selection = RowSelection.None;
            row.onPing = () => EditorGUIUtility.PingObject(comp);
            row.onOpen = () => EditorGUIUtility.PingObject(comp);
            row.onContextMenu = () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(comp));
#if UNITY_2022_3_OR_NEWER
                menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(comp));
#endif
                menu.ShowAsContext();
            };
            row.ClearHoverActions();
#if UNITY_2022_3_OR_NEWER
            row.AddHoverAction(FR2_GUIContent.FromString("P", "Open Properties"), () => EditorUtility.OpenPropertyEditor(comp));
#endif
            row.ClearColumns();
            row.showCheckbox = false;
            row.selectionPadLeft = 0f;
        }

        internal static void PopulateRowDataAsGameObject(RowDrawData row, GameObject go)
        {
            if (go == null) return;
            
            row.icon = AssetPreview.GetMiniThumbnail(go);
            row.nameContent = FR2_GUIContent.FromString(go.name);
            row.nameWidth = EditorStyles.label.CalcSize(row.nameContent).x;
            row.secondaryContent = null;
            row.secondaryWidth = 0f;
            row.secondaryHighPriority = false;
            row.showPath = false;
            row.pathContent = null;
            row.pathWidth = 0f;

            row.nameColor = null;
            row.isMissing = false;
            row.state = RowState.Normal;
            row.selection = RowSelection.None;
            row.onPing = () => EditorGUIUtility.PingObject(go);
            row.onOpen = () => EditorGUIUtility.PingObject(go);
            row.onContextMenu = () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(go));
#if UNITY_2022_3_OR_NEWER
                menu.AddItem(new GUIContent("Properties..."), false, () => EditorUtility.OpenPropertyEditor(go));
#endif
                menu.ShowAsContext();
            };
            row.ClearHoverActions();
#if UNITY_2022_3_OR_NEWER
            row.AddHoverAction(FR2_GUIContent.FromString("P", "Open Properties"), () => EditorUtility.OpenPropertyEditor(go));
#endif
            row.ClearColumns();
            row.showCheckbox = false;
            row.selectionPadLeft = 0f;
        }        private void PopulateRowInteraction(RowDrawData row)
        {
            row.nameColor = null;
            row.isMissing = false;
            row.state = RowState.Normal;
            row.selection = IsInUnitySelection() ? RowSelection.Blue 
                : FR2_Bookmark.Contains(this) ? RowSelection.Green 
                : RowSelection.None;

            row.onPing = () => { if (component != null) EditorGUIUtility.PingObject(component); };
            row.onOpen = () => { if (component != null) EditorGUIUtility.PingObject(component); };
            row.onContextMenu = () =>
            {
                if (component == null) return;
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Ping"), false, () => EditorGUIUtility.PingObject(component));
#if UNITY_2022_3_OR_NEWER
                menu.AddItem(new GUIContent("Properties..."), false, () =>
                {
                    var target = GetSourceComponentForProperties();
                    if (target != null) EditorUtility.OpenPropertyEditor(target);
                });
#endif
                menu.ShowAsContext();
            };

            row.ClearHoverActions();
#if UNITY_2022_3_OR_NEWER
            row.AddHoverAction(FR2_GUIContent.FromString("P", "Open Properties"), () =>
            {
                var target = GetSourceComponentForProperties();
                if (target != null) EditorUtility.OpenPropertyEditor(target);
            });
#endif

            row.ClearColumns();
        }

        private GUIContent _cachedRefInfoContent;
        private float _cachedRefInfoWidth;
        private GUIContent _cachedRefInfoShortContent;
        private float _cachedRefInfoShortWidth;

        internal void SetColumnReferenceInfo(RowDrawData row, int colIndex, MetadataColumn col)
        {
            EnsureGroupingCached();
            if (_cachedGroups == null || _cachedGroups.Count == 0) return;

            if (_cachedRefInfoContent == null)
            {
                var firstGroup = _cachedGroups[0];
                Texture icon = firstGroup.iconContent?.image;

                var sb = new System.Text.StringBuilder();
                var sbShort = new System.Text.StringBuilder();
                int shown = 0;
                foreach (var group in _cachedGroups)
                {
                    if (shown > 0) { sb.Append(" "); sbShort.Append(" "); }
                    sb.Append(group.displayName);
                    if (group.count > 1)
                    {
                        sb.Append($"({group.count})");
                        sbShort.Append($"({group.count})");
                    }
                    shown++;
                    if (shown >= MAX_VISIBLE_GROUPS) break;
                }
                if (_cachedGroups.Count > MAX_VISIBLE_GROUPS)
                {
                    sb.Append($" +{_cachedGroups.Count - MAX_VISIBLE_GROUPS}");
                    sbShort.Append($" +{_cachedGroups.Count - MAX_VISIBLE_GROUPS}");
                }

                string text = sb.ToString();
                string shortText = sbShort.ToString();

                var sbTooltip = new System.Text.StringBuilder();
                foreach (var group in _cachedGroups)
                {
                    if (!string.IsNullOrEmpty(group.multiLineTooltip))
                    {
                        if (sbTooltip.Length > 0) sbTooltip.Append("\n");
                        sbTooltip.Append(group.multiLineTooltip);
                    }
                }

                _cachedRefInfoContent = new GUIContent(text, icon, sbTooltip.ToString());
                _cachedRefInfoWidth = EditorStyles.miniLabel.CalcSize(FR2_GUIContent.FromString(text)).x + (icon != null ? 18f : 0f);

                if (string.IsNullOrEmpty(shortText))
                {
                    _cachedRefInfoShortContent = null;
                    _cachedRefInfoShortWidth = 0f;
                }
                else
                {
                    _cachedRefInfoShortContent = new GUIContent(shortText, icon, sbTooltip.ToString());
                    _cachedRefInfoShortWidth = EditorStyles.miniLabel.CalcSize(FR2_GUIContent.FromString(shortText)).x + (icon != null ? 18f : 0f);
                }
            }

            row.SetRightColumnValue(colIndex, _cachedRefInfoContent);
            row.rightColumnShortValues[colIndex] = _cachedRefInfoShortContent;
            row.rightColumnShortWidths[colIndex] = _cachedRefInfoShortWidth;
            col.UpdateWidth(_cachedRefInfoWidth);

            row.onRightColumnClick = () =>
            {
                if (_cachedGroups == null || _cachedGroups.Count == 0) return;
                var group = _cachedGroups[0];
                if (group.refs == null || group.refs.Count == 0) return;
                group.cyclingIndex = (group.cyclingIndex + 1) % group.refs.Count;
                var refInfo = group.refs[group.cyclingIndex];
                if (refInfo.sourceComponent != null)
                    FR2_Unity.PingAndHighlight(refInfo.sourceComponent, refInfo.propertyPath);
            };
        }

        UnityObject GetSourceComponentForProperties()
        {
            if (sourceRefs?.Count > 0) return sourceRefs[0].sourceComponent;
            if (backwardRefs?.Count > 0) return backwardRefs[0].sourceComponent;
            return component;
        }

        class ComponentGroup
        {
            public Type componentType;
            public List<SceneRefInfo> refs;
            public int count;
            public float countWidth;
            public float iconWidth;
            public float nameWidth;
            public float totalWidth;
            public string displayName;
            public GUIContent iconContent;
            public GUIContent countContent;
            public GUIContent nameContent;
            public string multiLineTooltip;
            public int cyclingIndex;
        }
        
        public void MarkGroupingDirty()
        {
            _groupingDirty = true;
            _cachedRefInfoContent = null;
        }
        
        void EnsureGroupingCached()
        {
            if (!_groupingDirty && _cachedGroups != null) return;
            
            var refInfos = sourceRefs?.Count > 0 ? sourceRefs : backwardRefs;
            if (refInfos == null || refInfos.Count == 0)
            {
                _cachedGroups = new List<ComponentGroup>();
                _groupingDirty = false;
                return;
            }
            
            _cachedGroups = GroupReferencesByComponentType(refInfos);
            _groupingDirty = false;
        }

        List<ComponentGroup> GroupReferencesByComponentType(List<SceneRefInfo> refInfos)
        {
            var groups = new List<ComponentGroup>();
            var groupDict = new Dictionary<Type, List<SceneRefInfo>>();
            
            foreach (var refInfo in refInfos)
            {
                if (refInfo.sourceComponent == null) continue;
                var componentType = refInfo.sourceComponent.GetType();
                if (!groupDict.TryGetValue(componentType, out var refList))
                {
                    refList = new List<SceneRefInfo>();
                    groupDict[componentType] = refList;
                }
                refList.Add(refInfo);
            }
            
            float iconWidth = 18f;
            
            foreach (var kvp in groupDict)
            {
                var componentType = kvp.Key;
                var compRefs = kvp.Value;
                var count = compRefs.Count;
                var displayName = GetCachedTypeName(componentType);

                float countWidth = 0f;
                GUIContent countContent = null;
                if (count > 1)
                {
                    var countText = $"({count})";
                    countContent = FR2_GUIContent.FromString(countText);
                    countWidth = EditorStyles.miniLabel.CalcSize(countContent).x;
                }

                var nameContent = FR2_GUIContent.FromString(displayName);
                float nameWidth = EditorStyles.label.CalcSize(nameContent).x;
                float totalWidth = countWidth + (countWidth > 0 ? 2f : 0f) + iconWidth;

                var iconContent = EditorGUIUtility.ObjectContent(compRefs[0].sourceComponent, componentType);

                var tooltipLines = new List<string>();
                foreach (var refInfo in compRefs)
                {
                    var pp = refInfo.propertyPath;
                    if (!string.IsNullOrEmpty(pp))
                    {
                        pp = pp.Replace(".Array.data[", "[");
                        pp = pp.Replace("].Array.data[", "][");
                    }
                    tooltipLines.Add($"  {displayName}.{pp}");
                }
                
                groups.Add(new ComponentGroup
                {
                    componentType = componentType,
                    refs = compRefs,
                    count = count,
                    countWidth = countWidth,
                    iconWidth = iconWidth,
                    nameWidth = nameWidth,
                    totalWidth = totalWidth,
                    displayName = displayName,
                    iconContent = iconContent,
                    countContent = countContent,
                    nameContent = nameContent,
                    multiLineTooltip = string.Join("\n", tooltipLines),
                    cyclingIndex = -1
                });
            }
            
            return groups;
        }
    }
}
