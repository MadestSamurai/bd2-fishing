using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
var managed=Path.GetFullPath(args[0]);var hookPath=Path.GetFullPath(args[1]);Assembly? hook=null;
AppDomain.CurrentDomain.AssemblyResolve+=(_,e)=>{var name=new AssemblyName(e.Name).Name;if(name=="0Harmony"&&hook!=null){using var s=hook.GetManifestResourceStream("BD2Fishing.Harmony.dll")!;using var b=new MemoryStream();s.CopyTo(b);return Assembly.Load(b.ToArray());}var file=Path.Combine(managed,name+".dll");return File.Exists(file)?Assembly.LoadFrom(file):null;};
hook=Assembly.LoadFrom(hookPath);var types=hook.GetTypes();int assertions=0;
void Check(bool ok,string message){assertions++;if(!ok)throw new Exception(message);}
Check(hook.GetName().Name=="BD2Fishing.Runtime7","identity");
Check(!types.Any(t=>new[]{"Sichuan","Watcher","Golden","Mirror","ReplayCapture"}.Any(n=>(t.FullName??"").Contains(n))),"unrelated type closure");
var identity=hook.GetType("BD2Fishing.FishingIdentity")!;var game=Assembly.LoadFrom(Path.Combine(managed,"Assembly-CSharp.dll"));
var flags=BindingFlags.Static|BindingFlags.NonPublic;
var bindings=hook.GetType("BD2Fishing.Runtime.FishingBindings")!;
MemberInfo Member(Type t,string name)=>(MemberInfo)bindings.GetMethod("Member",flags)!.Invoke(null,new object[]{t,name})!;
Type Role(string name)=>(Type)bindings.GetMethod("Type",flags)!.Invoke(null,new object[]{name})!;
int members=(int)bindings.GetMethod("Validate",flags)!.Invoke(null,null)!;Check(members>=40,"binding closure");
var runtime=hook.GetType("BD2Fishing.Runtime.RuntimeEngine")!;Check(runtime.GetMethod("Pump",flags)!.Invoke(null,null) is MethodInfo,"main thread pump");
var managerType=game.GetType("gamfs.Fishing.FishingManager")!;
var manager=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(managerType);
var snapshotType=hook.GetType("BD2Fishing.FishingSnapshot")!;var mapSnapshot=Activator.CreateInstance(snapshotType)!;
var transitionReader=runtime.GetMethod("ReadMapTransition",flags)!;
foreach(var pendingFlag in new[]{false,true})foreach(var activeFlag in new[]{false,true})
{
 ((PropertyInfo)Member(managerType,"ὢὣὢὬὣὧὣὫὭὤὬ")).SetValue(manager,pendingFlag);
 ((PropertyInfo)Member(managerType,"ὯὥὬὦὤὬὡὤὡὡὨ")).SetValue(manager,activeFlag);
 transitionReader.Invoke(null,new[]{manager,mapSnapshot});
 Check((bool)snapshotType.GetProperty("MapChangePending")!.GetValue(mapSnapshot)! == pendingFlag && (bool)snapshotType.GetProperty("Busy")!.GetValue(mapSnapshot)! == activeFlag,"native map transition flags remain independent");
}
((PropertyInfo)Member(managerType,"ὢὣὢὬὣὧὣὫὭὤὬ")).SetValue(manager,true);
((PropertyInfo)Member(managerType,"ὯὥὬὦὤὬὡὤὡὡὨ")).SetValue(manager,false);
transitionReader.Invoke(null,new[]{manager,mapSnapshot});
foreach(var v in new Dictionary<string,object>{{"ProcessId",123},{"Ready",true},{"State","Caught"},{"ResultPopup",true},{"CanClosePopup",true},{"PopupId",7}})snapshotType.GetProperty(v.Key)!.SetValue(mapSnapshot,v.Value);
var controlType=hook.GetType("BD2Fishing.FishingControl")!;var mapControl=Activator.CreateInstance(controlType)!;var mapNow=DateTime.UtcNow.Ticks;
foreach(var v in new Dictionary<string,object>{{"ProcessId",123},{"Enabled",true},{"OwnerId","map-abi"},{"UntilUtcTicks",mapNow+TimeSpan.FromSeconds(10).Ticks}})controlType.GetProperty(v.Key)!.SetValue(mapControl,v.Value);
var policyType=hook.GetType("BD2Fishing.FishingPolicy")!;var mapPolicy=Activator.CreateInstance(policyType)!;
Check(policyType.GetMethod("Next")!.Invoke(mapPolicy,new[]{mapSnapshot,mapControl,(object)mapNow,false})!.ToString()=="ClosePopup","real client pending transition allows result confirmation");
snapshotType.GetProperty("ResultPopup")!.SetValue(mapSnapshot,false);snapshotType.GetProperty("State")!.SetValue(mapSnapshot,"None");snapshotType.GetProperty("CanCast")!.SetValue(mapSnapshot,true);
Check(policyType.GetMethod("Next")!.Invoke(mapPolicy,new[]{mapSnapshot,mapControl,(object)(mapNow+TimeSpan.FromSeconds(2).Ticks),false})!.ToString()=="None","real client pending transition reserves idle for scene change");
var observer=hook.GetType("BD2Fishing.Runtime.FishingNetwork")!;Check(observer.GetMethod("SendMethod",flags)!.Invoke(null,null) is MethodInfo,"network method");
var handlers=(System.Collections.IDictionary)observer.GetMethod("ResolveHandlers",flags)!.Invoke(null,null)!;Check(handlers.Count==6,"six original response handlers including sale and bait");
// Execute the production classifier against actual client UI types without constructing Unity objects.
var classifier=hook.GetType("BD2Fishing.Runtime.FishingPopupClassifier")!.GetMethod("IsBlockingPopup",flags)!;
var uiBase=game.GetType("UIBase")!;
var uiKey=((PropertyInfo)Member(uiBase,"ὧὨὦὯὣὣὡὣὪὮὨ"));
var popupFlag=uiBase.GetField("_isPopupUI",BindingFlags.NonPublic|BindingFlags.Instance)!;
foreach(var sample in new[]{
    ("CurrencyManageUI","CurrencyManageUI",false),
    ("FishingGameFieldDefaultUI","FishingGameFieldDefaultUI",false),
    ("OverheadManageUI","OverheadManageUI",false),
    ("NoticeUI","NoticeUI",false),
    ("MessagePopupUI","MessagePopupUI",true),
    ("AvatarFishingUseBaitPopupUI","AvatarFishingUseBaitPopupUI",true),
    ("AvatarFishingGetPopupUI","AvatarFishingGetPopupUI",true),
    ("AvatarFishingLevelUpPopupUI","AvatarFishingLevelUpPopupUI",true),
    ("UIBase","UnrecognizedPopup",true)})
{
    var surface=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(game.GetType(sample.Item1,true)!);
    uiKey.SetValue(surface,sample.Item2);popupFlag.SetValue(surface,true);
    Check((bool)classifier.Invoke(null,new[]{surface})! == sample.Item3,"client popup classification "+sample.Item2);
    popupFlag.SetValue(surface,false);
    Check(!(bool)classifier.Invoke(null,new[]{surface})!,"non-popup remains non-modal "+sample.Item2);
}
var inventoryType=hook.GetType("BD2Fishing.Runtime.FishingInventory")!;
Check(inventoryType.GetMethod("SaleMethod",flags)!.Invoke(null,null) is MethodInfo,"normal game batch-sale helper");
var rarity=Role("FishGrade");
Check(Convert.ToInt32(Enum.Parse(rarity,"Legendary"))==4 && Convert.ToInt32(Enum.Parse(rarity,"Normal"))==1 && Convert.ToInt32(Enum.Parse(rarity,"Rare"))==2,"client rarity values match protection policy");
var mapType=hook.GetType("BD2Fishing.Runtime.FishingMap")!;
var fillClock=mapType.GetMethod("FillClock",flags)!;
var clockStart=new DateTime(2026,9,15,8,0,0,DateTimeKind.Unspecified);
fillClock.Invoke(null,new object[]{mapSnapshot,clockStart,clockStart.AddHours(5).AddMinutes(56),21600d});
Check((bool)snapshotType.GetProperty("RoomTimerKnown")!.GetValue(mapSnapshot)! && (double)snapshotType.GetProperty("RoomRemainingSeconds")!.GetValue(mapSnapshot)! == 240,"room clock uses game timestamps for late connections");
fillClock.Invoke(null,new object[]{mapSnapshot,clockStart,clockStart.AddHours(-8),21600d});
Check(!(bool)snapshotType.GetProperty("RoomTimerKnown")!.GetValue(mapSnapshot)!,"mixed or future clock must not cause travel");
foreach(var role in new[]{"Clock.Instance","Clock.Now","Tables.Default","Inventory.MapUnlocked"})
 Check(bindings.GetMethod("Api",flags)!.Invoke(null,new object[]{role}) is MemberInfo,"map API resolves: "+role);
