using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class RamFurySetup
{
    private const string ServiceName = "RamFuryMonitor";
    private const string InstallDir = @"C:\ProgramData\RamFuryMonitor";
    private static Label _status;
    private static Button _install;
    private static Button _uninstall;

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
    }

    private sealed class SetupForm : Form
    {
        public SetupForm()
        {
            Text = "RAM FURY  /  SETUP";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 500);
            BackColor = Color.FromArgb(18, 20, 25);
            ForeColor = Color.White;

            var banner = new PictureBox { Location = new Point(0, 0), Size = new Size(760, 250), SizeMode = PictureBoxSizeMode.StretchImage, BackColor = Color.FromArgb(25, 28, 35) };
            string bannerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ram-fury-installer-banner.png");
            if (File.Exists(bannerPath)) banner.Image = Image.FromFile(bannerPath);
            Controls.Add(banner);

            Controls.Add(Label("RAM FURY MONITOR", 34, 275, 400, 34, 20, FontStyle.Bold, Color.White));
            Controls.Add(Label("Monitoramento de RAM e controle FURY RGB, sem controlador externo.", 36, 313, 650, 24, 10, FontStyle.Regular, Color.FromArgb(170, 175, 185)));
            Controls.Add(Label("INSTALAÇÃO", 36, 357, 120, 18, 9, FontStyle.Bold, Color.FromArgb(210, 170, 70)));
            _status = Label("Verificando bundle...", 36, 382, 650, 22, 10, FontStyle.Regular, Color.FromArgb(190, 195, 205));
            Controls.Add(_status);

            _install = Button("Instalar / atualizar", 36, 430, 170);
            _install.Click += (s, e) => Install();
            Controls.Add(_install);
            _uninstall = Button("Desinstalar", 216, 430, 120);
            _uninstall.Click += (s, e) => Uninstall();
            Controls.Add(_uninstall);
            var close = Button("Fechar", 620, 430, 100);
            close.Click += (s, e) => Close();
            Controls.Add(close);
            Shown += (s, e) => RefreshStatus();
        }

        private void RefreshStatus()
        {
            string[] required = { "RamFuryMonitor.exe", "RamFuryTray.exe", "ram-fury.ico" };
            bool files = true;
            foreach (string file in required) if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file))) files = false;
            bool fury = IsPortOpen("127.0.0.1", 55599);
            _status.Text = files
                ? (fury ? "Bundle pronto  •  FURY CTRL detectado  •  instalação segura" : "Bundle pronto  •  FURY CTRL não detectado agora")
                : "Bundle incompleto  •  execute o pacote gerado pelo build";
            _status.ForeColor = files ? Color.FromArgb(120, 220, 150) : Color.FromArgb(245, 130, 110);
            _install.Enabled = files;
        }

        private void Install()
        {
            try
            {
                Directory.CreateDirectory(InstallDir);
                StopService();
                CopyPayload("RamFuryMonitor.exe"); CopyPayload("RamFuryTray.exe"); CopyPayload("ram-fury.ico"); CopyPayload("ram-fury-installer-banner.png");
                string config = Path.Combine(InstallDir, "config.json");
                if (!File.Exists(config)) File.WriteAllText(config, "{\"enabled\":true,\"palette\":\"traffic\",\"brightness\":-1,\"color\":\"#8C5000\",\"savedPalettes\":[]}");
                Sc("delete " + ServiceName, false);
                Sc("create " + ServiceName + " binPath= \"" + Path.Combine(InstallDir, "RamFuryMonitor.exe") + " --service\" start= auto DisplayName= \"RAM FURY Monitor\"", true);
                Sc("description " + ServiceName + " \"RAM FURY Monitor service\"", false);
                Sc("failure " + ServiceName + " reset= 86400 actions= restart/10000/restart/30000/restart/60000", false);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run", "RamFuryTray", Path.Combine(InstallDir, "RamFuryTray.exe"));
                Sc("start " + ServiceName, false);
                _status.Text = "Instalação concluída  •  serviço iniciado  •  configuração preservada";
                _status.ForeColor = Color.FromArgb(120, 220, 150);
                MessageBox.Show("RAM FURY Monitor foi instalado com sucesso.", "RAM FURY", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { _status.Text = "Falha: " + ex.Message; _status.ForeColor = Color.FromArgb(245, 130, 110); }
        }

        private void Uninstall()
        {
            if (MessageBox.Show("Remover o serviço e a inicialização automática?\nA configuração e os logs serão preservados.", "RAM FURY", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                StopService(); Sc("delete " + ServiceName, false);
                RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (runKey != null) { runKey.DeleteValue("RamFuryTray", false); runKey.Close(); }
                _status.Text = "Serviço removido  •  configuração e logs preservados";
                _status.ForeColor = Color.FromArgb(210, 170, 70);
            }
            catch (Exception ex) { _status.Text = "Falha: " + ex.Message; _status.ForeColor = Color.FromArgb(245, 130, 110); }
        }

        private static void CopyPayload(string file) { File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file), Path.Combine(InstallDir, file), true); }
        private static void StopService() { Sc("stop " + ServiceName, false); System.Threading.Thread.Sleep(1500); }
        private static void Sc(string arguments, bool required) { using (var p = Process.Start(new ProcessStartInfo("sc.exe", arguments) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })) { p.WaitForExit(); if (required && p.ExitCode != 0) throw new InvalidOperationException(p.StandardError.ReadToEnd()); } }
        private static bool IsPortOpen(string host, int port) { try { using (var client = new TcpClient()) { var task = client.ConnectAsync(host, port); return task.Wait(300); } } catch { return false; } }
        private static Label Label(string text, int x, int y, int w, int h, float size, FontStyle style, Color color) { return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h), ForeColor = color, Font = new Font("Segoe UI", size, style) }; }
        private static Button Button(string text, int x, int y, int w) { return new Button { Text = text, Location = new Point(x, y), Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(43, 47, 56), ForeColor = Color.White }; }
    }
}
