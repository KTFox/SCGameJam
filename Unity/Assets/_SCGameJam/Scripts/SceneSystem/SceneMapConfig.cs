using System.Collections.Generic;
using UnityEngine;

namespace SCJam.SceneSystem
{
    [CreateAssetMenu(fileName = "SceneMapConfig", menuName = "SCJam/Scene System/Scene Map Config")]
    public class SceneMapConfig : ScriptableObject
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private List<SceneMapEntry> _entries;


        // ===== Methods ===== //

        public bool TryGetSceneName(SceneId id, out string sceneName)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id != id)
                    continue;

                sceneName = _entries[i].SceneName;
                return true;
            }

            sceneName = null;
            return false;
        }
    }
}
