using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.toggle")]
    public class ToggleAction : KeypadBase
    {
        private ToggleSettings _s       = new();
        private bool           _enabled = false;
        private QSApiClient?   _qsApi;

        public ToggleAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0) return;
            _s     = payload.Settings.ToObject<ToggleSettings>() ?? new();
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = RefreshAsync();
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s = payload.Settings.ToObject<ToggleSettings>() ?? _s;
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = RefreshAsync();
        }

        public override void KeyPressed(KeyPayload payload)  => _ = ToggleAsync();
        public override void KeyReleased(KeyPayload payload) { }
        public override void OnTick()                        { }
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
        public override void Dispose()                       { }

        private async Task ToggleAsync()
        {
            if (_qsApi == null) return;
            _enabled = !_enabled;
            await _qsApi.EditTuneEnabledAsync(_s.ProfileId, _s.TuneGroup, _s.TuneName, _enabled);
            await UpdateButtonAsync();
        }

        private async Task RefreshAsync()
        {
            if (_qsApi == null) return;
            var json = await _qsApi.GetProfileDetailsAsync(_s.ProfileId);
            if (json == null) return;
            _enabled = QSApiClient.GetTuneEnabled(json, _s.TuneName);
            await UpdateButtonAsync();
        }

        private async Task UpdateButtonAsync()
        {
            await Connection.SetStateAsync(_enabled ? 1u : 0u);
            await Connection.SetTitleAsync(_enabled
                ? $"{_s.TuneName}\n✅ ON"
                : $"{_s.TuneName}\n❌ OFF");
        }
    }
}
