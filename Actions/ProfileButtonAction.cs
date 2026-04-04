using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.profilebutton")]
    public class ProfileButtonAction : KeypadBase
    {
        private QSApiClient?        _qsApi;
        private int                 _tickCount = 0;
        private List<QSProfile>     _profiles  = new();
        private int                 _activeIdx = 0;

        private class QSProfile
        {
            public int    Id       { get; set; }
            public string Name     { get; set; } = "";
            public bool   IsActive { get; set; }
        }

        public ProfileButtonAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            _ = Connection.SetTitleAsync(" ");
            // Demander les global settings (IP/Port)
            Connection.GetGlobalSettingsAsync();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            var gs = payload.Settings.ToObject<GlobalPluginSettings>() ?? new();

            if (string.IsNullOrWhiteSpace(gs.IpAddress))
                gs.IpAddress = QSGlobalSettings.DetectLocalIp();

            QSGlobalSettings.Update(gs);
            _qsApi = new QSApiClient(gs.IpAddress, gs.Port);
            _ = RefreshAsync();
        }

        public override void KeyPressed(KeyPayload payload) { }
        public override void KeyReleased(KeyPayload payload) => _ = ActivateNextAsync();

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 5) { _tickCount = 0; _ = RefreshAsync(); }
        }

        public override void Dispose() { }

        // ── Activation du profil suivant ──────────────────────────────────────
        private async Task ActivateNextAsync()
        {
            if (_qsApi == null || _profiles.Count == 0) return;

            int nextIdx = (_activeIdx + 1) % _profiles.Count;
            var next    = _profiles[nextIdx];

            bool ok = await _qsApi.ActivateProfileAsync(next.Id);
            if (ok)
            {
                _activeIdx = nextIdx;
                foreach (var p in _profiles) p.IsActive = (p.Id == next.Id);
            }

            await UpdateDisplayAsync();

            await Task.Delay(600);
            await RefreshAsync();
        }

        // ── Refresh depuis le device ──────────────────────────────────────────
        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;

            var json = await _qsApi.GetRawProfilesAsync();
            if (json == null) return;

            _profiles.Clear();
            try
            {
                var parsed = JsonConvert.DeserializeObject<dynamic>(json);
                var arr    = parsed?.profiles ?? parsed;
                if (arr != null)
                {
                    foreach (var p in arr)
                    {
                        var prof = new QSProfile
                        {
                            Id       = (int)p.id,
                            Name     = (string)p.name ?? $"Profile {p.id}",
                            IsActive = (bool)(p.is_active ?? false)
                        };
                        _profiles.Add(prof);
                        if (prof.IsActive)
                            _activeIdx = _profiles.Count - 1;
                    }
                }
            }
            catch { }

            await UpdateDisplayAsync();
        }

        // ── Affichage ─────────────────────────────────────────────────────────
        private async Task UpdateDisplayAsync()
        {
            if (_profiles.Count == 0)
            {
                await Connection.SetImageAsync(
                    TuneDialRenderer.RenderProfileButton("No profiles", "", false));
                return;
            }

            var active  = _activeIdx < _profiles.Count ? _profiles[_activeIdx] : null;
            var nextIdx = (_activeIdx + 1) % _profiles.Count;
            var next    = nextIdx < _profiles.Count ? _profiles[nextIdx] : null;

            string img = TuneDialRenderer.RenderProfileButton(
                active?.Name ?? "—",
                next?.Name   ?? "—",
                true);
            await Connection.SetImageAsync(img);
        }
    }
}
