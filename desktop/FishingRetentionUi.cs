using System.Windows;
using System.Windows.Controls;
namespace BD2Fishing.Desktop;
public partial class FishingWindow
{
 private readonly Dictionary<int,FishingSizeMode> sizeRules=new();
 private readonly Dictionary<int,string> fishNames=new();
 private FishingSpeciesSummary[] currentSpecies=Array.Empty<FishingSpeciesSummary>();
 private bool retentionUiLoading;
 private string speciesKey="";
 private sealed record SpeciesChoice(int Id,string Name){public override string ToString()=>Name;}
 private void LoadRetention(FishingRetentionOptions? options)
 {
  var value=(options??new()).Clone();
  retentionUiLoading=true;
  try
  {
   KeepLegendaryBox.IsChecked=value.KeepLegendary!=false;KeepLockedBox.IsChecked=value.KeepLocked!=false;KeepUnknownBox.IsChecked=value.KeepUnknown!=false;
   SizeLegendaryBox.IsChecked=value.SizeLegendary!=false;SizeLockedBox.IsChecked=value.SizeLocked!=false;
   sizeRules.Clear();foreach(var rule in value.SizeRules??Array.Empty<FishingSizeRule>())sizeRules[rule.FishId]=rule.Mode;
   SizeModeBox.ItemsSource=new[]{"不额外保留","仅最大","仅最小","最大和最小"};
  }
  finally{retentionUiLoading=false;}
  UpdateSpecies(Array.Empty<FishingSpeciesSummary>());
 }
 private FishingRetentionOptions RetentionSettings()=>new()
 {
  KeepLegendary=KeepLegendaryBox.IsChecked==true,KeepLocked=KeepLockedBox.IsChecked==true,KeepUnknown=KeepUnknownBox.IsChecked==true,
  SizeLegendary=SizeLegendaryBox.IsChecked==true,SizeLocked=SizeLockedBox.IsChecked==true,
  SizeRules=sizeRules.OrderBy(p=>p.Key).Select(p=>new FishingSizeRule{FishId=p.Key,Mode=p.Value}).ToArray()
 };
 private void UpdateSpecies(FishingSpeciesSummary[]? species)
 {
  currentSpecies=species??Array.Empty<FishingSpeciesSummary>();
  foreach(var fish in currentSpecies.Where(f=>f.FishId>0))fishNames[fish.FishId]=string.IsNullOrWhiteSpace(fish.Name)?$"鱼种 #{fish.FishId}":System.Text.RegularExpressions.Regex.Replace(fish.Name,"<[^>]*>","");
  var ids=currentSpecies.Select(f=>f.FishId).Concat(sizeRules.Keys).Where(id=>id>0).Distinct().OrderBy(id=>id).ToArray();
  var key=string.Join("|",ids.Select(id=>$"{id}:{(fishNames.TryGetValue(id,out var name)?name:"")}"));
  if(speciesKey!=key || SpeciesBox.ItemsSource==null)
  {
   var selected=(SpeciesBox.SelectedItem as SpeciesChoice)?.Id;retentionUiLoading=true;
   try
   {
    var choices=ids.Select(id=>new SpeciesChoice(id,fishNames.TryGetValue(id,out var name)?name:$"鱼种 #{id}（已保存）")).ToArray();
    SpeciesBox.ItemsSource=choices;SpeciesBox.SelectedItem=choices.FirstOrDefault(c=>c.Id==selected)??choices.FirstOrDefault();speciesKey=key;
   }
   finally{retentionUiLoading=false;}
   ShowSpeciesRule();
  }
  ShowSpeciesSummary();
 }
 private void SpeciesChanged(object sender,SelectionChangedEventArgs e){if(!retentionUiLoading)ShowSpeciesRule();}
 private void ShowSpeciesRule()
 {
  retentionUiLoading=true;
  try
  {
   var choice=SpeciesBox.SelectedItem as SpeciesChoice;SizeModeBox.IsEnabled=choice!=null;
   SizeModeBox.SelectedIndex=choice!=null&&sizeRules.TryGetValue(choice.Id,out var mode)?(int)mode:0;
  }
  finally{retentionUiLoading=false;}
  ShowSpeciesSummary();
 }
 private void ShowSpeciesSummary()
 {
  var choice=SpeciesBox.SelectedItem as SpeciesChoice;
  var fish=choice==null?null:currentSpecies.FirstOrDefault(f=>f.FishId==choice.Id);
  SpeciesHint.Text=choice==null?"连接游戏并读取背包后，可按鱼种设置。":fish==null?"当前背包没有此鱼种；已保存的规则继续有效。":$"当前 {fish.Count} 条 · 最小 {SizeText(fish.MinSize)} · 最大 {SizeText(fish.MaxSize)}";
 }
 private static string SizeText(int size)=>size>0?(size/10d).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" cm":"未知";
 private void SizeModeChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!initialized || retentionUiLoading || SpeciesBox.SelectedItem is not SpeciesChoice choice || SizeModeBox.SelectedIndex<0)return;
  sizeRules[choice.Id]=(FishingSizeMode)SizeModeBox.SelectedIndex;SettingsChanged(sender,e);
 }
}
