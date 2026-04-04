using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.dial")]
    public class TuneDialAction : KeyAndEncoderBase
    {
        private TuneDialSettings _s             = new();
        private QSApiClient?     _qsApi;
        private bool             _enabled       = true;
        private bool             _isBusy        = false;
        private int              _tickCount     = 0;
        private int              _selectedParam = 0;
        private int              _activeProfileId = 0;
        private List<ParamState> _params        = new();

        private class ParamState
        {
            public string Label { get; set; } = "";
            public int    Index { get; set; } = 0;
            public double Value { get; set; } = 0;
            public double Min   { get; set; } = 0;
            public double Max   { get; set; } = 2.5;
            public double Step  { get; set; } = 0.1;
        }

        public TuneDialAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            _ = Connection.SetTitleAsync(" ");

            // Charger les settings de l'action (juste TuneName/TuneGroup)
            if (payload.Settings != null && payload.Settings.Count > 0)
                _s = payload.Settings.ToObject<TuneDialSettings>() ?? new();

            // Demander les global settings (IP/Port) — réponse dans ReceivedGlobalSettings
            Connection.GetGlobalSettingsAsync();

            BuildParamList();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s = payload.Settings.ToObject<TuneDialSettings>() ?? _s;
            BuildParamList();
            _ = RefreshAsync();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            var gs = payload.Settings.ToObject<GlobalPluginSettings>() ?? new();

            // Auto-détecter l'IP si vide
            if (string.IsNullOrWhiteSpace(gs.IpAddress))
                gs.IpAddress = QSGlobalSettings.DetectLocalIp();

            QSGlobalSettings.Update(gs);
            _qsApi = new QSApiClient(gs.IpAddress, gs.Port);
            _ = RefreshAsync();
        }

        public override void KeyPressed(KeyPayload payload)  => _ = AdjustAsync(+GetStep());
        public override void KeyReleased(KeyPayload payload) { }
        public override void DialRotate(DialRotatePayload payload) => _ = AdjustAsync(payload.Ticks * GetStep());
        public override void DialDown(DialPayload payload)   => _ = ToggleEnabledAsync();
        public override void DialUp(DialPayload payload)     { }
        public override void TouchPress(TouchpadPressPayload payload)
        {
            if (_params.Count > 1) _selectedParam = (_selectedParam + 1) % _params.Count;
            _ = UpdateDisplayAsync();
        }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 1) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void Dispose() { }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void BuildParamList()
        {
            _params.Clear();
            _selectedParam = 0;
            foreach (var p in TuneCatalog.All)
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup)
                    _params.Add(new ParamState { Label = p.ParamLabel, Index = p.ParamIndex, Min = p.Min, Max = p.Max, Step = p.Step });
        }

        private double GetStep() => _selectedParam < _params.Count ? _params[_selectedParam].Step : 0.1;

        private async Task EnsureProfileIdAsync()
        {
            if (_activeProfileId <= 0 && _qsApi != null)
                _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
        }

        private async Task AdjustAsync(double delta)
        {
            if (_params.Count == 0 || _qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                var cur = _params[_selectedParam];
                cur.Value = Math.Clamp(Math.Round(cur.Value + delta, 2), cur.Min, cur.Max);
                double v0 = _params.Count > 0 ? _params[0].Value : 0;
                double v1 = _params.Count > 1 ? _params[1].Value : 0;
                double v2 = _params.Count > 2 ? _params[2].Value : 0;
                await _qsApi.EditTuneAsync(_activeProfileId, _s.TuneGroup, _s.TuneName, v0, v1, v2);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task ToggleEnabledAsync()
        {
            if (_qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                if (json != null) _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
                _enabled = !_enabled;
                await _qsApi.EditTuneEnabledAsync(_activeProfileId, _s.TuneGroup, _s.TuneName, _enabled);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;

            // Toujours re-détecter le profil actif
            _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
            if (_activeProfileId <= 0) return;

            var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
            if (json == null) return;

            _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
            foreach (var p in _params)
                p.Value = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, p.Index), 2);

            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            var displays = new List<TuneDialRenderer.ParamDisplay>();
            for (int i = 0; i < _params.Count; i++)
            {
                var p = _params[i];
                displays.Add(new TuneDialRenderer.ParamDisplay(p.Label, p.Value, p.Min, p.Max, _enabled, Selected: i == _selectedParam));
            }
            string img = TuneDialRenderer.Render(_s.TuneName, _enabled, displays);
            await Connection.SetFeedbackAsync(new JObject { ["full-canvas"] = img });
            await Connection.SetStateAsync(_enabled ? 1u : 0u);
        }
    }
}
