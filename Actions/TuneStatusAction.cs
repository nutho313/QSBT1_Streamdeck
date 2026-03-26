using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.status")]
    public class TuneStatusAction : KeypadBase
    {
        private TuneDialSettings  _s         = new();
        private QSApiClient?      _qsApi;
        private bool              _enabled   = true;
        private int               _tickCount = 0;
        private List<ParamState>  _params    = new();

        private class ParamState
        {
            public string Label { get; set; } = "";
            public int    Index { get; set; } = 0;
            public double Value { get; set; } = 0;
            public double Min   { get; set; } = 0;
            public double Max   { get; set; } = 2.5;
        }

        public TuneStatusAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
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

        public override void KeyPressed(KeyPayload payload) => _ = RefreshAsync();
        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
            _tickCount++;
            if (_tickCount >= 3) { _tickCount = 0; _ = RefreshAsync(); }
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
        public override void Dispose() { }

        private void BuildParamList()
        {
            _params.Clear();
            foreach (var p in TuneCatalog.All)
                if (p.TuneName == _s.TuneName && p.TuneGroup == _s.TuneGroup)
                    _params.Add(new ParamState { Label = p.ParamLabel, Index = p.ParamIndex, Min = p.Min, Max = p.Max });
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
            foreach (var p in _params)
                displays.Add(new TuneDialRenderer.ParamDisplay(p.Label, p.Value, p.Min, p.Max, _enabled));

            string img = TuneDialRenderer.RenderStatus(_s.TuneName, _enabled, displays);
            await Connection.SetImageAsync(img);
        }
    }
}
