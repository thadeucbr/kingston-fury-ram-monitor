using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

internal sealed class MonitorSettings
{
    public bool Enabled = true;
    public string Palette = "traffic";
    public int Brightness = -1;
    public string Color;

    public static MonitorSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new MonitorSettings();
            var serializer = new JavaScriptSerializer();
            var data = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var result = new MonitorSettings();
            object enabled;
            object palette;
            object brightness;
            object color;
            if (data.TryGetValue("enabled", out enabled)) result.Enabled = Convert.ToBoolean(enabled);
            if (data.TryGetValue("palette", out palette)) result.Palette = Convert.ToString(palette);
            if (data.TryGetValue("brightness", out brightness)) result.Brightness = Convert.ToInt32(brightness);
            if (data.TryGetValue("color", out color)) result.Color = Convert.ToString(color);
            return result;
        }
        catch { return new MonitorSettings(); }
    }
}

