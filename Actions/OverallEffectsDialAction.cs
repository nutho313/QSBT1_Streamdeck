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

        // Overall FX — Intensity (idx 1) pour effets vibratoires
        // Rumble Strips : both=true → ajuste At 20kmh (idx 0) ET At 300kmh (idx 1) ensemble
        private static readonly (string name, int group, int idx, double min, double max, bool both)[] FxTunes = {
            ("Rev Limiter",           17, 1, 0.0, 2.5, false),  // Intensity
            ("Wheel Forward Slip/Lock",17,1, 0.0, 2.5, false),  // Intensity
            ("Wheel Slip Angle",      17, 1, 0.0, 2.5, false),  // Intensity
            ("ABS Active",            17, 1, 0.0, 2.5, false),  // Intensity
            ("Rumble Strips Intensity",17,0, 0.0, 2.5, true),   // At 20kmh + At 300kmh
            ("Engine Vibration Extra",17, 1, 0.0, 2.5, false),  // Alone
        };

        private const double Step = 0.1;

        public OverallEffectsDialAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            _ = Connection.SetTitleAsync(" ");
            // Init immédiate depuis les settings en cache si disponibles
            var cached = QSGlobalSettings.Current;
            if (!string.IsNullOrWhiteSpace(cached.IpAddress))
                _qsApi = new QSApiClient(cached.IpAddress, cached.Port);
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
                var fxTaskList = new System.Collections.Generic.List<Task>();
                foreach (var (name, group, idx, min, max, both) in FxTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i, group), 2);
                    all[idx] = Math.Clamp(Math.Round(all[idx] + delta, 2), min, max);
                    if (both) all[1] = Math.Clamp(Math.Round(all[1] + delta, 2), min, max);
                    sum += all[idx]; count++;
                    fxTaskList.Add(_qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]));
                }
                await Task.WhenAll(fxTaskList);
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
                foreach (var (name, group, idx, min, max, both) in FxTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i, group), 2);
                    all[idx] = 1.0;
                    if (both) all[1] = 1.0;
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
            foreach (var (name, _, idx, _, _, _) in FxTunes)
                sum += QSApiClient.GetTuneValue(json, name, idx);
            _displayValue = Math.Round(sum / FxTunes.Length, 2);
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            string img = TuneDialRenderer.RenderSingleDial(
                "OVR FX", true, "Avg Intensity", _displayValue, 0, 2.5);
            await Connection.SetTitleAsync(" ");
            await Connection.SetFeedbackAsync(new JObject { ["full-canvas"] = img });
        }
    }
}
