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
        };

        public static readonly List<(string TuneName, int TuneGroup)> Toggleable = new()
        {
            ("Braking", 12), ("Acceleration", 12), ("Sideways Acceleration", 12),
            ("Centrifugal Force", 12), ("Bounds", 12), ("Vertical G-Force", 12),
            ("Side Slip", 12), ("Road Harshness", 12), ("Pre-Impact Protection", 12),
            ("Violent Movement Threshold", 2),
            ("Rev Limiter", 17), ("Gear Change Effect", 17), ("Wheel Forward Slip/Lock", 17),
            ("Wheel Slip Angle", 17), ("ABS Active", 17),
            ("Rumble Strips Intensity", 17), ("LFE Enhancement", 17),
        };
    }

    public class AdjustSettings
    {
        [JsonProperty("IpAddress")]  public string IpAddress  { get; set; } = "192.168.8.131";
        [JsonProperty("Port")]       public int    Port       { get; set; } = 8081;
        [JsonProperty("ProfileId")]  public int    ProfileId  { get; set; } = 276;
        [JsonProperty("TuneName")]   public string TuneName   { get; set; } = "Braking";
        [JsonProperty("TuneGroup")]  public int    TuneGroup  { get; set; } = 12;
        [JsonProperty("ParamLabel")] public string ParamLabel { get; set; } = "Gain";
        [JsonProperty("ParamIndex")] public int    ParamIndex { get; set; } = 0;
        [JsonProperty("Min")]        public double Min        { get; set; } = 0.0;
        [JsonProperty("Max")]        public double Max        { get; set; } = 2.5;
        [JsonProperty("Step")]       public double Step       { get; set; } = 0.1;
        [JsonProperty("DefaultVal")] public double DefaultVal { get; set; } = 1.0;
    }

    public class ToggleSettings
    {
        [JsonProperty("IpAddress")] public string IpAddress { get; set; } = "192.168.8.131";
        [JsonProperty("Port")]      public int    Port      { get; set; } = 8081;
        [JsonProperty("ProfileId")] public int    ProfileId { get; set; } = 276;
        [JsonProperty("TuneName")]  public string TuneName  { get; set; } = "Braking";
        [JsonProperty("TuneGroup")] public int    TuneGroup { get; set; } = 12;
    }

    public class ProfileSettings
    {
        [JsonProperty("IpAddress")]   public string IpAddress   { get; set; } = "192.168.8.131";
        [JsonProperty("Port")]        public int    Port        { get; set; } = 8081;
        [JsonProperty("ProfileId")]   public int    ProfileId   { get; set; } = 276;
        [JsonProperty("ProfileName")] public string ProfileName { get; set; } = "My Profile";
    }

    public class StatusSettings
    {
        [JsonProperty("IpAddress")]  public string IpAddress  { get; set; } = "192.168.8.131";
        [JsonProperty("Port")]       public int    Port       { get; set; } = 8081;
        [JsonProperty("ProfileId")]  public int    ProfileId  { get; set; } = 276;
        [JsonProperty("TuneName")]   public string TuneName   { get; set; } = "Braking";
        [JsonProperty("TuneGroup")]  public int    TuneGroup  { get; set; } = 12;
        [JsonProperty("ParamLabel")] public string ParamLabel { get; set; } = "Gain";
        [JsonProperty("ParamIndex")] public int    ParamIndex { get; set; } = 0;
    }
}
