namespace vietlabs.fr2
{
    internal partial class FR2_RefDrawer
    {
        internal static class GroupMode
        {
            internal const string Dependency = "Dependency";
            internal const string Depth = "Depth";
            internal const string Type = "Type";
            internal const string Extension = "Extension";
            internal const string Folder = "Folder";
            internal const string Atlas = "Atlas";
            internal const string AssetBundle = "AssetBundle";
            internal const string None = "None";
            
            internal const string Hierarchy = "Hierarchy";
            internal const string SourceComponent = "Component";
            internal const string SourceGameObject = "GameObject";
            internal const string PropertyPath = "Property";
        }
        
        internal static readonly string[] AssetGroupModes = {
            GroupMode.Dependency, GroupMode.Depth, GroupMode.Type,
            GroupMode.Extension, GroupMode.Folder, GroupMode.Atlas, GroupMode.AssetBundle
        };
        
        internal static readonly string[] SceneGroupModes = {
            GroupMode.Hierarchy, GroupMode.SourceComponent,
            GroupMode.SourceGameObject, GroupMode.PropertyPath
        };
        
        internal static readonly string[] SceneAssetUsedByModes = {
            GroupMode.Hierarchy, GroupMode.None
        };
        
        internal static readonly string[] SceneGOUsesModes = {
            GroupMode.SourceComponent, GroupMode.Hierarchy, GroupMode.None
        };
        
        internal static readonly string[] SceneGOUsedByModes = {
            GroupMode.SourceGameObject, GroupMode.Hierarchy, GroupMode.None
        };
        
        internal static readonly string[] ToolGroupModes = {
            GroupMode.Type, GroupMode.Extension, GroupMode.Folder
        };
    }
}
