using System;
using System.IO;
using System.Text.Json;
namespace BD2.LocalIpc {
 // Optional host contract. Standalone launches have no environment descriptor and keep their own connection.
 public static class HostedConnection {
  public static bool Enabled=>!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BD2_DAILY_HOSTED_TOOL"));
  public static bool TryOpen(PipeClient pipe,int pid,long start){
   if(!Enabled)return false;
   string tool=Environment.GetEnvironmentVariable("BD2_DAILY_HOSTED_TOOL")!;
   string owner=Environment.GetEnvironmentVariable("BD2_DAILY_SUITE_OWNER")!;
   string root=Environment.GetEnvironmentVariable("BD2_DAILY_DATA_ROOT")!;
   if(!int.TryParse(Environment.GetEnvironmentVariable("BD2_DAILY_GAME_PID"),out int expectedPid)||!long.TryParse(Environment.GetEnvironmentVariable("BD2_DAILY_GAME_START"),out long expectedStart)||pid!=expectedPid||start!=expectedStart)
    throw new InvalidOperationException("游戏会话已变化，请返回日常助手重新连接。");
   var control=new PipeClient(Path.Combine(root,"suite"),pid,start);
   using var state=JsonDocument.Parse(control.Read("status.json")??throw new IOException("统一会话未就绪。"));
   var s=state.RootElement;
   if(s.GetProperty("State").GetString()!="ready"||s.GetProperty("Tool").GetString()!=tool||s.GetProperty("Owner").GetString()!=owner||s.GetProperty("At").GetInt64()<DateTime.UtcNow.AddSeconds(-5).Ticks)
    throw new InvalidOperationException("此功能的统一会话已停止，请返回日常助手。");
   string fingerprint=Environment.GetEnvironmentVariable("BD2_DAILY_TOOL_FINGERPRINT")??"";
   if(string.IsNullOrEmpty(fingerprint)||pipe.Fingerprint()!=fingerprint)throw new InvalidOperationException("功能组件与统一会话不一致，请返回日常助手。");
   pipe.Open(fingerprint);return true;
  }
 }
}
