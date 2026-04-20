using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.overallgain")]
    public class OverallGainAction : KeypadBase
    {
        private QSApiClient? _qsApi;
        private int          _activeProfileId = 0;
        private bool         _isBusy          = false;
        private int          _tickCount       = 0;
        private double       _displayValue    = 0;

        // Tunes Gain groupe 12
        private static readonly (string name, int group)[] GainTunes = {
            ("Braking",               12),
            ("Acceleration",          12),
            ("Sideways Acceleration", 12),
            ("Centrifugal Force",     12),
            ("Vertical G-Force",      12),
            ("Road Harshness",        12),
        };

        private const double Step = 0.1;
        private const double Min  = 0.0;
        private const double Max  = 2.5;

        // Double appui
        private int       _pressCount       = 0;
        private const int ConsecMs         = 250;
        private CancellationTokenSource? _pendingCts;

        // Long press
        private DateTime _keyDownTime      = DateTime.MinValue;
        private const int LongPressMs      = 1000;
        private bool      _longPressHandled = false;
        private CancellationTokenSource? _longPressCts;

        public OverallGainAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
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
            _keyDownTime      = DateTime.Now;
            _longPressHandled = false;
            _longPressCts?.Cancel();
            _longPressCts = new CancellationTokenSource();
            var cts = _longPressCts;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(LongPressMs, cts.Token);
                    _longPressHandled = true;
                    _pendingCts?.Cancel();
                    // Long press = reset to 1.0
                    _ = SetAllAsync(1.0);
                }
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
                    double delta = (count == 1) ? +Step : -(count - 1) * Step;
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

                double sum = 0;
                foreach (var (name, group) in GainTunes)
                {
                    double cur = QSApiClient.GetTuneValue(json, name, 0);
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i), 2);
                    double newVal = Math.Clamp(Math.Round(cur + delta, 2), Min, Max);
                    all[0] = newVal;
                    sum += newVal;
                    await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                }
                _displayValue = Math.Round(sum / GainTunes.Length, 2);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task SetAllAsync(double value)
        {
            if (_qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                if (json == null) return;
                foreach (var (name, group) in GainTunes)
                {
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i), 2);
                    all[0] = value;
                    await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                }
                _displayValue = value;
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
            foreach (var (name, _) in GainTunes)
                sum += QSApiClient.GetTuneValue(json, name, 0);
            _displayValue = Math.Round(sum / GainTunes.Length, 2);
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            string img = TuneDialRenderer.RenderSingle(
                "OVR GAIN", true, "Avg Gain", _displayValue, Min, Max);
            await Connection.SetImageAsync(img);
        }
    }
}
