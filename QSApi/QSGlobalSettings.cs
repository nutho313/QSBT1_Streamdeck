using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace QSBT1_Streamdeck.QSApi
{
    /// <summary>
    /// Cache en mémoire des global settings, mis à jour dès que n'importe quelle
    /// action reçoit un ReceivedGlobalSettings.
    /// </summary>
    public static class QSGlobalSettings
    {
        public static GlobalPluginSettings Current { get; private set; } = new();

        public static void Update(GlobalPluginSettings s)
        {
            Current = s;
        }

        /// <summary>
        /// Détecte automatiquement l'IP locale du PC (première IPv4 non-loopback).
        /// </summary>
        public static string DetectLocalIp()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = addr.Address.ToString();
                    if (ip.StartsWith("127.")) continue;
                    return ip;
                }
            }
            return "192.168.1.1"; // fallback
        }
    }
}
