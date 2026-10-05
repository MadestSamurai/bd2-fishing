// Test-only native boundary; the production FishingInventory coordinator is compiled unchanged.
using System.Collections;
using System.Reflection;
namespace Proto.Net {
 public class FishingFishDBInfo {public long InvenIndex {get;set;} public int Id {get;set;} public int Size {get;set;} public bool IsLock {get;set;}}
}
namespace BD2Fishing.Runtime {
 internal sealed class InventoryHost {
  public List<Proto.Net.FishingFishDBInfo> Fish=new();
  public int FishingFishInvenSlot {get;set;}=400;
  public List<long[]> Sales=new();public List<long> Unlocks=new();public bool MissingTable;public int FishGrade=4;
  public object? Invoke(string api,object?[] args)=>api switch {
   "Inventory.FishList"=>Fish,
   "Tables.Shop"=>new {ShopItemId=1},
   "Tables.ShopEntries"=>Fish.Select(f=>f.Id).Distinct().Select(id=>new {GroupId=1,ItemType=1,ItemId=id,PriceCount=1}).ToArray(),
   "Tables.Fish"=>MissingTable?null:new {Id=(int)args[0]!,Grade=FishGrade,NameTextId=(int)args[0]!},
   "Text.FishName"=>"fish-"+args[0],
   "Inventory.Sell"=>Sell((IList)args[1]!),
   "Inventory.Unlock"=>Unlock((long)args[0]!),
   _=>throw new InvalidOperationException(api)
  };
  object? Sell(IList list){Sales.Add(list.Cast<SaleItem>().Select(f=>f.Index).ToArray());return null;}
  object? Unlock(long id){Unlocks.Add(id);return null;}
 }
 internal sealed class SaleItem {
  public long Index;public SaleItem(int id,int type,int quantity,long index){if(quantity!=1)throw new Exception("fish quantity");Index=index;}
 }
 internal static partial class FishingBindings {
  internal static InventoryHost Inventory=new();
  public static object? Invoke(string api,params object?[] args)=>Inventory.Invoke(api,args);
  public static int EnumValue(string role,string name)=>1;
  public static Type Type(string name)=>typeof(SaleItem);
  public static MemberInfo Api(string name)=>typeof(InventoryHost).GetMethod("Invoke")!;
 }
}
