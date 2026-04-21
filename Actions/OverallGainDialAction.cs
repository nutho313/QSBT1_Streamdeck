using System;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json.Linq;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.overallgaindial")]
    public class OverallGainDialAction : KeyAndEncoderBase
    {
        private QSApiClient? _qsApi;
        private int          _activeProfileId = 0;
        private bool         _isBusy          = false;
        private int          _tickCount       = 0;
        private double       _displayValue    = 0;

        private static readonly (string name, int group, double gainMin, double gainMax)[] GainTunes = {
            ("Braking",               12,  0.0, 2.5),
            ("Acceleration",          12,  0.0, 2.5),
            ("Sideways Acceleration", 12, -2.5, 2.5),
            ("Centrifugal Force",     12, -2.5, 2.5),
            ("Vertical G-Force",      12, -2.5, 2.5),
            ("Road Harshness",        12,  0.0, 2.5),
        };

        private const double Step = 0.1;
        private const double Min  = 0.0;
        private const double Max  = 2.5;

        public OverallGainDialAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
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

        // ── Encoder ──────────────────────────────────────────────────────────
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
                double sum = 0;
                var gainTasks = new System.Collections.Generic.List<Task>();
                foreach (var (name, group, gainMin, gainMax) in GainTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i, group), 2);
                    all[0] = Math.Clamp(Math.Round(all[0] + delta, 2), gainMin, gainMax);
                    sum += all[0];
                    gainTasks.Add(_qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]));
                }
                await Task.WhenAll(gainTasks);
                _displayValue = Math.Round(sum / GainTunes.Length, 2);
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
                foreach (var (name, group, gainMin, gainMax) in GainTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i, group), 2);
                    all[0] = 1.0;
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
            foreach (var (name, _, _, _) in GainTunes)
                sum += QSApiClient.GetTuneValue(json, name, 0);
            _displayValue = Math.Round(sum / GainTunes.Length, 2);
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            string img = TuneDialRenderer.RenderSingleDial(
                "OVR GAIN", true, "Avg Gain", _displayValue, Min, Max);
            await Connection.SetTitleAsync(" ");
            await Connection.SetFeedbackAsync(new JObject { ["full-canvas"] = img });
        }
    }
}
