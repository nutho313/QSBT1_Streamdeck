using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.profile")]
    public class ActivateProfileAction : KeypadBase
    {
        private ProfileSettings _s     = new();
        private QSApiClient?    _qsApi;

        public ActivateProfileAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0) return;
            _s     = payload.Settings.ToObject<ProfileSettings>() ?? new();
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = Connection.SetTitleAsync($"▶ {_s.ProfileName}");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            _s = payload.Settings.ToObject<ProfileSettings>() ?? _s;
            _qsApi = new QSApiClient(_s.IpAddress, _s.Port);
            _ = Connection.SetTitleAsync($"▶ {_s.ProfileName}");
        }

        public override void KeyPressed(KeyPayload payload)  => _ = ActivateAsync();
        public override void KeyReleased(KeyPayload payload) { }
        public override void OnTick()                        { }
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }
        public override void Dispose()                       { }

        private async Task ActivateAsync()
        {
            if (_qsApi == null) return;
            bool ok = await _qsApi.ActivateProfileAsync(_s.ProfileId);
            await Connection.SetTitleAsync(ok ? $"✔ {_s.ProfileName}" : "✘ Error");
            await Task.Delay(1500);
            await Connection.SetTitleAsync($"▶ {_s.ProfileName}");
        }
    }
}
