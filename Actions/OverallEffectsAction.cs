using System;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.overallfx")]
    public class OverallEffectsAction : KeypadBase
    {
        private QSApiClient? _qsApi;
        private int          _activeProfileId = 0;
        private bool         _isBusy          = false;
        private int          _tickCount       = 0;
        private double       _displayValue    = 0;

        // Tunes Effects groupe 17 — Frequency idx 0
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

        private int       _pressCount       = 0;
        private const int ConsecMs         = 250;
        private CancellationTokenSource? _pendingCts;
        private DateTime _keyDownTime      = DateTime.MinValue;
        private const int LongPressMs      = 1000;
        private bool      _longPressHandled = false;
        private CancellationTokenSource? _longPressCts;

        public OverallEffectsAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
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

        public override void KeyPressed(KeyPayload payload)
        {
            _keyDownTime = DateTime.Now; _longPressHandled = false;
            _longPressCts?.Cancel();
            _longPressCts = new CancellationTokenSource();
            var cts = _longPressCts;
            Task.Run(async () =>
            {
                try { await Task.Delay(LongPressMs, cts.Token); _longPressHandled = true; _pendingCts?.Cancel(); _ = ResetAllAsync(); }
                catch (TaskCanceledException) { }
            });
        }

        public override void KeyReleased(KeyPayload payload)
        {
            _longPressCts?.Cancel();
            if (_longPressHandled) return;
            var held = (DateTime.Now - _keyDownTime).TotalMilliseconds;
            if (held >= LongPressMs) return;
            _pressCount++;
            _pendingCts?.Cancel();
            _pendingCts = new CancellationTokenSource();
            var cts = _pendingCts;
            int count = _pressCount;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ConsecMs, cts.Token);
                    double delta = (count % 2 == 1) ? +Step : -Step;
                    _ = AdjustAllAsync(delta);
                    _pressCount = 0;
                }
                catch (TaskCanceledException) { }
            });
        }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 3) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void Dispose() { _longPressCts?.Cancel(); _pendingCts?.Cancel(); }

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
            string img = TuneDialRenderer.RenderSingle(
                "OVR FX", true, "Avg Effect", _displayValue, 0, 2.5);
            await Connection.SetImageAsync(img);
        }
    }
}
