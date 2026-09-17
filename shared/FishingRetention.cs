using System;
using System.Linq;
namespace BD2Fishing
{
    public enum FishingSizeMode { None, Maximum, Minimum, Both }
    public sealed class FishingSizeRule
    {
        public int FishId {get;set;}
        public FishingSizeMode Mode {get;set;}
    }
    public sealed class FishingRetentionOptions
    {
        public bool? KeepLegendary {get;set;} = true;
        public bool? KeepLocked {get;set;} = true;
        public bool? KeepUnknown {get;set;} = true;
        public bool? SizeLegendary {get;set;} = true;
        public bool? SizeLocked {get;set;} = true;
        public FishingSizeRule[] SizeRules {get;set;} = new FishingSizeRule[0];
        public FishingRetentionOptions Clone()
        {
            var rules=SizeRules??new FishingSizeRule[0];
            if(rules.Any(r=>r==null || r.FishId<=0 || !Enum.IsDefined(typeof(FishingSizeMode),r.Mode)) || rules.Select(r=>r.FishId).Distinct().Count()!=rules.Length)
                throw new ArgumentException("鱼种尺寸规则无效或重复");
            return new FishingRetentionOptions{KeepLegendary=KeepLegendary!=false,KeepLocked=KeepLocked!=false,KeepUnknown=KeepUnknown!=false,SizeLegendary=SizeLegendary!=false,SizeLocked=SizeLocked!=false,
                SizeRules=rules.Select(r=>new FishingSizeRule{FishId=r.FishId,Mode=r.Mode}).ToArray()};
        }
        public string Fingerprint()
        {
            var o=Clone();
            return string.Join("",new[]{o.KeepLegendary,o.KeepLocked,o.KeepUnknown,o.SizeLegendary,o.SizeLocked}.Select(v=>v==false?"0":"1"))+":"+
                string.Join(";",o.SizeRules.Where(r=>r.Mode!=FishingSizeMode.None).OrderBy(r=>r.FishId).Select(r=>r.FishId.ToString(System.Globalization.CultureInfo.InvariantCulture)+"="+((int)r.Mode).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }
}
