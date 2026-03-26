using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.button")]
    public class TuneButtonAction : KeypadBase
    {
        private TuneDialSettings  _s            = new();
        private QSApiClient?      _qsApi;
        private bool              _enabled      = true;
        private bool              _isBusy       = false;
        private int               _tickCount    = 0;
        private int               _selectedParam= 0;
        private List<ParamState>  _params       = new();

        // Press detection
        private DateTime          _keyDownTime  = DateTime.MinValue;
        private DateTime          _lastRelease  = DateTime.MinValue;
        private const int         DoublePressMs = 350;
        private const int         LongPressMs   = 500;

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
            if (payload.Settings == null || payload.Settings.Count == 0) return;
            _s     = payload.Settings.ToObject<TuneDialSettings>() ?? new();
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            BuildParamList();
            _ = RefreshAsync();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s     = payload.Settings.ToObject<TuneDialSettings>() ?? _s;
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            BuildParamList();
            _ = RefreshAsync();
        }

        // ── Press detection ──────────────────────────────────────────────────
        public override void KeyPressed(KeyPayload payload)
        {
            _keyDownTime = DateTime.Now;
        }

        public override void KeyReleased(KeyPayload payload)
        {
            var now     = DateTime.Now;
            var held    = (now - _keyDownTime).TotalMilliseconds;
            var sinceLastRelease = (now - _lastRelease).TotalMilliseconds;
            _lastRelease = now;

            if (sinceLastRelease < DoublePressMs && held < LongPressMs)
            {
                // Double press — cycle parameter
                _ = CycleParamAsync();
            }
            else if (held >= LongPressMs)
            {
                // Long press — decrease
                _ = AdjustAsync(-GetStep());
            }
            else
            {
                // Short press — increase
                _ = AdjustAsync(+GetStep());
            }
        }

        // ── Tick — poll every 3s ─────────────────────────────────────────────
        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 3) { _tickCount = 0; if (!_isBusy) _ = RefreshAsync(); }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
        public override void Dispose() { }

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

        private double GetStep() =>
            _selectedParam < _params.Count ? _params[_selectedParam].Step : 0.1;

        private async Task CycleParamAsync()
        {
            if (_params.Count > 1)
                _selectedParam = (_selectedParam + 1) % _params.Count;
            await UpdateDisplayAsync();
        }

        private async Task AdjustAsync(double delta)
        {
            if (_params.Count == 0 || _qsApi == null) return;
            _isBusy = true;
            try
            {
                var cur = _params[_selectedParam];
                cur.Value = Math.Clamp(Math.Round(cur.Value + delta, 2), cur.Min, cur.Max);

                double v0 = _params.Count > 0 ? _params[0].Value : 0;
                double v1 = _params.Count > 1 ? _params[1].Value : 0;
                double v2 = _params.Count > 2 ? _params[2].Value : 0;
                await _qsApi.EditTuneAsync(_s.ProfileId, _s.TuneGroup, _s.TuneName, v0, v1, v2);
                await UpdateDisplayAsync();
            }
            finally { _isBusy = false; }
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;
            var json = await _qsApi.GetProfileDetailsAsync(_s.ProfileId);
            if (json == null) return;
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
                var p = _params[i];
                displays.Add(new TuneDialRenderer.ParamDisplay(
                    p.Label, p.Value, p.Min, p.Max, _enabled, Selected: i == _selectedParam));
            }

            string img = TuneDialRenderer.RenderStatus(_s.TuneName, _enabled, displays);
            await Connection.SetImageAsync(img);
        }
    }
}
