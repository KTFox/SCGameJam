using System;

namespace vietlabs.fr2
{
    internal static class FR2_DrawerFactory
    {
        internal static FR2_RefDrawer CreateAssetDrawer(
            FR2_WindowAll w,
            string emptyMessage,
            Func<string> contextualMessage = null,
            Func<bool> shouldShowDetailButton = null,
            float padLeft = FR2_WindowAll.ASSET_SELECTION_PAD_LEFT)
        {
            var s = w.settings;
            var vs = s.assetView;
            
            return new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = w,
                getSortMode = () => vs.sortMode,
                getGroupMode = () => vs.groupMode,
                showFullPath = vs.showFullPath,
                showFileSize = vs.showFileSize,
                showExtension = vs.showExtension,
                showUsageType = vs.showUsageType,
                showAssetBundleName = FR2_Setting.s.displayAssetBundleName,
                showAtlasName = FR2_Setting.s.displayAtlasName,
                showToggle = true,
                selectionPadLeft = padLeft,
                shouldShowExtension = () => vs.showExtension,
                shouldShowDetailButton = shouldShowDetailButton ?? (() => true),
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = emptyMessage,
                GetContextualEmptyMessage = contextualMessage
            };
        }

        internal static FR2_RefDrawer CreateSceneDrawer(
            FR2_WindowAll w,
            FR2_WindowAll.DrawerViewSettings vs,
            string emptyMessage,
            Func<string> contextualMessage = null,
            Func<bool> shouldShowDetailButton = null)
        {
            return new FR2_RefDrawer(new FR2_RefDrawer.SceneDrawingConfig
            {
                window = w,
                getSortMode = () => vs.sortMode,
                getGroupMode = () => vs.groupMode,
                showFullPath = vs.showFullPath,
                showDetails = true,
                showToggle = true,
                selectionPadLeft = FR2_WindowAll.SCENE_SELECTION_PAD_LEFT,
                shouldShowExtension = () => false,
                shouldShowDetailButton = shouldShowDetailButton ?? (() => true),
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = emptyMessage,
                GetContextualEmptyMessage = contextualMessage
            };
        }

        internal static FR2_RefDrawer CreateToolDrawer(
            FR2_WindowAll w,
            string emptyMessage,
            Func<bool> shouldShowDetailButton = null)
        {
            var s = w.settings;
            var vs = s.toolView;
            
            return new FR2_RefDrawer(new FR2_RefDrawer.AssetDrawingConfig
            {
                window = w,
                getSortMode = () => vs.sortMode,
                getGroupMode = () => vs.groupMode,
                showFullPath = vs.showFullPath,
                showFileSize = vs.showFileSize,
                showExtension = vs.showExtension,
                showUsageType = vs.showUsageType,
                showAssetBundleName = FR2_Setting.s.displayAssetBundleName,
                showAtlasName = FR2_Setting.s.displayAtlasName,
                showToggle = true,
                selectionPadLeft = FR2_WindowAll.TOOL_SELECTION_PAD_LEFT,
                shouldShowExtension = () => vs.showExtension,
                shouldShowDetailButton = shouldShowDetailButton ?? (() => true),
                onCacheInvalidated = () => { }
            })
            {
                messageEmpty = emptyMessage
            };
        }
    }
}
