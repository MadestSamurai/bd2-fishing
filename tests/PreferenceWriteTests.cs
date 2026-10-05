using System.Diagnostics;
using BD2Fishing;
internal static class PreferenceWriteTests
{
 internal static int Run()
 {
  string root=Path.Combine(AppContext.BaseDirectory,"test-data","preference-write-"+Guid.NewGuid().ToString("N"));
  string path=Path.Combine(root,"settings.json");FishingJson.Write(path,new FishingSettings{NextCastMilliseconds=1000});
  int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
  using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
  {
   var write=Task.Run(()=>FishingJson.Write(path,new FishingSettings{NextCastMilliseconds=500}));
   Thread.Sleep(60);Check(!write.IsCompleted,"Writer should wait for a briefly held destination");
   held.Dispose();Check(write.Wait(2000),"Writer did not recover after destination was released");
  }
  Check(FishingJson.Read<FishingSettings>(path)!.NextCastMilliseconds==500,"Replacement after brief lock differs");
  using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
  {
   var watch=Stopwatch.StartNew();bool failed=false;
   try{FishingJson.Write(path,new FishingSettings{NextCastMilliseconds=750});}catch(Exception e)when(e is IOException or UnauthorizedAccessException){failed=true;}
   Check(failed&&watch.Elapsed.TotalSeconds<2,"Permanent occupation must fail with a bounded wait");
  }
  Check(FishingJson.Read<FishingSettings>(path)!.NextCastMilliseconds==500,"A failed save changed the previous preference");
  File.SetAttributes(path,FileAttributes.ReadOnly);
  try
  {
   var watch=Stopwatch.StartNew();bool failed=false;
   try{FishingJson.Write(path,new FishingSettings{NextCastMilliseconds=250});}catch(UnauthorizedAccessException){failed=true;}
   Check(failed&&watch.ElapsedMilliseconds<250,"Read-only settings must remain protected without repeated retries");
  }
  finally{File.SetAttributes(path,FileAttributes.Normal);}
  Check(FishingJson.Read<FishingSettings>(path)!.NextCastMilliseconds==500,"Read-only save changed the prior value");
  Check(Directory.GetFiles(root,"*.tmp").Length==0,"Temporary preference files were left behind");
  return checks;
 }
}
