using System;
using UnityEditor;
namespace vietlabs.fr2
{
    internal static partial class FR2_Lightmap
    {
        [Serializable]
        private struct SceneObjectIdentifier : IEquatable<SceneObjectIdentifier>
        {
            public long targetObject;

            public long targetPrefab;

            public SceneObjectIdentifier(GlobalObjectId id)
            {
                if (id.identifierType != 2) throw new ArgumentException("GlobalObjectId must refer to a scene object.", nameof(id));

                targetObject = unchecked((long)id.targetObjectId);
                targetPrefab = unchecked((long)id.targetPrefabId);
            }

            public bool Equals(SceneObjectIdentifier other)
            {
                return (targetObject == other.targetObject) && (targetPrefab == other.targetPrefab);
            }
        }
    }
}
