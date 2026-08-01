using System;
using System.Collections.Generic;

namespace vietlabs.fr2
{
    internal interface IEventSource { }

    internal static class FR2_Event
    {
        private static readonly Dictionary<object, Dictionary<Type, Delegate>> _map
            = new Dictionary<object, Dictionary<Type, Delegate>>();

        private static readonly Dictionary<Type, Delegate> _global
            = new Dictionary<Type, Delegate>();

        private static readonly HashSet<Type> _dispatching = new HashSet<Type>();

        internal static void AddListener<T>(this IEventSource source, Action<T> handler)
        {
            if (source == null || handler == null) return;
            var key = typeof(T);

            if (!_map.TryGetValue(source, out var events))
            {
                events = new Dictionary<Type, Delegate>();
                _map[source] = events;
            }

            if (events.TryGetValue(key, out var existing))
            {
                var typed = (Action<T>)existing;
                typed -= handler;
                events[key] = typed + handler;
            }
            else
            {
                events[key] = handler;
            }
        }

        internal static void RemoveListener<T>(this IEventSource source, Action<T> handler)
        {
            if (source == null || handler == null) return;
            if (!_map.TryGetValue(source, out var events)) return;

            var key = typeof(T);
            if (!events.TryGetValue(key, out var existing)) return;

            var typed = (Action<T>)existing - handler;
            if (typed == null) events.Remove(key);
            else events[key] = typed;
        }

        internal static void Dispatch<T>(this IEventSource source, T evt)
        {
            if (source == null) return;
            if (!_map.TryGetValue(source, out var events)) return;

            var key = typeof(T);
            if (!events.TryGetValue(key, out var existing)) return;
            if (!_dispatching.Add(key)) return;

            ((Action<T>)existing).Invoke(evt);
            _dispatching.Remove(key);
        }

        internal static void Dispatch<T>(this IEventSource source) where T : struct
        {
            source.Dispatch(default(T));
        }

        internal static void ClearAllListeners(this IEventSource source)
        {
            if (source == null) return;
            _map.Remove(source);
        }

        internal static void AddGlobalListener<T>(Action<T> handler)
        {
            if (handler == null) return;
            var key = typeof(T);

            if (_global.TryGetValue(key, out var existing))
            {
                var typed = (Action<T>)existing;
                typed -= handler;
                _global[key] = typed + handler;
            }
            else
            {
                _global[key] = handler;
            }
        }

        internal static void RemoveGlobalListener<T>(Action<T> handler)
        {
            if (handler == null) return;
            var key = typeof(T);
            if (!_global.TryGetValue(key, out var existing)) return;

            var typed = (Action<T>)existing - handler;
            if (typed == null) _global.Remove(key);
            else _global[key] = typed;
        }

        internal static void DispatchGlobal<T>(T evt)
        {
            var key = typeof(T);
            if (!_global.TryGetValue(key, out var existing)) return;
            if (!_dispatching.Add(key)) return;

            ((Action<T>)existing).Invoke(evt);
            _dispatching.Remove(key);
        }

        internal static void DispatchGlobal<T>() where T : struct
        {
            DispatchGlobal(default(T));
        }

        internal static void ClearAll()
        {
            _map.Clear();
            _global.Clear();
        }
    }
}
