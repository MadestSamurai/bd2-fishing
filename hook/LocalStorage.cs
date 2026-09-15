using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
namespace BD2Fishing.Runtime
{
    internal static class LocalStorage
    {
        internal static string DataRoot=>FishingIdentity.DataRoot;
        internal static void WriteJsonAtomically(string path,object value)
        {using(var b=new MemoryStream()){new DataContractJsonSerializer(value.GetType()).WriteObject(b,value);WriteAtomically(path,b.ToArray());}}
        internal static void WriteAtomically(string path,byte[] value)
        {
            var directory=Path.GetDirectoryName(path);Directory.CreateDirectory(directory);var tmp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
            try{File.WriteAllBytes(tmp,value);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
            finally{if(File.Exists(tmp))File.Delete(tmp);}
        }
        internal static void Log(string message)
        {
            try{Directory.CreateDirectory(DataRoot);var path=Path.Combine(DataRoot,"runtime.log");lock(typeof(LocalStorage))
            {if(File.Exists(path)&&new FileInfo(path).Length>1000000){var old=path+".previous";if(File.Exists(old))File.Delete(old);File.Move(path,old);}File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine,Encoding.UTF8);}}catch{}
        }
    }
}
