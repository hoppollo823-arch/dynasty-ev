// ============================================================================
// DynastyVR Earth — UI Event Bus
// 东方皇朝元宇宙部 | DynastyEV VR Earth Event System
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DynastyVR.Earth.UI
{
    /// <summary>
    /// Lightweight publish/subscribe event bus for decoupled UI ↔ Game communication.
    /// Use static methods from anywhere. No MonoBehaviour required.
    /// </summary>
    public static class UIEventBus
    {
        private static readonly Dictionary<Type, Delegate> _events = new Dictionary<Type, Delegate>();

        /// <summary>
        /// Subscribe to an event type.
        /// </summary>
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_events.TryGetValue(type, out var existing))
                _events[type] = Delegate.Combine(existing, handler);
            else
                _events[type] = handler;
        }

        /// <summary>
        /// Unsubscribe from an event type.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_events.TryGetValue(type, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null) _events.Remove(type);
                else _events[type] = result;
            }
        }

        /// <summary>
        /// Publish an event to all subscribers.
        /// </summary>
        public static void Publish<T>(T eventData) where T : struct
        {
            if (_events.TryGetValue(typeof(T), out var d))
                (d as Action<T>)?.Invoke(eventData);
        }

        /// <summary>
        /// Clear all subscriptions (call on scene unload).
        /// </summary>
        public static void Clear() => _events.Clear();
    }

    // -----------------------------------------------------------------------
    // Event structs
    // -----------------------------------------------------------------------

    /// <summary>Fired when user navigates to a new location.</summary>
    public struct LocationNavigatedEvent
    {
        public string LocationKey;
        public double Longitude;
        public double Latitude;
        public double Height;
    }

    /// <summary>Fired when travel mode changes.</summary>
    public struct ModeChangedEvent
    {
        public string Mode; // "Fly", "Drive", "Browse"
    }

    /// <summary>Fired when settings are updated.</summary>
    public struct SettingsChangedEvent
    {
        public string Setting;  // "Quality", "Time", "Weather", "Language"
        public string Value;
    }

    /// <summary>Fired when search is triggered.</summary>
    public struct SearchRequestedEvent
    {
        public string Query;
    }
}
