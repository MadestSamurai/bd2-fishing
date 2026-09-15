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
    public FishingConnection(string? root=null){this.root=root??FishingIdentity.DataRoot;}
    public static bool SameProcess(FishingConnectionState s,int pid,long start)=>s.ProcessId==pid&&s.ProcessStartTicks==start;
    public static bool Fresh(FishingRuntimeStatus? status,int pid,DateTime since)=>status!=null&&status.ProcessId==pid&&status.Runtime==FishingIdentity.RuntimeName&&DateTime.TryParse(status.AtUtc,null,DateTimeStyles.RoundtripKind,out var when)&&when>=since&&when<=DateTime.UtcNow.AddSeconds(2);
    public string Connect(Action<string>? progress=null)
    {
        using var game=FindGame();int pid=game.Id;long start=game.StartTime.ToUniversalTime().Ticks;
        string fingerprint=HookCompiler.ToolFingerprint;var path=Path.Combine(root,"connection.json");
        var old=FishingJson.Read<FishingConnectionState>(path);
        if(old!=null&&SameProcess(old,pid,start))
        {
            if(old.ToolFingerprint!=fingerprint)throw new InvalidOperationException("游戏内已加载另一版钓鱼组件，请正常重启游戏后再连接。");
            var report=FishingJson.Read<FishingRuntimeStatus>(Path.Combine(root,"runtime.json"));
            if(Fresh(report,pid,DateTime.UtcNow.AddSeconds(-5))&&report!.State=="active")return $"已连接游戏 {pid} · 钓鱼独立组件";
            if(report?.ProcessId==pid&&report.State=="error")throw new InvalidOperationException(report.Error);
            throw new InvalidOperationException("此游戏进程已有连接记录，但组件未就绪。请等待游戏加载；仍无状态时正常重启游戏后再连接。");
        }
        // Resolve the required interfaces and compile against installed metadata before any injection.
        var exe=game.MainModule?.FileName??throw new InvalidOperationException("无法读取游戏路径，请使用与游戏相同的权限运行。");
        var client=Path.Combine(Path.GetDirectoryName(exe)!,Path.GetFileNameWithoutExtension(exe)+"_Data","Managed","Assembly-CSharp.dll");
        progress?.Invoke("正在识别钓鱼接口并生成适配组件，首次连接可能需要数秒…");
        PreparedHook prepared;
        try { prepared=HookCompiler.Prepare(Path.GetDirectoryName(client)!);FishingJson.Write(Path.Combine(root,"compatibility.json"),prepared.Report); }
        catch(CompatibilityException ex) { FishingJson.Write(Path.Combine(root,"compatibility.json"),ex.Report);throw; }
        catch(Exception ex) { FishingJson.Write(Path.Combine(root,"compatibility.json"),new{Status="unsupported",Error=ex.Message,Injection=false});throw; }
        var payload=prepared.Payload;string sha=Convert.ToHexString(SHA256.HashData(payload));
        if(game.HasExited || game.StartTime.ToUniversalTime().Ticks!=start)throw new InvalidOperationException("游戏进程已变化，请重新连接。");
        progress?.Invoke("接口检查通过，正在连接独立钓鱼组件…");
        var state=new FishingConnectionState{ProcessId=pid,ProcessStartTicks=start,HookSha256=sha,ToolFingerprint=fingerprint};
        using var injector=new Injector(pid);
        FishingJson.Write(path,state); // In-flight marker prevents a blind duplicate load after an ambiguous injector failure.
        var attempt=DateTime.UtcNow;
        state.Address=injector.Inject(payload,"BD2Fishing.Runtime","Loader","Load").ToInt64();
        FishingJson.Write(path,state);
        for(int i=0;i<50;i++)
        {
            var report=FishingJson.Read<FishingRuntimeStatus>(Path.Combine(root,"runtime.json"));
            if(Fresh(report,pid,attempt))
            {
                if(report!.State=="error")throw new InvalidOperationException(report.Error);
                if(report.State=="active")return $"已连接游戏 {pid} · 钓鱼独立组件";
            }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException("组件尚未返回连接状态，请等待游戏完成加载。请勿反复注入；必要时正常重启游戏。");
    }
    public static Process FindGame()
    {
        var games=new List<Process>();
        foreach(var p in Process.GetProcesses())try{if(FishingIdentity.IsGameProcessName(p.ProcessName))games.Add(p);else p.Dispose();}catch{p.Dispose();}
        if(games.Count==1)return games[0];foreach(var p in games)p.Dispose();
        throw new InvalidOperationException(games.Count==0?"请先启动 BrownDust II，再点击连接游戏。":"检测到多个游戏实例，请只保留需要操作的一个。");
    }
}
