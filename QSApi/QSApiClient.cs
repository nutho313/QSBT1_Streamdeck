using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace QSBT1_Streamdeck.QSApi
{
    public class QSApiClient
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(4) };
        private readonly string _base;

        public QSApiClient(string ip, int port) =>
            _base = $"http://{ip}:{port}";

        // ── Read ─────────────────────────────────────────────────────────────
        public async Task<string?> GetProfileDetailsAsync(int profileId)
        {
            try { return await _http.GetStringAsync($"{_base}/api/profile/{profileId}/details"); }
            catch { return null; }
        }

        public async Task<int> GetActiveProfileIdAsync()
        {
            try
            {
                var json = await _http.GetStringAsync($"{_base}/api/profilesInstalled");
                // Find first "is_active":true and grab its id
                int idx = json.IndexOf("\"is_active\":true", StringComparison.Ordinal);
                if (idx < 0) return -1;
                // Walk back to find "id":
                int idIdx = json.LastIndexOf("\"id\":", idx, StringComparison.Ordinal);
                if (idIdx < 0) return -1;
                int start = idIdx + 5;
                int end   = json.IndexOf(',', start);
                if (end < 0) end = json.IndexOf('}', start);
                return int.TryParse(json[start..end].Trim(), out int id) ? id : -1;
            }
            catch { return -1; }
        }

        // ── Parse helpers ────────────────────────────────────────────────────
        public static double GetTuneValue(string json, string tuneName, int index)
        {
            int objStart = FindTuneObjectStart(json, tuneName);
            if (objStart < 0) return 0;
            int cvIdx = json.IndexOf("\"currentValue\":[", objStart, 800, StringComparison.Ordinal);
            if (cvIdx < 0) return 0;
            int arrStart = json.IndexOf('[', cvIdx) + 1;
            var ic = CultureInfo.InvariantCulture;
            int cur = arrStart, i = 0;
            while (cur < arrStart + 300)
            {
                int comma = json.IndexOf(',', cur);
                int close = json.IndexOf(']', cur);
                int end   = (comma >= 0 && comma < close) ? comma : close;
                if (end < 0) break;
                if (i == index)
                    return double.TryParse(json[cur..end].Trim(), System.Globalization.NumberStyles.Any, ic, out double v) ? v : 0;
                cur = end + 1;
                i++;
            }
            return 0;
        }

        public static bool GetTuneEnabled(string json, string tuneName)
        {
            int objStart = FindTuneObjectStart(json, tuneName);
            if (objStart < 0) return false;
            // Search for "enabled": within 800 chars of the tune object
            int enIdx = json.IndexOf("\"enabled\":", objStart, Math.Min(800, json.Length - objStart), StringComparison.Ordinal);
            if (enIdx < 0) return false;
            int valStart = enIdx + 10;
            // Skip whitespace
            while (valStart < json.Length && json[valStart] == ' ') valStart++;
            // Check next 4-5 chars
            if (valStart + 4 > json.Length) return false;
            string val = json.Substring(valStart, Math.Min(5, json.Length - valStart)).Trim();
            return val.StartsWith("true", StringComparison.OrdinalIgnoreCase);
        }

        private static int FindTuneObjectStart(string json, string tuneName)
        {
            var boundary = "{\"canBeDisabledByUser\"";
            var nameTag  = $"\"name\":\"{tuneName}\"";
            int pos = 0;
            while (true)
            {
                int next = json.IndexOf(boundary, pos, StringComparison.Ordinal);
                if (next < 0) return -1;
                int nameIdx = json.IndexOf(nameTag, next, Math.Min(800, json.Length - next), StringComparison.Ordinal);
                if (nameIdx >= 0) return next;
                pos = next + 1;
            }
        }

        // ── Write ─────────────────────────────────────────────────────────────
        public async Task<bool> EditTuneAsync(int profileId, int tuneGroup, string tuneName,
                                               double v0, double v1 = 0, double v2 = 0)
        {
            var ic   = CultureInfo.InvariantCulture;
            var url  = $"{_base}/api/editTune";
            var body = $"{{\"profileId\":{profileId},\"tuneGroup\":{tuneGroup}," +
                       $"\"tuneName\":\"{tuneName}\"," +
                       $"\"tuneValue\":[{v0.ToString(ic)},{v1.ToString(ic)},{v2.ToString(ic)},0,0,0]}}";
            return await PostJsonAsync(url, body);
        }

        public async Task<bool> EditTuneEnabledAsync(int profileId, int tuneGroup,
                                                       string tuneName, bool enabled)
        {
            var url  = $"{_base}/api/editTuneEnabled";
            var body = $"{{\"profileId\":{profileId},\"tuneGroup\":{tuneGroup}," +
                       $"\"tuneName\":\"{tuneName}\",\"tuneEnabled\":{(enabled ? "true" : "false")}}}";
            return await PostJsonAsync(url, body);
        }

        public async Task<bool> ActivateProfileAsync(int profileId)
        {
            var url  = $"{_base}/api/activateProfile";
            var body = $"{{\"profileId\":{profileId}}}";
            return await PostJsonAsync(url, body);
        }

        private async Task<bool> PostJsonAsync(string url, string json)
        {
            try
            {
                var content  = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
