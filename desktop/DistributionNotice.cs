using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace BD2.Distribution;
// Presentation only. No network access, game actions, file writes or external processes.
internal static class DistributionNotice
{
 internal const string Repository="https://github.com/MadestSamurai/bd2-fishing";
 internal static void Attach(Window window,Selector language)
 {
  if(window.Content is not UIElement body)throw new InvalidOperationException("Missing main view");
  window.Content=null;
  var host=new Grid();host.RowDefinitions.Add(new RowDefinition());host.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});host.Children.Add(body);
  var row=new DockPanel{LastChildFill=true,Margin=new Thickness(20,8,20,10)};
  var button=new Button{Name="SourceNoticeButton",Tag="literal",Padding=new Thickness(10,5,10,5),Margin=new Thickness(12,0,0,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(button,Dock.Right);row.Children.Add(button);
  var text=new TextBlock{Name="SourceNoticeText",Tag="literal",FontSize=11,TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(87,100,116)),VerticalAlignment=VerticalAlignment.Center};row.Children.Add(text);
  bool English()=>language.SelectedIndex==1;
  void Render(){text.Text=English()?"Free & open source · GitHub: MadestSamurai · Bilibili: MadSamurai":"免费开源交流工具 · GitHub：MadestSamurai · B站：MadSamurai";button.Content=English()?"About & source":"来源与说明";}
  button.Click+=(_,_)=>Show(window,English());language.SelectionChanged+=(_,_)=>Render();Render();
  var border=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(216,222,231)),BorderThickness=new Thickness(0,1,0,0),Child=row};Grid.SetRow(border,1);host.Children.Add(border);window.Content=host;
 }
 static void Show(Window owner,bool english)
 {
  var dialog=new Window{Owner=owner,Title=english?"About & source":"来源与说明",Width=580,Height=560,MinWidth=380,MinHeight=340,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brushes.White,Foreground=new SolidColorBrush(Color.FromRgb(34,44,56)),FontFamily=owner.FontFamily};
  var panel=new StackPanel{Margin=new Thickness(24)};
  void Text(string s,int size=14){panel.Children.Add(new TextBlock{Text=s,Tag="literal",FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});}
  Text(english?"Free release from the author":"作者发布版免费提供",20);
  Text("GitHub: MadestSamurai · "+(english?"Bilibili":"B站")+": MadSamurai");
  Text(english?"This community project is not affiliated with or endorsed by the game developer or publisher. Helper tools may result in account penalties, bans, game errors or data loss. Follow the game rules, assess the risks and take responsibility for your use.":"本工具用于交流学习，与游戏开发商、发行商无关联，也未获其背书。使用辅助工具可能导致账号处罚、封禁、游戏异常或数据损失。请了解并遵守游戏规则，评估风险后自行决定使用并承担相关后果。");
  Text(english?"Official downloads are free. Third-party fees do not imply the author's involvement, endorsement or support. Modified builds may differ from the author's release. Check the source at the repository below.":"作者提供的官方下载免费。第三方收费不代表作者参与、背书或提供服务；第三方修改版可能与作者发布版不同。请通过下方仓库和发布页核对来源。");
  Text(english?"The MIT License remains unchanged. This notice adds no license restrictions and does not promise immunity from liability.":"MIT 许可证保持不变。本说明不增加许可限制，也不承诺免责效果。");
  Text(english?"Official source and downloads (select to copy)":"源码与官方下载（可选择复制）");
  panel.Children.Add(new TextBox{Text=Repository+"\n"+Repository+"/releases",Tag="literal",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,BorderThickness=new Thickness(1),Padding=new Thickness(8),Margin=new Thickness(0,0,0,16)});
  var done=new Button{Content=english?"Close":"关闭",Tag="literal",Padding=new Thickness(16,7,16,7),HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};done.Click+=(_,_)=>dialog.Close();panel.Children.Add(done);
  dialog.Content=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=panel};dialog.ShowDialog();
 }
}
