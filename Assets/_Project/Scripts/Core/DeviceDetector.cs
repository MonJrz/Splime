using System.Runtime.InteropServices;
using UnityEngine;

namespace Splime.Core
{
    /// <summary>
    /// Utilidad para detectar si el juego se está ejecutando en un dispositivo móvil
    /// tanto en WebGL (itch.io) como en plataformas nativas o el Editor de Unity.
    /// </summary>
    public static class DeviceDetector
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern bool IsMobileDevice();
#endif

        /// <summary>
        /// Devuelve true si el cliente actual es un dispositivo móvil (smartphone o tablet).
        /// </summary>
        /// <param name="simulateMobileInEditor">Si es true, simula comportamiento móvil en el Editor de Unity.</param>
        public static bool IsMobile(bool simulateMobileInEditor = false)
        {
#if UNITY_EDITOR
            if (simulateMobileInEditor)
            {
                return true;
            }
            return SystemInfo.deviceType == DeviceType.Handheld;
#elif UNITY_WEBGL
            try
            {
                return IsMobileDevice();
            }
            catch
            {
                return Application.isMobilePlatform;
            }
#else
            return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
#endif
        }
    }
}
