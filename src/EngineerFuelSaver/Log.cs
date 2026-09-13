using UnityEngine;

namespace EngineerFuelSaver
{
    internal static class Log
    {
        private const string Prefix = "[EngineerFuelSaver] ";

        public static void Info(string message)
        {
            Debug.Log(Prefix + message);
        }

        public static void Warn(string message)
        {
            Debug.LogWarning(Prefix + message);
        }

        public static void Error(string message)
        {
            Debug.LogError(Prefix + message);
        }

        /// <summary>Nur ausgegeben, wenn debugLog = true in der cfg steht.</summary>
        public static void Debugging(string message)
        {
            if (Settings.DebugLog) Debug.Log(Prefix + message);
        }
    }
}
