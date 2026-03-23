using System;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.status")]
    public class StatusDisplayAction : KeypadBase
    {
        private StatusSettings _s          = new();
        private QSApiClient?   _qsApi;
        private int            _tickCount  = 0;

        public StatusDisplayAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0) return;
            _s     = payload.Settings.ToObject<StatusSettings>() ?? new();
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = RefreshAsync();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s = payload.Settings.ToObject<StatusSettings>() ?? _s;
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = RefreshAsync();
        }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 3) { _tickCount = 0; _ = RefreshAsync(); }
        }

        public override void KeyPressed(KeyPayload payload)  => _ = RefreshAsync();
        public override void KeyReleased(KeyPayload payload) { }
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
        public override void Dispose()                       { }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;
            var json = await _qsApi.GetProfileDetailsAsync(_s.ProfileId);
            if (json == null) { await Connection.SetTitleAsync("⚠ offline"); return; }

            double val     = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, _s.ParamIndex), 2);
            bool   enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);

            await Connection.SetStateAsync(enabled ? 1u : 0u);
            await Connection.SetTitleAsync(
                $"{_s.TuneName}\n{_s.ParamLabel}\n{val:F2}\n{(enabled ? "● ON" : "○ OFF")}");
        }
    }
}
