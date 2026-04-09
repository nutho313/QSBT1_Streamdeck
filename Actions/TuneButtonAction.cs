using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.button")]
    public class TuneButtonAction : KeypadBase
    {
        private TuneDialSettings  _s               = new();
        private QSApiClient?      _qsApi;
        private bool              _enabled         = true;
        private bool              _isBusy          = false;
        private int               _tickCount       = 0;
        private int               _activeProfileId = 0;
        private List<ParamState>  _params          = new();

        // _selectedParam : 0..N-1 = param, N = header (toggle mode)
        private int               _selectedParam   = 0;
        private bool              OnHeader         => _params.Count > 0 && _selectedParam >= _params.Count;

        // Gestion double appui
        private DateTime          _lastShortPress  = DateTime.MinValue;
        private const int         ConsecMs         = 400; // fenêtre double appui
        private CancellationTokenSource? _pendingCts; // délai avant d'envoyer +step

        // Long press
        private DateTime          _keyDownTime     = DateTime.MinValue;
        private const int         LongPressMs      = 1000;
        private bool              _longPressHandled = false;
        private CancellationTokenSource? _longPressCts;

        private class ParamState
        {
            public string Label { get; set; } = "";
            public int    Index { get; set; } = 0;
            public double Value { get; set; } = 0;
            public double Min   { get; set; } = 0;
            public double Max   { get; set; } = 2.5;
            public double Step  { get; set; } = 0.1;
        }

        public TuneButtonAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            _ = Connection.SetTitleAsync(" ");
            _ = ShowPlaceholderAsync();
            if (payload.Settings != null && payload.Settings.Count > 0)
                _s = payload.Settings.ToObject<TuneDialSettings>() ?? new();
            Connection.GetGlobalSettingsAsync();
            BuildParamList();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s = payload.Settings.ToObject<TuneDialSettings>() ?? _s;
            BuildParamList();
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
                    CycleParam();
                    await UpdateDisplayAsync();
                }
                catch (TaskCanceledException) { }
            });
        }

        public override void KeyReleased(KeyPayload payload)
        {
            _longPressCts?.Cancel();

            // Si long press déjà géré, ignorer
            if (_longPressHandled) return;

            var held = (DateTime.Now - _keyDownTime).TotalMilliseconds;
            if (held >= LongPressMs) return;

            // Si on est sur le header : appui court = toggle ON/OFF immédiat
            if (OnHeader)
            {
                _ = ToggleEnabledAsync();
                return;
            }

            // Logique double appui avec délai
            var sinceLastShort = (DateTime.Now - _lastShortPress).TotalMilliseconds;

            if (sinceLastShort < ConsecMs)
            {
                // 2ème appui détecté → annuler le +step en attente, envoyer -step
                _pendingCts?.Cancel();
                _lastShortPress = DateTime.MinValue;
                _ = AdjustAsync(-GetStep());
            }
            else
            {
                // 1er appui → attendre ConsecMs avant d'envoyer +step
                _lastShortPress = DateTime.Now;
                _pendingCts?.Cancel();
                _pendingCts = new CancellationTokenSource();
                var cts = _pendingCts;
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(ConsecMs, cts.Token);
                        _ = AdjustAsync(+GetStep());
                    }
                    catch (TaskCanceledException) { }
                });
            }
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
        private void BuildParamList()
        {
            _params.Clear();
            _selectedParam = 0;
            foreach (var p in TuneCatalog.All)
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup)
                    _params.Add(new ParamState
                    {
                        Label = p.ParamLabel,
                        Index = p.ParamIndex,
                        Min   = p.Min,
                        Max   = p.Max,
                        Step  = p.Step,
                    });
        }

        // Cycle : param 0 → 1 → 2 → header → param 0 → ...
        private void CycleParam()
        {
            if (_params.Count == 0) return;
            _selectedParam = (_selectedParam + 1) % (_params.Count + 1); // +1 pour le header
        }

        private double GetStep() =>
            (!OnHeader && _selectedParam < _params.Count) ? _params[_selectedParam].Step : 0.1;

        private async Task EnsureProfileIdAsync()
        {
            if (_activeProfileId <= 0 && _qsApi != null)
                _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
        }

        private async Task ShowPlaceholderAsync()
        {
            string img = TuneDialRenderer.RenderStatus(
                _s.TuneName.Length > 0 ? _s.TuneName : "No tune",
                false,
                new List<TuneDialRenderer.ParamDisplay>());
            await Connection.SetImageAsync(img);
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
            if (_params.Count == 0 || _qsApi == null || OnHeader) return;
            _isBusy = true;
            try
            {
                await EnsureProfileIdAsync();
                var cur = _params[_selectedParam];
                cur.Value = Math.Clamp(Math.Round(cur.Value + delta, 2), cur.Min, cur.Max);
                double v0 = _params.Count > 0 ? _params[0].Value : 0;
                double v1 = _params.Count > 1 ? _params[1].Value : 0;
                double v2 = _params.Count > 2 ? _params[2].Value : 0;
                await _qsApi.EditTuneAsync(_activeProfileId, _s.TuneGroup, _s.TuneName, v0, v1, v2);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) { await ShowPlaceholderAsync(); return; }
            _activeProfileId = await _qsApi.GetActiveProfileIdAsync();
            if (_activeProfileId <= 0) { await ShowPlaceholderAsync(); return; }
            var json = await _qsApi.GetProfileDetailsAsync(_activeProfileId);
            if (json == null) { await ShowPlaceholderAsync(); return; }
            _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
            foreach (var p in _params)
                p.Value = Math.Round(QSApiClient.GetTuneValue(json, _s.TuneName, p.Index), 2);
            await UpdateDisplayAsync();
        }

        private async Task UpdateDisplayAsync()
        {
            var displays = new List<TuneDialRenderer.ParamDisplay>();
            for (int i = 0; i < _params.Count; i++)
            {
                bool sel = !OnHeader && i == _selectedParam;
                var p = _params[i];
                displays.Add(new TuneDialRenderer.ParamDisplay(
                    p.Label, p.Value, p.Min, p.Max, _enabled, Selected: sel));
            }
            string img = TuneDialRenderer.RenderStatus(_s.TuneName, _enabled, displays, headerSelected: OnHeader);
            await Connection.SetImageAsync(img);
        }
    }
}
