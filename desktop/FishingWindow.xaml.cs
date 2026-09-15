using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace BD2Fishing.Desktop;
public partial class FishingWindow:Window
{
 private readonly string root;private readonly FishingControlLink link;private readonly DispatcherTimer timer;
 private FishingSnapshot? snapshot;private bool initialized,connecting,closing,smoke;
 public FishingWindow(string? dataRoot=null)
 {
  root=dataRoot??FishingIdentity.DataRoot;link=new(root);InitializeComponent();Title="BD2 钓鱼 · "+typeof(FishingWindow).Assembly.GetName().Version!.ToString(3);
  var s=FishingJson.Read<FishingSettings>(Path.Combine(root,"settings.json"))??new();
  if(!FishingControl.ValidSettings(s.NextCastMilliseconds,s.CastGauge))s=new();
  IntervalBox.Text=s.NextCastMilliseconds.ToString();CastBox.Text=(s.CastGauge*100).ToString("0.#",CultureInfo.InvariantCulture);WeakBox.IsChecked=s.PreferWeak;AutoSellBox.IsChecked=s.AutoSell;AutoBaitBox.IsChecked=s.AutoBait;
  initialized=true;timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(_,_)=>Refresh();timer.Start();
 }
 private FishingSettings Settings()
 {
  if(!int.TryParse(IntervalBox.Text,out var delay)||!double.TryParse(CastBox.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var gauge)||!FishingControl.ValidSettings(delay,gauge/100))throw new InvalidOperationException("请输入 0–60000 毫秒的间隔，以及 5–95% 的蓄力。");
  return new(){NextCastMilliseconds=delay,CastGauge=gauge/100,PreferWeak=WeakBox.IsChecked==true,AutoSell=AutoSellBox.IsChecked==true,AutoBait=AutoBaitBox.IsChecked==true};
 }
 private void SettingsChanged(object sender,RoutedEventArgs e)
 {
  if(!initialized||closing)return;try{link.Configure(Settings());SettingsHint.Text="设置已保存；收线和长按仍按帧判断。";SettingsHint.Foreground=(Brush)FindResource("MutedBrush");}
  catch(Exception ex){SettingsHint.Text=ex.Message;SettingsHint.Foreground=Brushes.Firebrick;}
 }
 private async void ConnectClick(object sender,RoutedEventArgs e)
 {
  if(connecting)return;connecting=true;ConnectButton.IsEnabled=false;ReasonText.Text="正在连接钓鱼组件…";
  try{var result=await Task.Run(()=>new FishingConnection(root).Connect(message=>Dispatcher.InvokeAsync(()=>{if(!closing)ReasonText.Text=message;})));if(!closing)ReasonText.Text=result;}
  catch(Exception ex){if(!closing){StatusText.Text="连接未完成";ReasonText.Text=ex.GetBaseException().Message;}}
  finally{connecting=false;if(!closing)ConnectButton.IsEnabled=true;}
 }
 private void StartClick(object sender,RoutedEventArgs e)
 {
  try
  {
   if(!FishingControlLink.Fresh(snapshot,DateTime.UtcNow)||!snapshot!.Ready)throw new InvalidOperationException("请先连接游戏并进入钓点，等待实时状态。");
   if(!smoke){using var p=Process.GetProcessById(snapshot.ProcessId);if(!FishingIdentity.IsGameProcessName(p.ProcessName))throw new InvalidOperationException("游戏进程已变化，请重新连接。");}
   link.Configure(Settings());link.Start(snapshot.ProcessId);StopButton.IsEnabled=true;StartButton.IsEnabled=false;StatusText.Text="自动钓鱼已开启";
  }
  catch(Exception ex){ReasonText.Text=ex.Message;}
 }
 private void StopClick(object sender,RoutedEventArgs e){try{link.Stop();StatusText.Text="自动钓鱼已停止";StopButton.IsEnabled=false;}catch(Exception ex){ReasonText.Text=ex.Message;}}
 private void OpenFolderClick(object sender,RoutedEventArgs e){try{Directory.CreateDirectory(root);Process.Start(new ProcessStartInfo(root){UseShellExecute=true});}catch(Exception ex){ReasonText.Text=ex.Message;}}
 private void Refresh()
 {
  snapshot=FishingJson.Read<FishingSnapshot>(Path.Combine(root,"latest.json"));
  bool fresh=FishingControlLink.Fresh(snapshot,DateTime.UtcNow);
  if(!fresh)
  {
   StartButton.IsEnabled=false;
   if(link.Enabled){StatusText.Text="等待游戏恢复实时状态";ReasonText.Text="游戏帧心跳尚未更新，请检查加载、登录或连接状态。";}
   return;
  }
  var s=snapshot!;
  if(link.Enabled && s.OwnerId==link.OwnerId && s.Error.Length>0){try{link.Stop();}catch{}StatusText.Text="自动钓鱼已暂停";}
  else StatusText.Text=link.Enabled?"自动钓鱼运行中":s.Ready?"钓点已识别":"等待进入钓点";
  ReasonText.Text=s.Error.Length>0?s.Error:link.Error.Length>0?link.Error:link.Enabled?s.Reason:s.Ready?"准备好后点击「开始钓鱼」。":"请进入钓鱼地点并面向水面。";
  StatsText.Text=$"鱼：{(s.FishId>0?s.FishId.ToString():"未上钩")}　血量：{s.FishHp:0}　剩余：{s.TimeRemaining:0} 秒　确认收获：{s.Catches}";
  InventoryText.Text=$"鱼背包：{s.BagCount}/{s.BagCapacity}　可售：{s.SellableCount}　保留：{s.ProtectedFishCount}　已确认出售：{s.SoldCount}";
  SaleText.Text=s.SaleStatus;
  BaitText.Text=s.BaitReady?$"鱼饵：{s.BaitCount} 份　{(s.BaitActive?(s.BaitRemainingSeconds>0?$"增益剩余 {s.BaitRemainingSeconds:0} 秒":"增益生效中"):"增益未生效")}　已确认使用：{s.BaitUsedCount}":"鱼饵：尚未就绪";
  BaitStatusText.Text=s.BaitStatus;
  GaugeBar.Value=Math.Clamp(s.Gauge*100,0,100);GaugeText.Text=$"{s.Gauge*100:0}% · 当前档位 {s.CastGrade}";
  NetworkText.Text="网络："+s.Network+(s.NetworkPending?$"（等待 {s.NetworkWaitSeconds:0.0} 秒）":"");
  ActionText.Text=$"阶段：{StateName(s.State)}　操作：{ActionName(s.LastAction)}　共 {s.ActionCount} 次";
  StartButton.IsEnabled=!link.Enabled&&!connecting&&s.Ready&&!s.NetworkPending&&s.State!="Auto";StopButton.IsEnabled=link.Enabled;
 }
 private static string StateName(string s)=>s switch{"None"=>"准备抛竿","Casting"=>"蓄力抛竿","WaitingForBite"=>"等待咬钩","BiteDetected"=>"提竿","Fighting"=>"收线","Pause"=>"波次间隔","Caught"=>"收获结算","Auto"=>"游戏内自动钓鱼",_=>"未就绪"};
 private static string ActionName(string s)=>s switch{"CastPress"=>"开始蓄力","CastRelease"=>"释放抛竿","Hook"=>"提竿","FightClick"=>"收线点击","HoldPress"=>"按住收线","HoldRelease"=>"松开收线","ClosePopup"=>"确认弹窗","SellFish"=>"出售普通／稀有鱼","UseBait"=>"使用一份鱼饵",_=>"尚未操作"};
 private void OnClosing(object? sender,CancelEventArgs e){closing=true;timer.Stop();link.Dispose();}
}
