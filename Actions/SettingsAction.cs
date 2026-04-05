using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using QSBT1_Streamdeck.QSApi;

namespace QSBT1_Streamdeck.Actions
{
    [PluginActionId("ch.nutho313.qsbt1.settings")]
    public class SettingsAction : KeypadBase
    {
        public SettingsAction(SDConnection conn, InitialPayload payload) : base(conn, payload)
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
        }

        public override void KeyPressed(KeyPayload payload) { }
        public override void KeyReleased(KeyPayload payload) { }
        public override void OnTick() { }
        public override void Dispose() { }
    }
}
