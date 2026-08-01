using System;

namespace vietlabs.fr2
{
    internal class FR2_DrawerGroup
    {
        private readonly FR2_RefDrawer[] _drawers;

        internal FR2_DrawerGroup(params FR2_RefDrawer[] drawers)
        {
            _drawers = drawers;
        }

        internal void RefreshSort()
        {
            foreach (var d in _drawers) d?.RefreshSort();
        }

        internal void SetDirty()
        {
            foreach (var d in _drawers) d?.SetDirty();
        }

        internal void DirtyAndSort()
        {
            SetDirty();
            RefreshSort();
        }

        internal void ApplyToConfigs(Action<FR2_RefDrawer.RefDrawerConfig> action)
        {
            foreach (var d in _drawers)
            {
                if (d?.Config != null) action(d.Config);
            }
        }

        internal void ApplyToAssetConfigs(Action<FR2_RefDrawer.AssetDrawingConfig> action)
        {
            foreach (var d in _drawers)
            {
                if (d?.AssetConfig != null) action(d.AssetConfig);
            }
        }

        internal void InvalidateGroupCache()
        {
            foreach (var d in _drawers) d?.InvalidateGroupCache();
        }
        
        internal void NotifyDisplayChanged()
        {
            foreach (var d in _drawers) d?.Config?.NotifyDisplayChanged();
        }
        
        internal void NotifySortChanged()
        {
            foreach (var d in _drawers) d?.Config?.NotifySortChanged();
        }
        
        internal void NotifyGroupModeChanged()
        {
            foreach (var d in _drawers) d?.Config?.NotifyGroupModeChanged();
        }
    }
}
