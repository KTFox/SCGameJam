using System;
using UnityEngine;
namespace vietlabs.fr2
{
    partial class FR2_WindowAll
    {
        [Serializable] internal class DrawerViewSettings
        {
            public bool showFullPath = true;
            public bool showFileSize;
            public bool showExtension;
            public bool showBundleName;
            public bool showAtlasName;
            public bool showAddressable;
            public bool showUsageType = true;
            public string groupMode = FR2_RefDrawer.GroupMode.Dependency;
            public FR2_RefDrawer.Sort sortMode = FR2_RefDrawer.Sort.Path;
            
            [NonSerialized] internal FR2_EnumDrawer groupModeED;
            [NonSerialized] internal FR2_EnumDrawer sortModeED;
        }
        
        [Serializable] internal class PanelSettings
        {
            public bool selection;
            public bool horzLayout;
            public bool scene = true;
            public bool asset = true;
            public bool details;
            public bool bookmark;
            public bool toolMode;

            public bool writeImportLog;
            public bool recursiveUnusedScan = true;

            public int mainTabIndex = 0;
            public int toolTabIndex = 0;
            public int othersTabIndex = 0;

            public float selectionPanelPixel = 200f;
            public float detailsPanelPixel = 150f;  
            public float bookmarkPanelPixel = 150f;
            
            public DrawerViewSettings assetView = new DrawerViewSettings();
            public DrawerViewSettings sceneUsesView = new DrawerViewSettings { groupMode = FR2_RefDrawer.GroupMode.SourceComponent };
            public DrawerViewSettings sceneUsedByView = new DrawerViewSettings { groupMode = FR2_RefDrawer.GroupMode.Hierarchy };
            public DrawerViewSettings toolView = new DrawerViewSettings { groupMode = FR2_RefDrawer.GroupMode.Type };
        }
    }
}
