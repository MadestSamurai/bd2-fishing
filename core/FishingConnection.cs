using BD2Fishing.Compatibility;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using SharpMonoInjector;
namespace BD2Fishing;
public sealed class FishingConnectionState
{
    public int ProcessId {get;set;}
    public long ProcessStartTicks {get;set;}
    public string HookSha256 {get;set;}="";
    public string ToolFingerprint {get;set;}="";
    public long Address {get;set;}
}
public sealed class FishingConnection
{
    private readonly string root;
    public FishingConnection(string? root=null){this.root=root??FishingIdentity.DataRoot;BD2.LocalIpc.DesktopFiles.Configure(this.root,FishingIdentity.LiveEntries);}
    public static bool SameProcess(FishingConnectionState s,int pid,long start)=>s.ProcessId==pid&&s.ProcessStartTicks==start;
    public static bool Fresh(FishingRuntimeStatus? status,int pid,DateTime since)=>status!=null&&status.ProcessId==pid&&status.Runtime==FishingIdentity.RuntimeName&&DateTime.TryParse(status.AtUtc,null,DateTimeStyles.RoundtripKind,out var when)&&when>=since&&when<=DateTime.UtcNow.AddSeconds(2);
    public string Connect(Action<string>? progress=null,bool daily=false,CancellationToken cancellationToken=default)
    {
        using var trace=new FishingConnectionTrace(root,progress);
        try
        {
        cancellationToken.ThrowIfCancellationRequested();
        trace.Stage("process.find");
        using var game=FindGame();int pid=game.Id;long start=game.StartTime.ToUniversalTime().Ticks;
        string fingerprint=HookCompiler.ToolFingerprint+(daily?".daily":"");var path=Path.Combine(root,"connection.json");
        trace.Stage("process.found");
        FishingDiagnostics.Write(root,"process.identity",$"pid={pid}; startedUtcTicks={start}; tool64={Environment.Is64BitProcess}; os64={Environment.Is64BitOperatingSystem}");
        var pipe=BD2.LocalIpc.DesktopFiles.Connect(root,pid,start); if(BD2.LocalIpc.HostedConnection.TryOpen(pipe,pid,start))return "已使用日常助手的统一连接";
        trace.Stage("pipe.probe");
        try
        {
            if(pipe.Fingerprint()==fingerprint)
            {
                var report=FishingJson.Read<FishingRuntimeStatus>(Path.Combine(root,"runtime.json"));
                if(Fresh(report,pid,DateTime.UtcNow.AddSeconds(-5))&&report!.State=="active")
                {cancellationToken.ThrowIfCancellationRequested();pipe.Open(fingerprint);trace.Stage("connected.reused");return $"已连接游戏 {pid} · 本机管道";}
            }
        }
        catch(BD2.LocalIpc.LeaseRevokedException){}
        catch(TimeoutException){}
        catch(IOException){}
        cancellationToken.ThrowIfCancellationRequested();
        // Resolve the required interfaces and compile against installed metadata before any injection.
        var exe=game.MainModule?.FileName??throw new InvalidOperationException("无法读取游戏路径，请使用与游戏相同的权限运行。");
        var client=Path.Combine(Path.GetDirectoryName(exe)!,Path.GetFileNameWithoutExtension(exe)+"_Data","Managed","Assembly-CSharp.dll");
        trace.Stage("compatibility.prepare","正在识别钓鱼接口并生成适配组件，首次连接可能需要数秒…");
        PreparedHook prepared;
        try { prepared=HookCompiler.Prepare(Path.GetDirectoryName(client)!,daily:daily);FishingJson.Write(Path.Combine(root,"compatibility.json"),prepared.Report); }
        catch(CompatibilityException ex) { FishingJson.Write(Path.Combine(root,"compatibility.json"),ex.Report);throw; }
        catch(Exception ex) { FishingJson.Write(Path.Combine(root,"compatibility.json"),new{Status="unsupported",Error=ex.Message,Injection=false});throw; }
        cancellationToken.ThrowIfCancellationRequested();
        trace.Stage("compatibility.ready");
        var payload=prepared.Payload;string sha=Convert.ToHexString(SHA256.HashData(payload));
        if(game.HasExited || game.StartTime.ToUniversalTime().Ticks!=start)throw new InvalidOperationException("游戏进程已变化，请重新连接。");
        trace.Stage("injector.open","接口检查通过，正在连接独立钓鱼组件…");
        var state=new FishingConnectionState{ProcessId=pid,ProcessStartTicks=start,HookSha256=sha,ToolFingerprint=fingerprint};
        using var injector=new Injector(pid){DiagnosticStage=stage=>trace.Stage("injector."+stage)};
        FishingJson.Write(path,state); // In-flight marker prevents a blind duplicate load after an ambiguous injector failure.
        var attempt=DateTime.UtcNow;
        cancellationToken.ThrowIfCancellationRequested();
        trace.Stage("injector.invoke");
        state.Address=injector.Inject(payload,"BD2Fishing.Runtime","Loader","Load").ToInt64();
        trace.Stage("injector.returned");
        cancellationToken.ThrowIfCancellationRequested();
        FishingJson.Write(path,state);
        trace.Stage("handoff.wait");
        var deadline=DateTime.UtcNow.AddSeconds(35);
        while(DateTime.UtcNow<deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var report=FishingJson.Read<FishingRuntimeStatus>(Path.Combine(root,"runtime.json"));
            if(Fresh(report,pid,attempt))
            {
                if(report!.State=="error")throw new InvalidOperationException(report.Error);
                if(report.State=="active"){pipe.Open(fingerprint);trace.Stage("connected.ready");return $"已连接游戏 {pid} · 本机管道";}
            }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException("组件交接尚未完成，请等待游戏界面恢复或当前操作结算后重新连接；游戏可以保持运行。");
        }
        catch(Exception ex){trace.Fail(ex);throw;}
    }
    public static Process FindGame()
    {
        var games=new List<Process>();
        foreach(var p in Process.GetProcesses())try{if(FishingIdentity.IsGameProcessName(p.ProcessName))games.Add(p);else p.Dispose();}catch{p.Dispose();}
        if(games.Count==1)return games[0];foreach(var p in games)p.Dispose();
        throw new InvalidOperationException(games.Count==0?"请先启动 BrownDust II，再点击连接游戏。":"检测到多个游戏实例，请只保留需要操作的一个。");
    }
}
