using System;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json.Linq;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.overallfxdial")]
    public class OverallEffectsDialAction : KeyAndEncoderBase
    {
        private QSApiClient? _qsApi;
        private int          _activeProfileId = 0;
        private bool         _isBusy          = false;
        private int          _tickCount       = 0;
        private double       _displayValue    = 0;

        private static readonly (string name, int group, int idx, double min, double max)[] FxTunes = {
            ("Rev Limiter",            17, 0,  2.0, 50.0),
            ("Wheel Forward Slip/Lock",17, 0,  2.0, 50.0),
            ("Wheel Slip Angle",       17, 0,  2.0, 50.0),
            ("ABS Active",             17, 0,  2.0, 50.0),
            ("Rumble Strips Frequency",17, 0,  2.0, 50.0),
            ("Rumble Strips Intensity",17, 0,  0.0,  2.5),
            ("LFE Enhancement",        17, 0,  0.0,  2.5),
        };

        private const double Step = 0.1;

        public OverallEffectsDialAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            _ = Connection.SetTitleAsync(" ");
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

        public override void DialRotate(DialRotatePayload payload) => _ = AdjustAllAsync(payload.Ticks * Step);
        public override void DialDown(DialPayload payload)         => _ = ResetAllAsync();
        public override void DialUp(DialPayload payload)           { }
        public override void TouchPress(TouchpadPressPayload payload) { }
        public override void KeyPressed(KeyPayload payload)        { }
        public override void KeyReleased(KeyPayload payload)       { }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 3) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void Dispose() { }

        private async Task EnsureProfileIdAsync()
        {
            if (_activeProfileId <= 0 && _qsApi != null)
                _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
        }

        private async Task AdjustAllAsync(double delta)
        {
            if (_qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                if (json == null) return;
                double sum = 0; int count = 0;
                foreach (var (name, group, idx, min, max) in FxTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i), 2);
                    all[idx] = Math.Clamp(Math.Round(all[idx] + delta, 2), min, max);
                    sum += all[idx]; count++;
                    await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                }
                _displayValue = Math.Round(sum / count, 2);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task ResetAllAsync()
        {
            if (_qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                if (json == null) return;
                foreach (var (name, group, idx, min, max) in FxTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i), 2);
                    all[idx] = 1.0;
                    await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                }
                _displayValue = 1.0;
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;
            await EnsureProfileIdAsync();
            var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
            if (json == null) return;
            double sum = 0;
            foreach (var (name, _, idx, _, _) in FxTunes)
                sum += QSApiClient.GetTuneValue(json, name, idx);
            _displayValue = Math.Round(sum / FxTunes.Length, 2);
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            string img = TuneDialRenderer.RenderSingleDial(
                "OVR FX", true, "Avg Effect", _displayValue, 0, 2.5);
            await Connection.SetTitleAsync(" ");
            await Connection.SetFeedbackAsync(new JObject { ["full-canvas"] = img });
        }
    }
}
