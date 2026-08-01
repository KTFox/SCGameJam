using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace vietlabs.fr2
{
    internal class FR2_NavigationHistory
    {
        private readonly List<UnityObject[]> history = new List<UnityObject[]>();
        private int currentIndex = -1;
        private const int MAX_HISTORY_SIZE = 20;
        private FR2_WindowAll window;
        private bool isNavigating = false;
        
        private static readonly UnityObject[] EmptyArray = new UnityObject[0];
        
        public bool CanGoBack => currentIndex > 0 && GetValidHistoryCount() > 1;
        public bool CanGoForward => currentIndex < history.Count - 1 && GetValidHistoryCount() > 1;
        
        public void SetWindow(FR2_WindowAll windowAll)
        {
            window = windowAll;
        }
        
        public void RecordSelection(UnityObject[] selection)
        {
            if (selection == null || selection.Length == 0) return;
            if (isNavigating) return;
            
            // Only record selections where ALL objects are valid
            if (!AreAllObjectsValid(selection)) return;
            
            // Check if current selection is the same
            if (currentIndex >= 0 && currentIndex < history.Count)
            {
                UnityObject[] current = history[currentIndex];
                if (AreSelectionsEqual(current, selection)) return;
            }
            
            // Remove forward history if we're not at the end
            if (currentIndex < history.Count - 1)
            {
                history.RemoveRange(currentIndex + 1, history.Count - currentIndex - 1);
            }
            
            // Store the selection directly (all objects are valid)
            history.Add(selection);
            currentIndex = history.Count - 1;
            
            if (history.Count > MAX_HISTORY_SIZE)
            {
                history.RemoveAt(0);
                currentIndex--;
            }
        }
        
        public bool GoBack()
        {
            if (!CanGoBack) return false;
            
            CleanInvalidHistoryEntries();
            if (currentIndex <= 0) return false;
            
            currentIndex--;
            UnityObject[] validSelection = GetValidHistoryEntry(history[currentIndex]);
            
            if (validSelection.Length == 0)
            {
                history.RemoveAt(currentIndex);
                if (currentIndex >= history.Count) currentIndex = history.Count - 1;
                return GoBack();
            }
            
            isNavigating = true;
            UpdateFR2SelectionDirectly(validSelection);
            isNavigating = false;
            return true;
        }
        
        public bool GoForward()
        {
            if (!CanGoForward) return false;
            
            CleanInvalidHistoryEntries();
            if (currentIndex >= history.Count - 1) return false;
            
            currentIndex++;
            UnityObject[] validSelection = GetValidHistoryEntry(history[currentIndex]);
            
            if (validSelection.Length == 0)
            {
                history.RemoveAt(currentIndex);
                currentIndex--;
                return GoForward();
            }
            
            isNavigating = true;
            UpdateFR2SelectionDirectly(validSelection);
            isNavigating = false;
            return true;
        }
        
        private void CleanInvalidHistoryEntries()
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                UnityObject[] entry = history[i];
                if (entry == null || entry.Length == 0 || !AreAllObjectsValid(entry))
                {
                    // Remove entire history entry if any object is invalid
                    history.RemoveAt(i);
                    if (currentIndex >= i) currentIndex--;
                }
            }
            
            if (currentIndex < 0 && history.Count > 0) currentIndex = 0;
            if (currentIndex >= history.Count) currentIndex = history.Count - 1;
        }
        
        private UnityObject[] GetValidHistoryEntry(UnityObject[] entry)
        {
            if (entry == null || entry.Length == 0) return EmptyArray;
            
            // If any object is invalid, treat the entire entry as invalid
            return AreAllObjectsValid(entry) ? entry : EmptyArray;
        }
        
        private static bool AreAllObjectsValid(UnityObject[] objects)
        {
            if (objects == null || objects.Length == 0) return false;
            
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] == null) return false;
            }
            return true;
        }
        
        
        private int GetValidHistoryCount()
        {
            int count = 0;
            for (int i = 0; i < history.Count; i++)
            {
                UnityObject[] entry = history[i];
                if (entry != null && entry.Length > 0 && AreAllObjectsValid(entry))
                {
                    count++;
                }
            }
            return count;
        }
        
        private void UpdateFR2SelectionDirectly(UnityObject[] selection)
        {
            if (window == null) return;
            
            // Only use selections where ALL objects are valid
            UnityObject[] finalSelection = (selection != null && AreAllObjectsValid(selection)) ? selection : EmptyArray;
            
            window.SetFR2Selection(finalSelection);
            Selection.objects = finalSelection;
        }
        
        private static bool AreSelectionsEqual(UnityObject[] a, UnityObject[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            
            return true;
        }
    }
}