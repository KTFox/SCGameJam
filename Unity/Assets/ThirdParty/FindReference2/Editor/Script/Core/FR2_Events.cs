namespace vietlabs.fr2
{
    internal struct BookmarkChangedEvent { }
    internal struct IgnoreChangedEvent { }
    internal struct SelectionChangedEvent { }
    internal struct CacheReadyEvent { }
    internal struct SceneCacheReadyEvent { }
    
    internal struct SettingChangedEvent
    {
        public string key;
        public SettingChangedEvent(string key) { this.key = key; }
    }
    
    internal static class SettingKeys
    {
        internal const string AlternateRowColor = "alternateRowColor";
        internal const string ShowReferenceCount = "showReferenceCount";
        internal const string BadgeReferenceCount = "badgeReferenceCount";
        internal const string TreeIndent = "treeIndent";
        internal const string RowColor = "rowColor";
        internal const string ManualRefreshSelection = "manualRefreshSelection";
        internal const string ShowHierarchyReferenceCount = "showHierarchyReferenceCount";
        internal const string HierarchyReferenceCountOffset = "hierarchyReferenceCountOffset";
        internal const string AutoRefreshMode = "autoRefreshMode";
        internal const string Disable = "disable";
        internal const string DbValidation = "dbValidation";
        internal const string ParserVerbose = "parserVerbose";
        internal const string TypeFilter = "typeFilter";
        internal const string HidePackagesAndBuiltIn = "hidePackagesAndBuiltIn";
    }
}
