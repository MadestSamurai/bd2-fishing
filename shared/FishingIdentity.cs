using System;
namespace BD2Fishing
{
    public static partial class FishingIdentity
    {
        public const string RuntimeName = "BD2Fishing.Runtime10";
        public static string DataRoot => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BD2Fishing");
        public static bool IsGameProcessName(string name) => string.Equals(name,"BrownDust II",StringComparison.OrdinalIgnoreCase) || string.Equals(name,"BrownDust II.exe",StringComparison.OrdinalIgnoreCase);
    }
    public sealed class FishingRuntimeStatus
    {
        public string State {get;set;} = "";
        public string Error {get;set;} = "";
        public string Runtime {get;set;} = "";
        public string AtUtc {get;set;} = "";
        public int ProcessId {get;set;}
    }
    public sealed class FishingControl
    {
        public string OwnerId {get;set;} = "";
        public int ProcessId {get;set;}
        public long UntilUtcTicks {get;set;}
        public bool Enabled {get;set;}
        public int NextCastMilliseconds {get;set;} = 1000;
        public double CastGauge {get;set;} = 0.9;
        public bool PreferWeak {get;set;} = true;
        public bool AutoSell {get;set;}
        public FishingRetentionOptions Retention {get;set;} = new FishingRetentionOptions();
        public bool AutoApproach {get;set;}
        public bool AutoBait {get;set;}
        public bool AutoMapRenewal {get;set;}
        public bool Valid(long now, int pid) => Enabled && !string.IsNullOrEmpty(OwnerId) && ProcessId == pid && UntilUtcTicks > now && UntilUtcTicks <= now + TimeSpan.FromSeconds(15).Ticks && ValidSettings(NextCastMilliseconds, CastGauge);
        public static bool ValidSettings(int delay, double gauge) => delay >= 0 && delay <= 60000 && !double.IsNaN(gauge) && gauge >= .05 && gauge <= .95;
    }
    public sealed class FishingSnapshot
    {
        public int Schema {get;set;} = 1;
        public string Runtime {get;set;} = FishingIdentity.RuntimeName;
        public int ProcessId {get;set;}
        public long CapturedUtcTicks {get;set;}
        public string Scene {get;set;} = "";
        public string State {get;set;} = "Unavailable";
        public bool Ready {get;set;}
        public bool CanCast {get;set;}
        public bool BagFull {get;set;}
        public int BagCount {get;set;}
        public int BagCapacity {get;set;}
        public int SellableCount {get;set;}
        public int ProtectedFishCount {get;set;}
        public bool SaleReady {get;set;}
        public bool SalePending {get;set;}
        public int SoldCount {get;set;}
        public string SaleStatus {get;set;} = "尚未自动出售";
        public long SaleReplySerial {get;set;}
        public bool SaleReplyAccepted {get;set;}
        public long UnlockReplySerial {get;set;}
        public long UnlockReplyIndex {get;set;}
        public bool UnlockReplyAccepted {get;set;}
        public int UnlockedCount {get;set;}
        public FishingSpeciesSummary[] FishSpecies {get;set;} = new FishingSpeciesSummary[0];
        public bool BaitReady {get;set;}
        public bool BaitCanUse {get;set;}
        public int BaitCount {get;set;}
        public bool BaitActive {get;set;}
        public double BaitRemainingSeconds {get;set;}
        public bool BaitPending {get;set;}
        public int BaitUsedCount {get;set;}
        public string BaitStatus {get;set;} = "鱼饵：尚未读取";
        public long BaitReplySerial {get;set;}
        public bool BaitReplyAccepted {get;set;}
        // Pending waits for the current fishing cycle to reach None; Busy means active scene switching.
        public bool MapChangePending {get;set;}
        public bool Busy {get;set;}
        public int MapGroupId {get;set;} = -1;
        public bool MapTravelBusy {get;set;}
        public bool LobbyReady {get;set;}
        public bool MapUnlocked {get;set;}
        public long RoomStartTicks {get;set;}
        public double RoomDurationSeconds {get;set;}
        public double RoomRemainingSeconds {get;set;}
        public bool RoomTimerKnown {get;set;}
        public string MapRenewalStatus {get;set;} = "地图倒计时：尚未读取";
        public int MapRenewals {get;set;}
        public string ApproachStatus {get;set;} = "";
        public string UiLayers {get;set;} = "";
        public string BlockReason {get;set;} = "";
        public string Error {get;set;} = "";
        public bool CastRunning {get;set;}
        public double Gauge {get;set;}
        public int CastGrade {get;set;}
        public bool ResultPopup {get;set;}
        public bool LevelPopup {get;set;}
        public bool CanClosePopup {get;set;}
        public int PopupId {get;set;}
        public int FishId {get;set;}
        public double FishHp {get;set;}
        public double TimeRemaining {get;set;}
        public bool NormalHit {get;set;}
        public bool WeakHit {get;set;}
        public double NormalWidth {get;set;}
        public double WeakWidth {get;set;}
        public double NeedlePosition {get;set;}
        public double WeakPosition {get;set;}
        public double NeedleVelocity {get;set;}
        public double ShrinkSpeed {get;set;}
        public bool BlockedHit {get;set;}
        public bool Freeze {get;set;}
        public string Interaction {get;set;} = "";
        public int TargetId {get;set;}
        public bool HoldActive {get;set;}
        public bool HoldCompleted {get;set;}
        public bool HoldTracking {get;set;}
        public bool HoldStartHit {get;set;}
        public bool HoldEndHit {get;set;}
        public bool HoldTargetHit {get;set;}
        public bool HoldInside {get;set;}
        public bool NetworkPending {get;set;}
        public double NetworkWaitSeconds {get;set;}
        public string Network {get;set;} = "尚无钓鱼请求";
        public int Catches {get;set;}
        public bool Enabled {get;set;}
        public string OwnerId {get;set;} = "";
        public string Reason {get;set;} = "等待连接";
        public string LastAction {get;set;} = "";
        public long ActionCount {get;set;}
    }
    public enum FishingAction { None, CastPress, CastRelease, Hook, FightClick, HoldPress, HoldRelease, ClosePopup, SellFish, UseBait, TravelLobby, TravelReturn, ApproachWater }
}
