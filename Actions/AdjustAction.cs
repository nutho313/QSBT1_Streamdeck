using System;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json.Linq;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.adjust")]
    public class AdjustAction : KeyAndEncoderBase
    {
        private AdjustSettings _s         = new();
        private double         _val       = 0;
        private bool           _enabled   = true;
        private bool           _isBusy    = false;
        private QSApiClient?   _qsApi;
        private int            _tickCount = 0;

        public AdjustAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0) return;
            _s     = payload.Settings.ToObject<AdjustSettings>() ?? new();
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = RefreshAsync();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s     = payload.Settings.ToObject<AdjustSettings>() ?? _s;
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = RefreshAsync();
        }

        public override void KeyPressed(KeyPayload payload)  => _ = AdjustAsync(+_s.Step);
        public override void KeyReleased(KeyPayload payload) { }

        public override void DialRotate(DialRotatePayload payload) =>
            _ = AdjustAsync(payload.Ticks * _s.Step);

        public override void DialDown(DialPayload payload)   => _ = ToggleEnabledAsync();
        public override void DialUp(DialPayload payload)     { }

        public override void TouchPress(TouchpadPressPayload payload) => _ = RefreshAsync();

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 1) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
        public override void Dispose() { }

        private async Task AdjustAsync(double delta)
        {
            _isBusy = true;
            try
            {
                _val = Math.Clamp(Math.Round(_val + delta, 2), _s.Min, _s.Max);
                await SendAsync();
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
                var json = await _qsApi.GetProfileDetailsAsync(_s.ProfileId);
                if (json != null) _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
                _enabled = !_enabled;
                await _qsApi.EditTuneEnabledAsync(_s.ProfileId, _s.TuneGroup, _s.TuneName, _enabled);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task SendAsync()
        {
            if (_qsApi == null) return;
            var json = await _qsApi.GetProfileDetailsAsync(_s.ProfileId);
            if (json == null) return;
            double v0 = _s.ParamIndex == 0 ? _val : QSApiClient.GetTuneValue(json, _s.TuneName, 0);
            double v1 = _s.ParamIndex == 1 ? _val : QSApiClient.GetTuneValue(json, _s.TuneName, 1);
            double v2 = _s.ParamIndex == 2 ? _val : QSApiClient.GetTuneValue(json, _s.TuneName, 2);
            await _qsApi.EditTuneAsync(_s.ProfileId, _s.TuneGroup, _s.TuneName, v0, v1, v2);
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;
            var json = await _qsApi.GetProfileDetailsAsync(_s.ProfileId);
            if (json == null) return;
            _val     = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, _s.ParamIndex), 2);
            _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            // ── Keypad button ─────────────────────────────────────────────────
            string enabledStr = _enabled ? "● ON" : "○ OFF";
            await Connection.SetTitleAsync(
                $"{_s.TuneName}\n{_s.ParamLabel}\n{_val:F2}\n{enabledStr}");
            await Connection.SetStateAsync(_enabled ? 1u : 0u);

            // ── Encoder display (Stream Deck +) ───────────────────────────────
            double range     = _s.Max - _s.Min;
            int    indicator = range > 0 ? (int)((_val - _s.Min) / range * 100) : 0;

            // Blue = ON, Orange = OFF
            string color = _enabled ? "#FF5DB3E2" : "#FFFD890D";

            await Connection.SetFeedbackAsync(new JObject
            {
                ["title"]     = $"{_s.ParamLabel}",
                ["value"]     = $"{_val:F2}",
                ["indicator"] = new JObject
                {
                    ["value"]   = indicator,
                    ["enabled"] = true
                }
            });
        }
    }
}
