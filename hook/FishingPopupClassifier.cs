namespace BD2Fishing.Runtime
{
    internal static class FishingPopupClassifier
    {
        // The client uses this same read-only classification for popup closing and field pause.
        // Popup rendering includes persistent HUDs (currency, notices and field controls).
        internal static bool IsBlockingPopup(UIBase surface) => FishingBindings.Flag(surface,"ὡὡὡὯὬὨὢὧὦὦὫ")
            && !(bool)FishingBindings.Invoke("Ui.IsHud",surface);
    }
}
