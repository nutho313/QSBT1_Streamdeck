using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.singletune")]
    public class SingleTuneAction : KeypadBase
    {
        private SingleTuneSettings _s               = new();
        private QSApiClient?       _qsApi;
        private bool               _enabled         = true;
        private bool               _isBusy          = false;
        private DateTime           _lastAdjustTime  = DateTime.MinValue;
        private int                _tickCount       = 0;
        private int                _activeProfileId = 0;

        // Param affiché (mode normal)
        private string   _paramLabel = "";
        private int      _paramIndex = 0;
        private double   _paramValue = 0;
        private double   _paramMin   = 0;
        private double   _paramMax   = 2.5;
        private double   _paramStep  = 0.1;
        private double[] _allValues  = new double[6];

        // Overall Gain tunes
        private static readonly (string name, int group, double gainMin, double gainMax)[] GainTunes = {
            ("Braking",               12,  0.0, 2.5),
            ("Acceleration",          12,  0.0, 2.5),
            ("Sideways Acceleration", 12, -2.5, 2.5),
            ("Centrifugal Force",     12, -2.5, 2.5),
            ("Vertical G-Force",      12, -2.5, 2.5),
            ("Road Harshness",        12,  0.0, 2.5),
        };

        // Overall FX tunes — Intensity (idx 1) pour tous sauf Rumble Strips qui a 2 params
        // (name, group, idx, min, max, adjustBothParams)
        private static readonly (string name, int group, int idx, double min, double max, bool both)[] FxTunes = {
            ("Rev Limiter",            17, 1, 0.0, 2.5, false),
            ("Wheel Forward Slip/Lock",17, 1, 0.0, 2.5, false),
            ("Wheel Slip Angle",       17, 1, 0.0, 2.5, false),
            ("ABS Active",             17, 1, 0.0, 2.5, false),
            ("Rumble Strips Frequency",17, 0, 2.0,50.0, true),   // at 20kmh + at 300kmh
            ("Rumble Strips Intensity",17, 0, 0.0, 2.5, true),   // at 20kmh + at 300kmh
            ("Engine Vibration Extra",        17, 0, 0.0, 2.5, false),
        };

        // Multi-press
        private int       _pressCount      = 0;
        private const int ConsecMs         = 300;
        private CancellationTokenSource? _pendingCts;

        // Long press
        private DateTime _keyDownTime      = DateTime.MinValue;
        private const int LongPressMs      = 1000;
        private bool      _longPressHandled = false;
        private CancellationTokenSource? _longPressCts;

        public SingleTuneAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
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
                    // Long press : toggle (normal) ou reset (overall)
                    if (_s.IsOverall)
                        _ = ResetAllAsync();
                    else
                        _ = ToggleEnabledAsync();
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
            var cts     = _pendingCts;
            int count   = _pressCount;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ConsecMs, cts.Token);
                    double delta = (count == 1) ? +_paramStep : -(count - 1) * _paramStep;
                    _ = AdjustAsync(delta);
                    _pressCount = 0;
                }
                catch (TaskCanceledException) { }
            });
        }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 3) { _tickCount = 0; if (!_isBusy && (DateTime.Now - _lastAdjustTime).TotalSeconds > 2) _ = RefreshAsync(); }
        }

        public override void Dispose() { _longPressCts?.Cancel(); _pendingCts?.Cancel(); }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void BuildParam()
        {
            if (_s.IsOverall)
            {
                // Overall : label et step fixes, range 0-2.5
                _paramLabel = _s.OverallType == "gain" ? "Avg Gain" : "Avg Effect";
                _paramIndex = 0;
                _paramMin   = 0;
                _paramMax   = 2.5;
                _paramStep  = 0.1;
                return;
            }

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

        private async Task AdjustAsync(double delta)
        {
            if (_qsApi == null) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                if (_s.IsOverall)
                {
                    await AdjustAllAsync(delta);
                    return;
                }
                var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                if (json == null) return;
                for (int i = 0; i < _allValues.Length; i++)
                    _allValues[i] = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, i, _s.TuneGroup), 2);
                _paramValue = Math.Clamp(Math.Round(_paramValue + delta, 2), _paramMin, _paramMax);
                if (_paramIndex < _allValues.Length)
                    _allValues[_paramIndex] = _paramValue;
                await _qsApi.EditTuneAsync(_activeProfileId, _s.TuneGroup, _s.TuneName,
                    _allValues[0], _allValues[1], _allValues[2]);
                _lastAdjustTime = DateTime.Now;
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task AdjustAllAsync(double delta)
        {
            if (_qsApi == null) return;
            var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
            if (json == null) return;

            if (_s.OverallType == "gain")
            {
                double sum = 0;
                foreach (var (name, group, gainMin, gainMax) in GainTunes)
                {
                    // Relire les valeurs fraîches pour chaque tune
                    var freshJson = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                    if (freshJson == null) continue;
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(freshJson, name, i), 2);
                    all[0] = Math.Clamp(Math.Round(all[0] + delta, 2), gainMin, gainMax);
                    sum += all[0];
                    await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                }
                _paramValue = Math.Round(sum / GainTunes.Length, 2);
            }
            else
            {
                double sum = 0; int count = 0;
                foreach (var (name, group, idx, min, max, both) in FxTunes)
                {
                    var freshJson = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
                    if (freshJson == null) continue;
                    double[] all = new double[6];
                    for (int i = 0; i < 6; i++)
                        all[i] = Math.Round(QSApiClient.GetTuneValue(freshJson, name, i), 2);
                    all[idx] = Math.Clamp(Math.Round(all[idx] + delta, 2), min, max);
                    if (both) // Rumble Strips : ajuster les 2 params (20kmh et 300kmh)
                        all[1] = Math.Clamp(Math.Round(all[1] + delta, 2), min, max);
                    sum += all[idx]; count++;
                    await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                }
                _paramValue = Math.Round(sum / count, 2);
            }
            _lastAdjustTime = DateTime.Now;
            await UpdateDisplayAsync();
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
                if (_s.OverallType == "gain")
                {
                    foreach (var (name, group, _, _) in GainTunes)
                    {
                        double[] all = new double[6];
                        for (int i = 0; i < 6; i++)
                            all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i), 2);
                        all[0] = 1.0;
                        await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                    }
                }
                else
                {
                    foreach (var (name, group, idx, _, _, both) in FxTunes)
                    {
                        double[] all = new double[6];
                        for (int i = 0; i < 6; i++)
                            all[i] = Math.Round(QSApiClient.GetTuneValue(json, name, i), 2);
                        all[idx] = 1.0;
                        if (both) all[1] = 1.0;
                        await _qsApi.EditTuneAsync(_activeProfileId, group, name, all[0], all[1], all[2]);
                    }
                }
                _paramValue = 1.0;
                _lastAdjustTime = DateTime.Now;
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
                if (json != null) _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName, _s.TuneGroup);
                _enabled = !_enabled;
                await _qsApi.EditTuneEnabledAsync(_activeProfileId, _s.TuneGroup, _s.TuneName, _enabled);
                _lastAdjustTime = DateTime.Now;
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

            if (_s.IsOverall)
            {
                // Overall : toujours actif, calculer la moyenne
                _enabled = true;
                if (_s.OverallType == "gain")
                {
                    double sum = 0;
                    foreach (var (name, _, _, _) in GainTunes)
                        sum += QSApiClient.GetTuneValue(json, name, 0);
                    _paramValue = Math.Round(sum / GainTunes.Length, 2);
                }
                else
                {
                    double sum = 0;
                    foreach (var (name, _, idx, _, _, _) in FxTunes)
                        sum += QSApiClient.GetTuneValue(json, name, idx);
                    _paramValue = Math.Round(sum / FxTunes.Length, 2);
                }
            }
            else
            {
                _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName, _s.TuneGroup);
                for (int i = 0; i < _allValues.Length; i++)
                    _allValues[i] = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, i, _s.TuneGroup), 2);
                _paramValue = _paramIndex < _allValues.Length ? _allValues[_paramIndex] : 0;
            }
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            // Overall toujours enabled=true
            bool displayEnabled = _s.IsOverall ? true : _enabled;
            string tuneName = _s.IsOverall
                ? (_s.OverallType == "gain" ? "Overall Gain" : "Overall FX")
                : _s.TuneName;

            string img = TuneDialRenderer.RenderSingle(
                tuneName, displayEnabled, _paramLabel, _paramValue, _paramMin, _paramMax);
            await Connection.SetImageAsync(img);
        }
    }
}
