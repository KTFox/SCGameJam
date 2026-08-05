using UnityEngine;

namespace SCJam.AudioSystem
{
    [CreateAssetMenu(fileName = "SoundSO", menuName = "SCJam/SoundSO")]
    public class SoundSO : ScriptableObject
    {
        [SerializeField] private AudioType audioType;
        [SerializeField] private AudioClip clip;
        [SerializeField] private bool loop;
        [SerializeField][Range(0.1f, 2f)] private float volume = 1f;
        [SerializeField][Range(0.1f, 3f)] private float minPitch = 1f;
        [SerializeField][Range(0.1f, 3f)] private float maxPitch = 1f;
        [SerializeField][Range(0f, 10f)] private float playCooldown = 0.1f;

        public AudioType AudioType => audioType;
        public AudioClip Clip => clip;
        public bool Loop => loop;
        public float Volume => volume;
        public float MinPitch => minPitch;
        public float MaxPitch => maxPitch;
        public float PlayCooldown => playCooldown;
    }
}
