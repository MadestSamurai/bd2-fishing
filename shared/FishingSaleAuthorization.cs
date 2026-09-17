namespace BD2Fishing
{
    public sealed class FishingSaleAuthorization
    {
        private readonly string owner,fingerprint;
        public string OwnerId {get{return owner;}}
        public FishingSaleAuthorization(FishingControl control)
        {
            owner=control.OwnerId;
            fingerprint=(control.Retention??new FishingRetentionOptions()).Fingerprint();
        }
        public bool Valid(FishingControl control,long now,int processId)
        {
            return control!=null && control.Valid(now,processId) && control.AutoSell && control.OwnerId==owner
                && (control.Retention??new FishingRetentionOptions()).Fingerprint()==fingerprint;
        }
    }
}
