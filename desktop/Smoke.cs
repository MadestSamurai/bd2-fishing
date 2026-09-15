using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace BD2Fishing.Desktop;
public partial class FishingWindow
{
 public async Task SmokeAsync(string evidence)
 {
  smoke=true;timer.Stop();var checks=new List<string>();void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add(name);}
  Show();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
  Check(!StartButton.IsEnabled,"start disabled before snapshot");Check(IntervalBox.Text=="1000"&&CastBox.Text=="90","defaults");Check(AutoSellBox.IsChecked==true,"automatic sale default visible");Check(AutoBaitBox.IsChecked==true,"automatic bait default visible");
  var s=new FishingSnapshot{Ready=true,CanCast=true,ProcessId=1234,CapturedUtcTicks=DateTime.UtcNow.Ticks,State="Fighting",FishId=101,FishHp=150,TimeRemaining=32,Reason="等待弱点进入判定区",Network="FishingBiteStart 响应成功"};
  FishingJson.Write(Path.Combine(root,"latest.json"),s);Refresh();Check(StartButton.IsEnabled,"ready enables start");
  StartClick(this,new RoutedEventArgs());Check(link.Enabled,"start writes lease");
  var c=FishingJson.Read<FishingControl>(Path.Combine(root,"control.json"))!;Check(c.Enabled&&c.Valid(DateTime.UtcNow.Ticks,1234),"lease valid and pid bound");
  CastBox.Text="99";SettingsChanged(this,new RoutedEventArgs());Check(SettingsHint.Foreground==Brushes.Firebrick,"invalid gauge shown");CastBox.Text="90";
  IntervalBox.Text="500";SettingsChanged(this,new RoutedEventArgs());Check(FishingJson.Read<FishingSettings>(Path.Combine(root,"settings.json"))!.NextCastMilliseconds==500,"live settings saved");
  Check(c.AutoSell,"enabled sale included in lease");
  AutoSellBox.IsChecked=false;SettingsChanged(this,new RoutedEventArgs());Check(!FishingJson.Read<FishingControl>(Path.Combine(root,"control.json"))!.AutoSell,"sale can be disabled live");
  AutoSellBox.IsChecked=true;SettingsChanged(this,new RoutedEventArgs());Check(FishingJson.Read<FishingSettings>(Path.Combine(root,"settings.json"))!.AutoSell,"sale preference persisted");
  Check(c.AutoBait,"automatic bait included in lease");
  AutoBaitBox.IsChecked=false;SettingsChanged(this,new RoutedEventArgs());Check(!FishingJson.Read<FishingControl>(Path.Combine(root,"control.json"))!.AutoBait,"bait disabled live");
  AutoBaitBox.IsChecked=true;SettingsChanged(this,new RoutedEventArgs());Check(FishingJson.Read<FishingSettings>(Path.Combine(root,"settings.json"))!.AutoBait,"bait preference persisted");
  s.BaitReady=true;s.BaitCount=27;s.BaitActive=true;s.BaitRemainingSeconds=115;s.BaitUsedCount=3;s.BaitStatus="增益生效中，不重复消耗鱼饵";
  s.BagCount=200;s.BagCapacity=200;s.SellableCount=180;s.ProtectedFishCount=20;s.SoldCount=100;s.SaleStatus="已确认出售 100 条，保留鱼回读一致";
  s.Gauge=.88;s.LastAction="FightClick";FishingJson.Write(Path.Combine(root,"latest.json"),s);Refresh();UpdateLayout();
  Check(InventoryText.Text.Contains("保留：20") && SaleText.Text.Contains("100"),"inventory and confirmed sale visible");
  Check(BaitText.Text.Contains("27 份") && BaitText.Text.Contains("115 秒") && BaitText.Text.Contains("使用：3"),"bait quantity duration and confirmed usage visible");
  Capture("window.png");Width=650;Height=540;UpdateLayout();Capture("window-small.png");Check(ActualWidth>=MinWidth,"minimum window layout");
  ContentScroll.ScrollToBottom();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);UpdateLayout();
  var baitPoint=AutoBaitBox.TranslatePoint(new Point(0,0),ContentScroll);
  Check(ContentScroll.ScrollableHeight>0 && baitPoint.Y>=0 && baitPoint.Y+AutoBaitBox.ActualHeight<=ContentScroll.ActualHeight,"small window scroll reaches bait switch");Capture("window-small-settings.png");
  s.MapChangePending=true;s.Reason="确认收获；昼夜切换排队，先完成本竿";s.CapturedUtcTicks=DateTime.UtcNow.Ticks;FishingJson.Write(Path.Combine(root,"latest.json"),s);Refresh();Check(link.Enabled && ReasonText.Text.Contains("昼夜切换排队"),"queued day/night keeps automation and explains settlement");
  s.MapChangePending=false;s.Busy=true;s.State="None";s.Reason="等待场景切换";FishingJson.Write(Path.Combine(root,"latest.json"),s);Refresh();Check(link.Enabled && StopButton.IsEnabled,"scene loading keeps lease and stop available");
  s.Busy=false;s.Reason="准备下一竿";FishingJson.Write(Path.Combine(root,"latest.json"),s);Refresh();Check(link.Enabled && ReasonText.Text=="准备下一竿","scene completion shows automatic continuation");
  s.OwnerId=link.OwnerId;s.Error="网络响应超过 30 秒";FishingJson.Write(Path.Combine(root,"latest.json"),s);Refresh();Check(!link.Enabled,"network error stops automation");
  Close();c=FishingJson.Read<FishingControl>(Path.Combine(root,"control.json"))!;Check(!c.Enabled&&c.UntilUtcTicks==0,"close revokes lease");
  FishingJson.Write(Path.Combine(evidence,"results.json"),new{status="pass",assertions=checks});
  void Capture(string name){var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(evidence,name));png.Save(file);}
 }
}
