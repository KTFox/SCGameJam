using System.Collections.Generic;

namespace vietlabs.fr2
{
    internal class GOCacheEntry
    {
        public ulong goID;
        public Dictionary<ulong, CompRefs> compsWithRefs;
        public string sourcePrefabGUID;
        public string variantPrefabGUID;

        public GOCacheEntry(ulong goID)
        {
            this.goID = goID;
            compsWithRefs = new Dictionary<ulong, CompRefs>();
        }
        
        public bool MatchesPrefabGUID(string guid)
        {
            return sourcePrefabGUID == guid 
                || (!string.IsNullOrEmpty(variantPrefabGUID) && variantPrefabGUID == guid);
        }
    }

    internal class CompRefs
    {
        public List<SceneObjRef> sceneRefs = new List<SceneObjRef>();
        public List<AssetRef> assets = new List<AssetRef>();

        public bool HasReferences => sceneRefs.Count > 0 || assets.Count > 0;
    }

    internal struct SceneObjRef
    {
        public ulong targetId;
        public string propertyPath;

        public SceneObjRef(ulong targetId, string propertyPath = null)
        {
            this.targetId = targetId;
            this.propertyPath = propertyPath;
        }
    }

    internal class AssetRef
    {
        public string guid;
        public ulong localFileId;
        public string propertyPath;

        public AssetRef(string guid, ulong localFileId, string propertyPath = null)
        {
            this.guid = guid;
            this.localFileId = localFileId;
            this.propertyPath = propertyPath;
        }
    }
}
