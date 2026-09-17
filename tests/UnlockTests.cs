using BD2Fishing;
static class UnlockTests
{
 public static int Run()
 {
  int count=0;void Check(bool value,string message){count++;if(!value)throw new Exception(message);}
  var now=DateTime.UtcNow.Ticks;var p=new FishingUnlockProgress();p.Begin(42,now);
  p.Observe(false,false,0,true,false,now);
  Check(p.Pending&&!p.Completed,"unlock local state alone is not confirmation");
  bool rejected=false;try{p.Begin(42,now);}catch(InvalidOperationException){rejected=true;}
  Check(rejected,"one unlock in flight");
  p.Observe(true,true,42,true,false,now);
  Check(!p.Pending&&p.Completed&&p.UnlockedCount==1,"accepted unlock with readback completes");
  p.Observe(true,true,42,true,false,now);Check(p.UnlockedCount==1,"duplicate unlock reply not counted");
  foreach(var kind in new[]{"rejected","wrong-id","missing","still-locked"})
  {
   p=new();p.Begin(42,now);p.Observe(true,kind!="rejected",kind=="wrong-id"?43:42,kind!="missing",kind=="still-locked",now);
   Check(!p.Completed&&p.Error.Length>0,"unlock fails closed: "+kind);
  }
  p=new();p.Begin(42,now);p.Observe(false,false,0,true,true,now+TimeSpan.FromSeconds(31).Ticks);p.AcknowledgeError();
  Check(p.Pending&&p.Error.Length>0,"timeout cannot be rearmed while unknown");
  p.Observe(true,true,42,true,false,now+TimeSpan.FromSeconds(32).Ticks);
  Check(!p.Pending&&p.Error.Length>0&&p.UnlockedCount==1,"late success observed but timeout remains paused");
  p.AcknowledgeError();Check(p.Error=="","resolved unlock can explicitly acknowledge");
  var command=new FishingControl{OwnerId="one",ProcessId=12,Enabled=true,AutoSell=true,UntilUtcTicks=now+TimeSpan.FromSeconds(10).Ticks,Retention=new(){KeepLocked=false}};
  var authorization=new FishingSaleAuthorization(command);
  Check(authorization.Valid(command,now,12),"captured sale authorization valid");
  command.Retention.KeepLocked=true;Check(!authorization.Valid(command,now,12),"retention change revokes queued unlock and sale");
  command.Retention.KeepLocked=false;command.AutoSell=false;Check(!authorization.Valid(command,now,12),"auto sale off revokes queued unlock and sale");
  command.AutoSell=true;command.Enabled=false;Check(!authorization.Valid(command,now,12),"stop revokes queued unlock and sale");
  command.Enabled=true;command.OwnerId="two";Check(!authorization.Valid(command,now,12),"restart cannot inherit old batch");
  var capturedOwner=typeof(FishingSaleAuthorization).GetProperty("OwnerId");
  Check(capturedOwner!=null && !capturedOwner.CanWrite,"authorization exposes a read-only captured owner");
  Check((string?)capturedOwner!.GetValue(authorization)=="one","cancellation belongs to old authorization, not replacement command owner");
  var restartedAuthorization=new FishingSaleAuthorization(command);
  Check(restartedAuthorization.Valid(command,now,12) && (string?)capturedOwner.GetValue(restartedAuthorization)=="two","restart can establish its own fresh authorization");
  command.OwnerId="one";Check(!authorization.Valid(command,now+TimeSpan.FromSeconds(11).Ticks,12),"expired lease cannot continue batch");
  var data=Path.Combine(AppContext.BaseDirectory,"test-data","atomic-retention-"+Guid.NewGuid().ToString("N"));
  using(var link=new FishingControlLink(data))
  {
   link.Configure(new FishingSettings{AutoSell=false,Retention=new FishingRetentionOptions{KeepLocked=true}});link.Start(12);
   var before=FishingJson.Read<FishingControl>(Path.Combine(data,"control.json"))!;
   bool invalidRejected=false;
   try{link.Configure(new FishingSettings{AutoSell=true,Retention=new FishingRetentionOptions{KeepLocked=false,SizeRules=new[]{new FishingSizeRule{FishId=0,Mode=FishingSizeMode.Maximum}}}});}catch(ArgumentException){invalidRejected=true;}
   Check(invalidRejected,"invalid retention configuration is rejected");
   typeof(FishingControlLink).GetMethod("Pulse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(link,null);
   var after=FishingJson.Read<FishingControl>(Path.Combine(data,"control.json"))!;
   Check(after.AutoSell==before.AutoSell && after.Retention.Fingerprint()==before.Retention.Fingerprint(),"heartbeat preserves complete previous configuration after invalid rules");
  }
  var migrated=System.Text.Json.JsonSerializer.Deserialize<FishingSettings>("{\"KeepLockedOnly\":true,\"AutoSell\":false}")!;
  Check(migrated.Retention.KeepLegendary==false && migrated.Retention.KeepLocked==true && migrated.Retention.KeepUnknown==true && !migrated.AutoSell,"0.3.2 locked-only preference migrates without unlocking fish");
  return count;
 }
}
