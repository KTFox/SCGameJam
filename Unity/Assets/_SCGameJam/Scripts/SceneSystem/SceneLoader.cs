using System;
using System.Collections;
using SCJam.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SCJam.SceneSystem
{
    public class SceneLoader : MonoSingleton<SceneLoader>
    {
        // ===== Constants ===== //

        private const float ACTIVATION_PROGRESS_THRESHOLD = 0.9f;


        // ===== Serialized Fields ===== //

        [SerializeField] private SceneMapConfig _sceneMap;
        [SerializeField] private float _minLoadingScreenDuration;


        // ===== Private Fields ===== //

        private Coroutine _loadRoutine;
        private SceneId? _activeContentScene;


        // ===== Events ===== //

        public static event Action<float> LoadProgressChanged;
        public static event Action<SceneId> SceneLoaded;


        // ===== Methods ===== //

        protected override void Awake()
        {
            base.Awake();

            if (_sceneMap == null)
                Debug.LogError($"[{nameof(SceneLoader)}] Missing {nameof(SceneMapConfig)} reference.", this);
        }

        public void LoadScene(SceneId sceneId)
        {
            if (_loadRoutine != null)
            {
                Debug.LogWarning($"[{nameof(SceneLoader)}] Ignored request to load '{sceneId}' because a load is already in progress.", this);
                return;
            }

            _loadRoutine = StartCoroutine(Co_LoadScene(sceneId));
        }

        private IEnumerator Co_LoadScene(SceneId sceneId)
        {
            if (!_sceneMap.TryGetSceneName(sceneId, out string sceneName))
            {
                Debug.LogError($"[{nameof(SceneLoader)}] No scene registered for '{sceneId}' in {_sceneMap}.", this);
                _loadRoutine = null;
                yield break;
            }

            _sceneMap.TryGetSceneName(SceneId.Loading, out string loadingSceneName);
            yield return SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);
            float loadingScreenShownAt = Time.unscaledTime;

            if (_activeContentScene.HasValue)
            {
                _sceneMap.TryGetSceneName(_activeContentScene.Value, out string previousSceneName);
                yield return SceneManager.UnloadSceneAsync(previousSceneName);
            }

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            loadOp.allowSceneActivation = false;

            while (loadOp.progress < ACTIVATION_PROGRESS_THRESHOLD)
            {
                LoadProgressChanged?.Invoke(loadOp.progress / ACTIVATION_PROGRESS_THRESHOLD);
                yield return null;
            }

            LoadProgressChanged?.Invoke(1f);
            loadOp.allowSceneActivation = true;
            yield return loadOp;

            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

            float remainingDuration = _minLoadingScreenDuration - (Time.unscaledTime - loadingScreenShownAt);
            if (remainingDuration > 0f)
                yield return new WaitForSecondsRealtime(remainingDuration);

            yield return SceneManager.UnloadSceneAsync(loadingSceneName);

            _activeContentScene = sceneId;
            _loadRoutine = null;
            SceneLoaded?.Invoke(sceneId);
        }
    }
}
