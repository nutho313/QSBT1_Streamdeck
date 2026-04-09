using BarRaider.SdTools;
using BarRaider.SdTools.Payloads;
using Newtonsoft.Json.Linq;
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

            // Auto-détecter et sauvegarder si IP vide
            if (string.IsNullOrWhiteSpace(gs.IpAddress))
            {
                gs.IpAddress = QSGlobalSettings.DetectLocalIp();
                _ = Connection.SetGlobalSettingsAsync(JObject.FromObject(gs));
            }

            QSGlobalSettings.Update(gs);

            // Pousser l'IP détectée vers le PI
            _ = Connection.SendToPropertyInspectorAsync(new JObject
            {
                ["detectedIp"] = gs.IpAddress,
                ["port"]       = gs.Port
            });
        }

        public override void KeyPressed(KeyPayload payload) { }
        public override void KeyReleased(KeyPayload payload) { }
        public override void OnTick() { }
        public override void Dispose() { }
    }
}
