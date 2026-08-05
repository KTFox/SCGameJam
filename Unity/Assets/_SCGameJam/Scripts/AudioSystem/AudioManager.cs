using System.Collections;
using System.Collections.Generic;
using SCJam.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace SCJam.AudioSystem
{
    public class AudioManager : MonoSingleton<AudioManager>
    {
        // ===== FIELDS ===== //

        [Header("Volume Settings")]
        [SerializeField][Range(0f, 2f)] private float masterVolume;

        [Header("Audio Mixer Groups")]
        [SerializeField] private AudioMixerGroup sfxMixerGroup;
        [SerializeField] private AudioMixerGroup musicMixerGroup;

        [Header("Pool Settings")]
        [SerializeField] private int _poolSize;

        [Header("Listener Settings")]
        [SerializeField] private Transform _listener;
        [SerializeField] private float _maxSoundDistance;
        [SerializeField][Range(0f, 1f)] private float _distanceVolumeThreshold;

        private readonly Queue<AudioSource> _audioSources = new Queue<AudioSource>();
        private readonly Dictionary<SoundSO, float> _lastPlayTimeBySound = new();

        private float _maxSoundDistanceSqr;


        // ===== MONOBEHAVIOUR METHODS ===== //

        protected override void Awake()
        {
            base.Awake();
            InitializePool();
        }

        private void Start()
        {
            _maxSoundDistanceSqr = _maxSoundDistance * _maxSoundDistance;
        }


        // ===== METHODS ===== //

        public void PlaySound(SoundSO soundData)
        {
            if (soundData == null)
                return;

            PlaySound(soundData, soundData.Volume, soundData.MaxPitch);
        }

        public void PlaySound(SoundSO soundData, float normalizedPitchProgress)
        {
            if (soundData == null)
                return;

            float clampedProgress = Mathf.Clamp01(normalizedPitchProgress);
            float pitch = Mathf.Lerp(soundData.MinPitch, soundData.MaxPitch, clampedProgress);

            PlaySound(soundData, soundData.Volume, pitch);
        }

        public void PlaySound(SoundSO soundData, Vector3 soundPosition = default)
        {
            if (soundData == null)
                return;

            float distanceVolume = GetVolumeMultiplierByDistance(soundPosition);
            if (distanceVolume <= 0f)
                return;

            PlaySound(soundData, soundData.Volume * distanceVolume, soundData.MaxPitch);
        }

        public void PlaySound(SoundSO soundData, float volume, float pitch)
        {
            if (soundData == null || !CanPlayByCooldown(soundData))
                return;

            _lastPlayTimeBySound[soundData] = Time.time;

            AudioSource audioSource = GetFromPool();
            audioSource.clip = soundData.Clip;
            audioSource.pitch = pitch;
            audioSource.volume = volume * masterVolume;
            audioSource.loop = soundData.Loop;
            audioSource.outputAudioMixerGroup = soundData.AudioType == AudioType.Music ? musicMixerGroup : sfxMixerGroup;
            audioSource.Play();

            if (!soundData.Loop)
            {
                StartCoroutine(Co_ReturnToPool(audioSource, soundData.Clip.length));
            }
        }

        private float GetVolumeMultiplierByDistance(Vector3 soundPosition)
        {
            if (_listener == null)
                return 0f;

            float distanceSqr = (soundPosition - _listener.position).sqrMagnitude;
            if (distanceSqr >= _maxSoundDistanceSqr)
                return 0f;

            float distance = Mathf.Sqrt(distanceSqr);
            float thresholdDistance = _maxSoundDistance * _distanceVolumeThreshold;

            if (distance <= thresholdDistance)
                return 1f;

            float falloffRange = _maxSoundDistance - thresholdDistance;
            if (falloffRange <= 0f)
                return 1f;

            return 1f - (distance - thresholdDistance) / falloffRange;
        }

        private bool CanPlayByCooldown(SoundSO soundData)
        {
            if (soundData.PlayCooldown <= 0f)
                return true;

            if (!_lastPlayTimeBySound.TryGetValue(soundData, out float lastPlayTime))
                return true;

            return Time.time >= lastPlayTime + soundData.PlayCooldown;
        }


        // ===== AUDIO SOURCE POOLING ===== //

        private void InitializePool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                AudioSource audioSource = CreateAudioSource();
                ReturnToPool(audioSource);
            }
        }

        private AudioSource CreateAudioSource()
        {
            GameObject soundObject = new GameObject("Audio Source");
            AudioSource audioSource = soundObject.AddComponent<AudioSource>();
            soundObject.transform.SetParent(transform);

            return audioSource;
        }

        private AudioSource GetFromPool()
        {
            AudioSource audioSource = null;

            if (_audioSources.Count > 0)
            {
                audioSource = _audioSources.Dequeue();
            }
            else
            {
                audioSource = CreateAudioSource();
            }

            audioSource.gameObject.SetActive(true);
            return audioSource;
        }

        private IEnumerator Co_ReturnToPool(AudioSource audioSource, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(audioSource);
        }

        private void ReturnToPool(AudioSource audioSource)
        {
            audioSource.Stop();
            audioSource.clip = null;
            audioSource.loop = false;
            audioSource.volume = 1f;
            audioSource.pitch = 1f;
            audioSource.outputAudioMixerGroup = null;
            audioSource.gameObject.SetActive(false);
            _audioSources.Enqueue(audioSource);
        }
    }
}
