using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal static class RamFuryTray
{
    private const string ConfigPath = @"C:\ProgramData\RamFuryMonitor\config.json";
    private static NotifyIcon _icon;
    private static ToolStripMenuItem _enabled;
    private static string _palette = "traffic";
    private static string _color = "#8C5000";
    private static int _brightness = -1;
    private static readonly List<SavedPalette> Saved = new List<SavedPalette>();

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        LoadConfig();
        var menu = new ContextMenuStrip();
        _enabled = new ToolStripMenuItem("Efeito ativado") { CheckOnClick = true, Checked = IsEnabled() };
        _enabled.Click += (s, e) => SaveConfig();
        menu.Items.Add(_enabled);
        var open = new ToolStripMenuItem("Abrir painel de controle");
        open.Click += (s, e) => ShowSettings();
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());
        var log = new ToolStripMenuItem("Abrir pasta de logs");
        log.Click += (s, e) => { Directory.CreateDirectory(@"C:\ProgramData\RamFuryMonitor"); Process.Start("explorer.exe", @"C:\ProgramData\RamFuryMonitor"); };
        menu.Items.Add(log);
        var exit = new ToolStripMenuItem("Fechar painel");
        exit.Click += (s, e) => { _icon.Visible = false; Application.Exit(); };
        menu.Items.Add(exit);
        _icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "RAM FURY Monitor", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (s, e) => ShowSettings();
        Application.Run();
    }

    private static bool IsEnabled() { return _enabled == null || _enabled.Checked; }

    private static void ShowSettings()
    {
        using (var form = new ControlPanel(_brightness, _palette, _color, IsEnabled(), Saved))
        {
            if (form.ShowDialog() != DialogResult.OK) return;
            _brightness = form.Brightness;
            _palette = form.Palette;
            _color = form.HexColor;
            _enabled.Checked = form.EffectEnabled;
            Saved.Clear(); Saved.AddRange(form.Palettes);
            SaveConfig();
        }
    }

    private static void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(ConfigPath));
            object value;
            if (data.TryGetValue("palette", out value)) _palette = Convert.ToString(value);
            if (data.TryGetValue("color", out value)) _color = Convert.ToString(value);
            if (data.TryGetValue("brightness", out value)) _brightness = Convert.ToInt32(value);
            object rawSaved;
            if (data.TryGetValue("savedPalettes", out rawSaved))
            {
                var list = rawSaved as object[];
                if (list != null) foreach (var item in list)
                {
                    var p = item as Dictionary<string, object>;
                    if (p != null && p.ContainsKey("name") && p.ContainsKey("color")) Saved.Add(new SavedPalette(Convert.ToString(p["name"]), Convert.ToString(p["color"])));
                }
            }
        }
        catch { }
    }

    private static void SaveConfig()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        var data = new Dictionary<string, object>();
        data["enabled"] = IsEnabled(); data["palette"] = _palette; data["color"] = _color; data["brightness"] = _brightness;
        var saved = new List<object>();
        foreach (var p in Saved) saved.Add(new Dictionary<string, object> { { "name", p.Name }, { "color", p.Hex } });
        data["savedPalettes"] = saved;
        File.WriteAllText(ConfigPath, new JavaScriptSerializer().Serialize(data));
    }

    private sealed class SavedPalette
    {
        public string Name; public string Hex;
        public SavedPalette(string name, string hex) { Name = name; Hex = hex; }
        public override string ToString() { return Name + "  " + Hex; }
    }

    private sealed class ControlPanel : Form
    {
        private readonly TrackBar _hue, _sat, _value, _brightness;
        private readonly TextBox _hex, _red, _green, _blue, _name;
        private readonly Panel _preview;
        private readonly CheckBox _enabled, _original;
        private readonly ComboBox _presets;
        private readonly List<SavedPalette> _palettes;
        private bool _updating;
        public int Brightness { get { return _original.Checked ? -1 : _brightness.Value; } }
        public string Palette { get { var selected = _presets.SelectedItem as Preset; return selected == null ? "custom" : selected.Value; } }
        public string HexColor { get { return NormalizeHex(_hex.Text); } }
        public bool EffectEnabled { get { return _enabled.Checked; } }
        public List<SavedPalette> Palettes { get { return _palettes; } }

        public ControlPanel(int brightness, string palette, string color, bool enabled, List<SavedPalette> saved)
        {
            _palettes = new List<SavedPalette>(saved);
            Text = "RAM FURY  /  CONTROL CENTER"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(520, 520); BackColor = Color.FromArgb(20, 22, 27); ForeColor = Color.White;
            var title = Label("RAM FURY MONITOR", 24, 20, 300, 28, 16, FontStyle.Bold); Controls.Add(title);
            Controls.Add(Label("Controle local de iluminação  •  sem controlador externo", 24, 50, 450, 22, 9, FontStyle.Regular, Color.FromArgb(155, 160, 170)));
            _enabled = new CheckBox { Text = "Efeito ativo", Checked = enabled, Location = new Point(24, 88), AutoSize = true, ForeColor = Color.White }; Controls.Add(_enabled);
            Controls.Add(Label("BRILHO", 24, 125, 100, 18, 9, FontStyle.Bold, Color.FromArgb(210, 170, 70)));
            _brightness = Slider(24, 145, 420, 0, 100, brightness < 0 ? 100 : brightness); Controls.Add(_brightness);
            _original = new CheckBox { Text = "Usar brilho original", Checked = brightness < 0, Location = new Point(24, 180), AutoSize = true, ForeColor = Color.FromArgb(210, 215, 225) }; _original.CheckedChanged += (s, e) => _brightness.Enabled = !_original.Checked; Controls.Add(_original);
            Controls.Add(Label("PALETA", 24, 218, 100, 18, 9, FontStyle.Bold, Color.FromArgb(210, 170, 70)));
            _presets = new ComboBox { Location = new Point(24, 240), Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(35, 38, 45), ForeColor = Color.White, DisplayMember = "Name", ValueMember = "Value" };
            var presets = new List<object> { new Preset("Traffic  /  verde → vermelho", "traffic"), new Preset("Verde fixa", "green"), new Preset("Azul fixa", "blue"), new Preset("Roxo fixa", "purple"), new Preset("Old Gold", "oldgold"), new Preset("Personalizada", "custom") }; foreach (var p in _palettes) presets.Add(p); _presets.DataSource = presets; _presets.SelectedValue = palette; _presets.SelectedIndexChanged += (s, e) => { if (!_updating && _presets.SelectedItem is SavedPalette) SetHex(((SavedPalette)_presets.SelectedItem).Hex); }; Controls.Add(_presets);
            _preview = new Panel { Location = new Point(350, 235), Size = new Size(94, 38), BackColor = ParseColor(color), BorderStyle = BorderStyle.FixedSingle }; Controls.Add(_preview);
            Controls.Add(Label("EDITOR DE COR  /  HSV + RGB + HEX", 24, 285, 300, 18, 9, FontStyle.Bold, Color.FromArgb(210, 170, 70)));
            _hue = Slider(24, 310, 300, 0, 360, 30); _sat = Slider(24, 345, 300, 0, 100, 100); _value = Slider(24, 380, 300, 0, 100, 55); Controls.Add(_hue); Controls.Add(_sat); Controls.Add(_value);
            Controls.Add(Label("HUE", 335, 304, 40, 18, 8, FontStyle.Bold, Color.FromArgb(155, 160, 170))); Controls.Add(Label("SAT", 335, 339, 40, 18, 8, FontStyle.Bold, Color.FromArgb(155, 160, 170))); Controls.Add(Label("VAL", 335, 374, 40, 18, 8, FontStyle.Bold, Color.FromArgb(155, 160, 170)));
            _red = Field("R", 24, 420); _green = Field("G", 120, 420); _blue = Field("B", 216, 420); _hex = new TextBox { Text = NormalizeHex(color), Location = new Point(315, 420), Width = 130, BackColor = Color.FromArgb(35, 38, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle }; Controls.Add(_hex); Controls.Add(Label("HEX", 315, 399, 50, 18, 8, FontStyle.Bold, Color.FromArgb(155, 160, 170)));
            foreach (var t in new[] { _hue, _sat, _value }) t.Scroll += (s, e) => { if (!_updating) UpdateFromHsv(); }; foreach (var t in new[] { _red, _green, _blue }) t.TextChanged += (s, e) => { if (!_updating) UpdateFromRgb(); }; _hex.TextChanged += (s, e) => { if (!_updating) UpdateFromHex(); };
            var saveName = Label("Nome", 24, 466, 45, 18, 8, FontStyle.Bold, Color.FromArgb(155, 160, 170)); Controls.Add(saveName); _name = new TextBox { Location = new Point(70, 462), Width = 170, BackColor = Color.FromArgb(35, 38, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle }; Controls.Add(_name);
            var savePalette = Button("Salvar paleta", 250, 458, 110); savePalette.Click += (s, e) => { string n = _name.Text.Trim(); if (n.Length > 0) { _palettes.RemoveAll(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase)); _palettes.Add(new SavedPalette(n, HexColor)); MessageBox.Show("Paleta salva.", "RAM FURY", MessageBoxButtons.OK, MessageBoxIcon.Information); } }; Controls.Add(savePalette);
            var cancel = Button("Cancelar", 365, 458, 75); cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); var apply = Button("Aplicar", 445, 458, 60); apply.DialogResult = DialogResult.OK; Controls.Add(apply); AcceptButton = apply; CancelButton = cancel;
            SetHex(color); _original.Checked = brightness < 0;
        }

        private void SetHex(string value) { _updating = true; _hex.Text = NormalizeHex(value); var c = ParseColor(_hex.Text); _preview.BackColor = c; _red.Text = c.R.ToString(); _green.Text = c.G.ToString(); _blue.Text = c.B.ToString(); int h, s, v; RgbToHsv(c, out h, out s, out v); _hue.Value = Math.Max(0, Math.Min(360, h)); _sat.Value = s; _value.Value = v; _updating = false; }
        private void UpdateFromHsv() { _updating = true; Color c = HsvToRgb(_hue.Value, _sat.Value, _value.Value); _hex.Text = ToHex(c); _preview.BackColor = c; _red.Text = c.R.ToString(); _green.Text = c.G.ToString(); _blue.Text = c.B.ToString(); _updating = false; }
        private void UpdateFromRgb() { int r, g, b; if (Int32.TryParse(_red.Text, out r) && Int32.TryParse(_green.Text, out g) && Int32.TryParse(_blue.Text, out b)) SetHex(ToHex(Color.FromArgb(Clamp(r), Clamp(g), Clamp(b)))); }
        private void UpdateFromHex() { string raw = (_hex.Text ?? "").Trim(); if (!raw.StartsWith("#")) raw = "#" + raw; if (raw.Length == 7) { try { ColorTranslator.FromHtml(raw); SetHex(raw); } catch { } } }
        private static int Clamp(int x) { return Math.Max(0, Math.Min(255, x)); }
        private static string NormalizeHex(string value) { string h = (value ?? "").Trim(); if (!h.StartsWith("#")) h = "#" + h; return h.Length == 7 ? h.ToUpperInvariant() : "#8C5000"; }
        private static Color ParseColor(string value) { try { return ColorTranslator.FromHtml(NormalizeHex(value)); } catch { return Color.FromArgb(140, 80, 0); } }
        private static string ToHex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }
        private static Label Label(string text, int x, int y, int w, int h, float size, FontStyle style, Color? color = null) { return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = color ?? Color.White, Font = new Font("Segoe UI", size, style) }; }
        private static TextBox Field(string prefix, int x, int y) { var t = new TextBox { Text = "0", Location = new Point(x, y), Width = 72, BackColor = Color.FromArgb(35, 38, 45), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle }; return t; }
        private static TrackBar Slider(int x, int y, int w, int min, int max, int value) { return new TrackBar { Location = new Point(x, y), Width = w, Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), TickFrequency = max > 100 ? 30 : 10, BackColor = Color.FromArgb(20, 22, 27) }; }
        private static Button Button(string text, int x, int y, int w) { return new Button { Text = text, Location = new Point(x, y), Width = w, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 47, 56), ForeColor = Color.White }; }
        private static void RgbToHsv(Color c, out int h, out int s, out int v) { double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0,max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),d=max-min; double hh=0; if(d!=0){if(max==r)hh=60*((g-b)/d%6);else if(max==g)hh=60*((b-r)/d+2);else hh=60*((r-g)/d+4);} if(hh<0)hh+=360; h=(int)Math.Round(hh);s=(int)Math.Round(max==0?0:d/max*100);v=(int)Math.Round(max*100); }
        private static Color HsvToRgb(int h, int s, int v) { double hd=h/60.0, c=v/100.0*(s/100.0), x=c*(1-Math.Abs(hd%2-1)), m=v/100.0-c; double r=0,g=0,b=0; if(hd<1){r=c;g=x;}else if(hd<2){r=x;g=c;}else if(hd<3){g=c;b=x;}else if(hd<4){g=x;b=c;}else if(hd<5){r=x;b=c;}else{r=c;b=x;} return Color.FromArgb(Clamp((int)Math.Round((r+m)*255)),Clamp((int)Math.Round((g+m)*255)),Clamp((int)Math.Round((b+m)*255))); }
        private sealed class Preset { public string Name; public string Value; public Preset(string n,string v){Name=n;Value=v;} public override string ToString(){return Name;} }
    }
}
