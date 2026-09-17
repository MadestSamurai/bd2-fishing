namespace BD2Fishing
{
    public sealed class FishingSpeciesSummary
    {
        public int FishId {get;set;}
        public string Name {get;set;}="";
        public int Count {get;set;}
        public int MinSize {get;set;}
        public int MaxSize {get;set;}
    }
}
