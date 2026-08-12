using System;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

internal sealed class RamFuryWindowsService : ServiceBase
{
    private RamFuryMonitor _monitor;
    private Task _worker;
    private StreamWriter _log;
    private CancellationTokenSource _serviceStop;

    public RamFuryWindowsService()
    {
        ServiceName = "RamFuryMonitor";
        CanStop = true;
        CanShutdown = true;
        AutoLog = false;
    }

    protected override void OnStart(string[] args)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RamFuryMonitor");
        Directory.CreateDirectory(dir);
        _log = new StreamWriter(Path.Combine(dir, "monitor.log"), true, Encoding.UTF8) { AutoFlush = true };
        Console.SetOut(_log);
        Console.SetError(_log);
        _serviceStop = new CancellationTokenSource();
        _worker = Task.Run(async () =>
        {
            while (!_serviceStop.IsCancellationRequested)
            {
                _monitor = new RamFuryMonitor(true);
                try { await _monitor.RunAsync(); }
                catch (Exception ex) { Console.Error.WriteLine("SERVICE_ERROR: " + ex); }
                finally { _monitor.Dispose(); _monitor = null; }
                if (!_serviceStop.IsCancellationRequested)
                {
                    try { await Task.Delay(TimeSpan.FromSeconds(10), _serviceStop.Token); }
                    catch (OperationCanceledException) { }
                }
            }
        });
    }

    protected override void OnStop()
    {
        if (_serviceStop != null) _serviceStop.Cancel();
        if (_monitor != null) _monitor.Stop();
        if (_worker != null) _worker.Wait(TimeSpan.FromSeconds(15));
        if (_monitor != null) _monitor.Dispose();
        if (_log != null) { _log.Flush(); _log.Dispose(); }
        if (_serviceStop != null) _serviceStop.Dispose();
    }

    protected override void OnShutdown() { OnStop(); base.OnShutdown(); }
}
