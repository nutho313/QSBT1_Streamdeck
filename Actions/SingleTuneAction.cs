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
        private int                _tickCount       = 0;
        private int                _activeProfileId = 0;

        // Param affiché
        private string  _paramLabel = "";
        private int     _paramIndex = 0;
        private double  _paramValue = 0;
        private double  _paramMin   = 0;
        private double  _paramMax   = 2.5;
        private double  _paramStep  = 0.1;

        // Toutes les valeurs du tune (pour ne pas écraser les autres params)
        private double[] _allValues = new double[6];

        // Multi-appui : impair = +step, pair = -step, fenêtre 250ms
        private int       _pressCount     = 0;
        private const int ConsecMs        = 250;
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

        // ── Key handling ──────────────────────────────────────────────────────
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

            // Multi-appui : impair=+step, pair=-step, résultat après 250ms
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
                    // 1 appui = +step | N appuis = -(N-1)*step
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
            if (_tickCount >= 3) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void Dispose()
        {
            _longPressCts?.Cancel();
            _pendingCts?.Cancel();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void BuildParam()
        {
            _paramLabel = "";
            _paramIndex = _s.ParamIndex;
            _paramMin   = 0;
            _paramMax   = 2.5;
            _paramStep  = 0.1;

            foreach (var p in TuneCatalog.All)
            {
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup && p.ParamIndex == _s.ParamIndex)
                {
                    _paramLabel = p.ParamLabel;
                    _paramIndex = p.ParamIndex;
                    _paramMin   = p.Min;
                    _paramMax   = p.Max;
                    _paramStep  = p.Step;
                    return;
                }
            }
            // Fallback premier param
            foreach (var p in TuneCatalog.All)
            {
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup)
                {
                    _paramLabel = p.ParamLabel;
                    _paramIndex = p.ParamIndex;
                    _paramMin   = p.Min;
                    _paramMax   = p.Max;
                    _paramStep  = p.Step;
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

                // Mettre à jour la valeur du param sélectionné
                _paramValue = Math.Clamp(Math.Round(_paramValue + delta, 2), _paramMin, _paramMax);

                // Mettre à jour dans le tableau complet des valeurs
                if (_paramIndex < _allValues.Length)
                    _allValues[_paramIndex] = _paramValue;

                // Envoyer toutes les valeurs pour ne pas écraser les autres params
                await _qsApi.EditTuneAsync(
                    _activeProfileId, _s.TuneGroup, _s.TuneName,
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

            // Lire toutes les valeurs du tune
            for (int i = 0; i < _allValues.Length; i++)
                _allValues[i] = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, i), 2);

            // Valeur du param affiché
            _paramValue = _paramIndex < _allValues.Length ? _allValues[_paramIndex] : 0;

            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            string img = TuneDialRenderer.RenderSingle(
                _s.TuneName, _enabled,
                _paramLabel, _paramValue,
                _paramMin, _paramMax);
            await Connection.SetImageAsync(img);
        }
    }
}
