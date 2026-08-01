using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal static class FR2WindowExtensions
    {
        internal static void CacheAllDrawers(this FR2_WindowAll window)
        {
            window._allDrawersCache = new FR2_RefDrawer[]
            {
                window.UsedByDrawer,
                window.UsesDrawer,
                window.SceneToAssetDrawer,
                window.RefUnUse,
                window.RefInScene,
                window.RefSceneInScene,
                window.SceneUsesDrawer,
                window.UsedInBuild.Drawer,
                window.AddressableDrawer?.drawer,
                window.bookmark?.drawer
            };
        }
    }
}
