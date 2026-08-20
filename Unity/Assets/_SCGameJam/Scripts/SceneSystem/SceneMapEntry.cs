using System;
using UnityEngine;

namespace SCJam.SceneSystem
{
    [Serializable]
    public class SceneMapEntry
    {
        // ===== Serialized Fields ===== //

        [SerializeField] private SceneId _id;
        [SerializeField] private string _sceneName;


        // ===== Public Properties ===== //

        public SceneId Id => _id;
        public string SceneName => _sceneName;
    }
}
