using System;
namespace BD2Fishing {
 public enum ApproachDecision { Continue, Retry, Fail }
 public sealed class FishingApproach {
  public const int MaxAttempts=3;
  public int Attempts {get;private set;}
  long started,progressAt;double best;
  public void Reset(){Attempts=0;started=progressAt=0;best=0;}
  public void Begin(long now,double distance){Attempts++;started=progressAt=now;best=distance;}
  public ApproachDecision Observe(long now,double distance,bool arrived){
   if(distance<best-.15){best=distance;progressAt=now;}
   if(arrived&&now-progressAt>=TimeSpan.FromSeconds(1).Ticks || now-progressAt>=TimeSpan.FromSeconds(8).Ticks || now-started>=TimeSpan.FromSeconds(90).Ticks)
    return Attempts>=MaxAttempts?ApproachDecision.Fail:ApproachDecision.Retry;
   return ApproachDecision.Continue;
  }
  public static bool CanRun(FishingSnapshot s,FishingControl c,long now)=>c!=null&&c.Valid(now,s.ProcessId)&&c.AutoApproach&&s.Ready&&s.MapGroupId>0&&s.State=="None"&&!s.CanCast&&!s.Busy&&!s.MapTravelBusy&&!s.MapChangePending&&s.BlockReason.Length==0&&s.Error.Length==0&&!s.ResultPopup&&!s.LevelPopup&&!s.NetworkPending&&!s.SalePending&&!s.BaitPending;
 }
}
