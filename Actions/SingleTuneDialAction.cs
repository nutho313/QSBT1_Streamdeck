using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json.Linq;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.singletunedin")]
    public class SingleTuneDialAction : KeyAndEncoderBase
    {
        private SingleTuneSettings _s               = new();
        private QSApiClient?       _qsApi;
        private bool               _enabled         = true;
        private bool               _isBusy          = false;
        private int                _tickCount       = 0;
        private int                _activeProfileId = 0;

        private string   _paramLabel = "";
        private int      _paramIndex = 0;
        private double   _paramValue = 0;
        private double   _paramMin   = 0;
        private double   _paramMax   = 2.5;
        private double   _paramStep  = 0.1;
        private double[] _allValues  = new double[6];

        public SingleTuneDialAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            _ = Connection.SetTitleAsync(" ");
            if (payload.Settings != null && payload.Settings.Count > 0)
                _s = payload.Settings.ToObject<SingleTuneSettings>() ?? new();
            Connection.GetGlobalSettingsAsync();
            BuildParam();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s = payload.Settings.ToObject<SingleTuneSettings>() ?? _s;
            BuildParam();
            _ = RefreshAsync();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {
            var gs = payload.Settings.ToObject<GlobalPluginSettings>() ?? new();
            if (string.IsNullOrWhiteSpace(gs.IpAddress))
                gs.IpAddress = QSGlobalSettings.DetectLocalIp();
            QSGlobalSettings.Update(gs);
            _qsApi = new QSApiClient(gs.IpAddress, gs.Port);
            _ = RefreshAsync();
        }

        // ── Encoder ──────────────────────────────────────────────────────────
        public override void DialRotate(DialRotatePayload payload) => _ = AdjustAsync(payload.Ticks * _paramStep);
        public override void DialDown(DialPayload payload)         => _ = ToggleEnabledAsync();
        public override void DialUp(DialPayload payload)           { }
        public override void TouchPress(TouchpadPressPayload payload) { }

        // Keypad fallback
        public override void KeyPressed(KeyPayload payload)  => _ = AdjustAsync(+_paramStep);
        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 1) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void Dispose() { }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void BuildParam()
        {
            _paramLabel = ""; _paramIndex = _s.ParamIndex;
            _paramMin = 0; _paramMax = 2.5; _paramStep = 0.1;
            foreach (var p in TuneCatalog.All)
            {
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup && p.ParamIndex == _s.ParamIndex)
                {
                    _paramLabel = p.ParamLabel; _paramIndex = p.ParamIndex;
                    _paramMin = p.Min; _paramMax = p.Max; _paramStep = p.Step;
                    return;
                }
            }
            foreach (var p in TuneCatalog.All)
            {
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup)
                {
                    _paramLabel = p.ParamLabel; _paramIndex = p.ParamIndex;
                    _paramMin = p.Min; _paramMax = p.Max; _paramStep = p.Step;
                    return;
                }
            }
        }

        private async Task EnsureProfileIdAsync()
        {
            if (_activeProfileId <= 0 && _qsApi != null)
                _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
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

        private async Task AdjustAsync(double delta)
        {
            if (_qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                _paramValue = Math.Clamp(Math.Round(_paramValue + delta, 2), _paramMin, _paramMax);
                if (_paramIndex < _allValues.Length)
                    _allValues[_paramIndex] = _paramValue;
                await _qsApi.EditTuneAsync(_activeProfileId, _s.TuneGroup, _s.TuneName,
                    _allValues.Length > 0 ? _allValues[0] : 0,
                    _allValues.Length > 1 ? _allValues[1] : 0,
                    _allValues.Length > 2 ? _allValues[2] : 0);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;
            _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
            if (_activeProfileId <= 0) return;
            var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
            if (json == null) return;
            _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
            for (int i = 0; i < _allValues.Length; i++)
                _allValues[i] = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, i), 2);
            _paramValue = _paramIndex < _allValues.Length ? _allValues[_paramIndex] : 0;
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            string img = TuneDialRenderer.RenderSingleDial(
                _s.TuneName, _enabled, _paramLabel, _paramValue, _paramMin, _paramMax);
            await Connection.SetTitleAsync(" ");
            await Connection.SetFeedbackAsync(new JObject { ["full-canvas"] = img });
        }
    }
}
