#define FR2_DEBUG

using System;
using UnityEditor;
using UnityEngine;

namespace vietlabs.fr2
{
    public class FR2_TimeSlice
    {
        private readonly Action onCompleteCallback;
        private readonly Action<int> processingAction;
        private readonly Func<int> targetCountFunc;
        private readonly Action<int, int> onProgressCallback;

        private int _currentIndex;
        private bool _stopped;
        private const float TARGET_FRAME_TIME = 0.016f;
        private int _checkInterval = 1;
        private int _itemsUntilNextCheck = 1;
        
        public string jobName;
        public int currentIndex => _currentIndex;
        public bool isStopped => _stopped;
        
        public FR2_TimeSlice(Func<int> countFunc, Action<int> action, Action onComplete = null, Action<int, int> onProgress = null)
        {
            targetCountFunc = countFunc;
            processingAction = action;
            onCompleteCallback = onComplete;
            onProgressCallback = onProgress;
        }

        public void Start()
        {
            _currentIndex = 0;
            _stopped = false;
            _checkInterval = 1;
            _itemsUntilNextCheck = 1;
            
            EditorApplication.update -= ProcessQueue;
            EditorApplication.update += ProcessQueue;
        }

        public void Stop()
        {
            _stopped = true;
            EditorApplication.update -= ProcessQueue;
        }

        private void ProcessQueue()
        {
            if (_stopped) return;
            
            float startTime = Time.realtimeSinceStartup;
            var targetCount = targetCountFunc.Invoke();
            int itemsProcessedThisFrame = 0;
            
            while (_currentIndex < targetCount)
            {
                if (_stopped) return;
                
                processingAction.SafeInvoke(_currentIndex);
                _currentIndex++;
                itemsProcessedThisFrame++;
                _itemsUntilNextCheck--;

                if (_itemsUntilNextCheck <= 0)
                {
                    float elapsed = Time.realtimeSinceStartup - startTime;
                    
                    float avgTimePerItem = elapsed / itemsProcessedThisFrame;
                    
                    float remainingTime = TARGET_FRAME_TIME - elapsed;
                    if (remainingTime <= 0 || elapsed >= TARGET_FRAME_TIME)
                    {
                        onProgressCallback?.Invoke(_currentIndex, targetCount);
                        return;
                    }
                    
                    _checkInterval = Mathf.Max(1, (int)(remainingTime / avgTimePerItem / 2f));
                    _itemsUntilNextCheck = _checkInterval;
                }
            }

            if (_stopped) return;

            targetCount = targetCountFunc.Invoke();
            if (_currentIndex < targetCount) return;

            EditorApplication.update -= ProcessQueue;
            onCompleteCallback?.Invoke();
        }
    }
}
