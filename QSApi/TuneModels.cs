using System.Collections.Generic;
using Newtonsoft.Json;

namespace QSBT1_Streamdeck.QSApi
{
    public static class TuneCatalog
    {
        public record TuneParam(string TuneName, int TuneGroup, string ParamLabel, int ParamIndex,
                                 double Min, double Max, double Step);

        public static readonly List<TuneParam> All = new()
        {
            new("Braking",               12, "Gain",       0,  0.0,   2.5, 0.1),
            new("Braking",               12, "Sharpness",  1,  0.1,   2.5, 0.1),
            new("Braking",               12, "Deadzone",   2,  0.0,   2.5, 0.1),
            new("Acceleration",          12, "Gain",       0,  0.0,   2.5, 0.1),
            new("Acceleration",          12, "Sharpness",  1,  0.1,   2.5, 0.1),
            new("Acceleration",          12, "Deadzone",   2,  0.0,   2.5, 0.1),
            new("Sideways Acceleration", 12, "Gain",       0, -2.5,   2.5, 0.1),
            new("Sideways Acceleration", 12, "Sharpness",  1,  0.1,   2.5, 0.1),
            new("Sideways Acceleration", 12, "Deadzone",   2,  0.0,   2.5, 0.1),
            new("Centrifugal Force",     12, "Gain",       0, -2.5,   2.5, 0.1),
            new("Centrifugal Force",     12, "Sharpness",  1,  0.1,   2.5, 0.1),
            new("Centrifugal Force",     12, "Deadzone",   2,  0.0,   2.5, 0.1),
            new("Bounds",                12, "Neutral",    0,  0.0,  30.0, 1.0),
            new("Bounds",                12, "Maximum",    1, 50.0, 100.0, 1.0),
            new("Vertical G-Force",      12, "Gain",       0, -2.5,   2.5, 0.1),
            new("Vertical G-Force",      12, "Sharpness",  1,  0.0,   2.5, 0.1),
            new("Side Slip",             12, "Threshold",  0,  0.0,  60.0, 0.5),
            new("Side Slip",             12, "Frequency",  1,  0.0,  50.0, 1.0),
            new("Side Slip",             12, "Intensity",  2,  0.0,   2.5, 0.1),
            new("Road Harshness",        12, "Gain",       0,  0.0,   2.5, 0.1),
            new("Road Harshness",        12, "Sharpness",  1,  0.0,   2.5, 0.1),
            new("Pre-Impact Protection", 12, "Long.",      0,  4.0, 100.0, 1.0),
            new("Pre-Impact Protection", 12, "Lateral",    1,  4.0, 100.0, 1.0),
            new("Pre-Impact Protection", 12, "Duration",   2,  0.0,2000.0,10.0),
            new("Violent Movement Threshold",        2, "Threshold", 0,  4.0, 100.0, 1.0),
            new("Violent Movement Suppression Time", 2, "Duration",  0,  1.0,   7.0, 1.0),
            new("Rev Limiter",           17, "Frequency",  0,  2.0,  50.0, 1.0),
            new("Rev Limiter",           17, "Intensity",  1,  0.0,   2.5, 0.1),
            new("Gear Change Effect",    17, "Duration",   0, 20.0, 250.0, 5.0),
            new("Gear Change Effect",    17, "Downshift",  1,  0.0,   2.5, 0.1),
            new("Gear Change Effect",    17, "Upshift",    2,  0.0,   2.5, 0.1),
            new("Wheel Forward Slip/Lock", 17, "Frequency",0,  2.0,  50.0, 1.0),
            new("Wheel Forward Slip/Lock", 17, "Intensity",1,  0.0,   2.5, 0.1),
            new("Wheel Slip Angle",      17, "Frequency",  0,  2.0,  50.0, 1.0),
            new("Wheel Slip Angle",      17, "Intensity",  1,  0.0,   2.5, 0.1),
            new("ABS Active",            17, "Frequency",  0,  2.0,  50.0, 1.0),
            new("ABS Active",            17, "Intensity",  1,  0.0,   2.5, 0.1),
            new("Rumble Strips Frequency",  17, "At 20kmh",  0,  2.0, 50.0, 1.0),
            new("Rumble Strips Frequency",  17, "At 300kmh", 1,  2.0, 50.0, 1.0),
            new("Rumble Strips Intensity",  17, "At 20kmh",  0,  0.0,  2.5, 0.1),
            new("Rumble Strips Intensity",  17, "At 300kmh", 1,  0.0,  2.5, 0.1),
            new("Engine Vibration Extra",17, "Phase Shift",0,  0.0, 250.0, 5.0),
            new("Engine Vibration Extra",17, "Alone",      1,  0.0,   2.5, 0.1),
            new("Engine Vibration Extra",17, "In-Group",   2,  0.0,   1.0, 0.05),
            new("LFE Enhancement",       17, "Gain",       0,  0.0,   2.5, 0.1),
            new("LFE Enhancement",       17, "Sharpness",  1,  0.1,   2.5, 0.1),
        };
    }

    // Global settings — IP + Port partagés entre toutes les actions
    // Stockés via setGlobalSettings / getGlobalSettings du SDK
    public class GlobalPluginSettings
    {
        [JsonProperty("IpAddress")] public string IpAddress { get; set; } = "";
        [JsonProperty("Port")]      public int    Port      { get; set; } = 8081;
    }

    // Per-action settings pour Tune Dial et Tune Button — juste le tune
    public class TuneDialSettings
    {
        [JsonProperty("TuneName")]  public string TuneName  { get; set; } = "Braking";
        [JsonProperty("TuneGroup")] public int    TuneGroup { get; set; } = 12;
    }
}