Check(((MethodInfo)Member(managerType,"EnterPackFishing")).GetParameters().Single().ParameterType==typeof(int),"native group travel method");
Check(((FieldInfo)Member(managerType,"ὡὣὧὫὦὯὧὤὧὦὬ")).FieldType.FullName=="UnityEngine.Coroutine","real load coroutine gate");
Check(((PropertyInfo)Member(managerType,"ὢὪὠὠὩὪὧὧὭὨὬ")).PropertyType==typeof(DateTime),"native room start timestamp");
var fishType=hook.GetType("BD2Fishing.FishingSaleItem")!;
var fish=Activator.CreateInstance(fishType)!;
foreach(var v in new Dictionary<string,object>{{"InvenIndex",9123456789L},{"FishId",123},{"Grade",2},{"HasFishTable",true},{"HasSaleEntry",true}})fishType.GetProperty(v.Key)!.SetValue(fish,v.Value);
var samples=Array.CreateInstance(fishType,1);samples.SetValue(fish,0);
var buildSale=hook.GetType("BD2Fishing.FishingSalePlan")!.GetMethod("Build")!;
var salePlan=buildSale.Invoke(null,new object[]{samples,true})!;
var items=(System.Collections.IList)inventoryType.GetMethod("RequestItems",flags)!.Invoke(null,new[]{salePlan})!;
var item=items[0]!;object Val(string n)=>((PropertyInfo)Member(item.GetType(),n)).GetValue(item)!;
Check(items.Count==1 && Convert.ToInt32(Val("ὢὯὧὤὮὭὮὣὭὪὣ"))==123 && Convert.ToInt64(Val("ὠὬὩὥὥὨὠὭὩὬὬ"))==9123456789L && Convert.ToInt32(Val("ὬὯὭὩὡὮὩὨὢὭὣ"))==56 && Convert.ToInt32(Val("ὬὯὫὯὮὠὡὪὤὣὬ"))==1,"normal sale DTO preserves concrete inventory ID, fish type and quantity");
fishType.GetProperty("Grade")!.SetValue(fish,4);
bool protectedRejected=false;try{inventoryType.GetMethod("RequestItems",flags)!.Invoke(null,new[]{salePlan});}catch(TargetInvocationException e)when(e.InnerException is InvalidOperationException){protectedRejected=true;}
Check(protectedRejected,"runtime request builder rechecks protected grade");
fishType.GetProperty("IsLocked")!.SetValue(fish,true);
var optOutPlan=buildSale.Invoke(null,new object[]{samples,false})!;
var optOutItems=(System.Collections.IList)inventoryType.GetMethod("RequestItems",flags)!.Invoke(null,new[]{optOutPlan})!;
Check(optOutItems.Count==1,"runtime request builder accepts locked legendary fish after explicit opt-out");
fishType.GetProperty("HasSaleEntry")!.SetValue(fish,false);
bool unknownRejected=false;try{inventoryType.GetMethod("RequestItems",flags)!.Invoke(null,new[]{optOutPlan});}catch(TargetInvocationException e)when(e.InnerException is InvalidOperationException){unknownRejected=true;}
Check(unknownRejected,"opt-out still rechecks native sale eligibility");
var baitType=hook.GetType("BD2Fishing.Runtime.FishingBait")!;
var useMethod=(MethodInfo)baitType.GetMethod("UseMethod",flags)!.Invoke(null,null)!;
Check(useMethod.ReturnType==typeof(void) && useMethod.GetParameters()[3].HasDefaultValue && (int)useMethod.GetParameters()[3].DefaultValue! == 1,"native bait use quantity defaults to one");
Check(Convert.ToInt32(Enum.Parse(Role("ItemType"),"FishingConsumable"))==58,"native consumable type");
var baitDbType=game.GetType("Proto.Net.FishingItemDBInfo")!;var baitDb=Activator.CreateInstance(baitDbType)!;
foreach(var v in new Dictionary<string,object>{{"InvenIndex",9123456789L},{"Id",1},{"Type",58},{"Count",1}})baitDbType.GetProperty(v.Key)!.SetValue(baitDb,v.Value);
var validBait=baitType.GetMethod("ValidItem",flags)!;
Check((bool)validBait.Invoke(null,new[]{baitDb})!,"last bait in real long-ID stack usable");
foreach(var changedField in new[]{("Count",0),("Id",2),("Type",56)})
{
 var field=baitDbType.GetProperty(changedField.Item1)!;var previous=field.GetValue(baitDb);field.SetValue(baitDb,changedField.Item2);
 Check(!(bool)validBait.Invoke(null,new[]{baitDb})!,"bait rejects changed "+changedField.Item1);field.SetValue(baitDb,previous);
}
Check(!(bool)validBait.Invoke(null,new object?[]{null})!,"bait rejects absent concrete item");
foreach(var a in hook.GetReferencedAssemblies())Check(a.Name=="0Harmony"||File.Exists(Path.Combine(managed,a.Name+".dll")),"missing dependency "+a.Name);
Console.WriteLine(JsonSerializer.Serialize(new{status="pass",assertions,members,hookTypes=types.Length,responseHandlers=handlers.Count,gameRequests=0,injection=false}));
