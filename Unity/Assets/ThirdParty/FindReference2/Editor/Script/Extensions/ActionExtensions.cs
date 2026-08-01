using System;

namespace vietlabs.fr2
{
    internal static class ActionExtensions
    {
        /// <summary>
        /// Safely invokes an Action while setting a guard flag to prevent re-entrant calls.
        /// The flag is automatically cleared even if the action throws an exception.
        /// </summary>
        /// <param name="action">The action to invoke</param>
        /// <param name="invokingFlag">Reference to a flag that will be set during invocation</param>
        internal static void SafeInvoke(this Action action, ref bool invokingFlag)
        {
            if (action == null) return;
            
            invokingFlag = true;
            try
            {
                action.Invoke();
            }
            finally
            {
                invokingFlag = false;
            }
        }
        
        /// <summary>
        /// Safely invokes an Action with parameter, swallowing exceptions and logging warnings.
        /// </summary>
        internal static void SafeInvoke<T>(this Action<T> action, T parameter)
        {
            if (action == null) return;
            
            try
            {
                action.Invoke(parameter);
            }
            catch (Exception ex)
            {
                FR2_LOG.LogWarning($"SafeInvoke error: {ex.Message}");
            }
        }
    }
}
