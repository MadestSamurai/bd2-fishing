using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using gamfs.Fishing;
namespace BD2Fishing.Runtime
{
    internal static class FishingBindings
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly Dictionary<string,MemberInfo> cache=new Dictionary<string,MemberInfo>();
        private static readonly Dictionary<string,MemberInfo> apiCache=new Dictionary<string,MemberInfo>();
        private static readonly Dictionary<string,Type> typeCache=new Dictionary<string,Type>();
        internal static MemberInfo Member(Type t,string name)
        {
            var key=t.FullName+"|"+name;
            if(cache.TryGetValue(key,out var value))return value;
            for(var next=t;next!=null;next=next.BaseType)
            {
                var actual=FishingClient.MemberNames.TryGetValue((next.IsGenericType?next.GetGenericTypeDefinition():next).FullName+"|"+name,out var mapped)?mapped:name;
                value=(MemberInfo)next.GetField(actual,Flags)??next.GetProperty(actual,Flags)??(MemberInfo)next.GetMethods(Flags).SingleOrDefault(m=>m.Name==actual);
                if(value!=null){cache[key]=value;return value;}
            }
            throw new MissingMemberException(t.FullName,name);
        }
        internal static object Get(object obj,string name){if(obj==null)return null;var m=Member(obj.GetType(),name);return m is FieldInfo f?f.GetValue(obj):((PropertyInfo)m).GetValue(obj,null);}
        internal static object Call(object obj,string name,params object[] args)=>((MethodInfo)Member(obj.GetType(),name)).Invoke(obj,args);
        internal static bool Flag(object obj,string name)=>Get(obj,name) is bool b&&b;
        internal static double Num(object obj,string name)=>Convert.ToDouble(Get(obj,name)??0);
        internal static bool Active(Component c)=>c!=null && c.gameObject.activeInHierarchy;
        internal static bool Active(GameObject o)=>o!=null && o.activeInHierarchy;
        internal static Type Type(string role) {if(typeCache.TryGetValue(role,out var known))return known;return typeCache[role]=typeof(FishingGameFieldDefaultUI).Assembly.GetType(FishingClient.TypeNames.TryGetValue(role,out var name)?name:role,true);}
        internal static MemberInfo Api(string role) {if(apiCache.TryGetValue(role,out var known))return known;var entry=FishingClient.Apis[role];var type=Type(entry[0]);return apiCache[role]=entry[2]=="method"?(MemberInfo)type.GetMethods(Flags).Single(m=>m.MetadataToken==int.Parse(entry[1])):Member(type,entry[1]);}
        internal static object Read(string role,object owner) {var m=Api(role);return m is FieldInfo f?f.GetValue(owner):((PropertyInfo)m).GetValue(owner,null);}
        internal static object Invoke(string role,params object[] args) => ((MethodInfo)Api(role)).Invoke(null,args);
        internal static object EnumObject(string role,string name) => Enum.Parse(Type(role),name);
        internal static int EnumValue(string role,string name) => Convert.ToInt32(EnumObject(role,name));
        internal static void ValidateCompiledClient()
        {
            // This is a per-connection race guard for the locally generated hook, not a release version lock.
            if(typeof(FishingGameFieldDefaultUI).Module.ModuleVersionId.ToString()!=FishingClient.CompiledMvid)
                throw new InvalidOperationException("游戏运行中客户端文件发生变化，请正常重启游戏后重新连接。");
        }
        internal static int Validate()
        {
            int count=0;var game=typeof(FishingGameFieldDefaultUI).Assembly;
            foreach(var pair in new Dictionary<string,string[]>{
                {"FishingGameFieldDefaultUI",new[]{"ὢὨὠὩὤὬὠὧὬὠὬ","_castingCharger","_baitInfo","_skillCaster","_hitzonePicker","_goBtnHook","_goBtnFight","_rectHitzoneNormal","_rectHitzoneWeak","ὬὣὦὫὬὯὧὮὣὣὡ","ὣὥὪὥὫὬὬὮὦὭὩ","ὯὦὦὯὤὪὡὮὢὫὮ"}},
                {"gamfs.Fishing.FishingManager",new[]{"ὮὧὥὥὦὬὤὭὤὡὪ","ὧὠὤὦὠὡὠὪὪὢὠ","ὯὥὬὦὤὬὡὤὡὡὨ","ὢὣὢὬὣὧὣὫὭὤὬ","ὨὥὯὤὪὣὬὪὤὬὬ","UseItem","HasBuff","ὥὬὪὥὠὣὪὡὢὥὮ","ὭὡὧὡὢὡὬὬὧὥὯ","ὢὪὠὠὩὪὧὧὭὨὬ","ὡὣὧὫὦὯὧὤὧὦὬ","ὣὤὣὬὪὡὥὨὪὫὤ","EnterPackFishing"}},
                {"FishingBaitInfoItem",new[]{"ὩὯὪὩὯὫὮὤὭὧὢ"}},
                {"gamfs.Fishing.FishingTimerHub",new[]{"IsRunning","GetRemaining"}},
                {"FishingCastingCharger",new[]{"ὩὯὪὩὯὫὮὤὭὧὢ","ὩὡὣὢὬὦὥὧὥὭὦ","GetNormalizeValue"}},
                {"FishingSkillCaster",new[]{"_holdItem","ὡὮὮὪὨὨὪὪὫὣὩ","ὦὣὪὬὫὢὢὩὤὥὤ","ὦὪὦὭὧὯὮὮὪὫὣ","ὦὭὠὠὣὢὯὢὩὣὪ"}},
                {"FishingSkillHoldItem",new[]{"Update","_rectStart","_rectEnd","_rectKeep","ὯὩὬὯὮὯὦὧὮὪὢ","ὬὩὡὩὦὧὤὤὠὩὠ","ὪὤὧὥὡὬὩὪὤὨὣ","ὮὢὣὩὫὥὠὨὢὬὧ"}},
                {"ὧὩὮὥὥὤὧὢὭὮὪ",new[]{"ὦὡὬὨὤὢὠὥὬὡὣ","ὮὬὠὯὡὧὢὥὥὢὦ","ὤὥὫὤὦὧὪὤὤὢὯ"}},
                {"ὣὠὦὨὩὫὩὩὣὤὮ",new[]{"ὦὬὢὢὮὩὨὨὠὠὯ"}},
                {"ὧὭὥὩὯὦὧὠὧὡὦ",new[]{"ὩὤὮὠὠὥὪὢὩὯὡ"}},
                {"UIBase",new[]{"_isPopupUI","CanCloseUI","CloseUI","ὧὨὦὯὣὣὡὣὪὮὨ","ὤὩὪὬὬὤὪὮὣὩὩ"}},
                {"ὨὧὠὯὪὦὩὣὤὢὡ",new[]{"ὩὠὮὥὫὧὢὣὯὯὫ"}}
            })foreach(var n in pair.Value){Member(Type(pair.Key),n);count++;}
            foreach(var field in new[]{"ὡὮὮὪὨὨὪὪὫὣὩ","ὦὣὪὬὫὢὢὩὤὥὤ","ὦὪὦὭὧὯὮὮὪὫὣ","ὦὭὠὠὣὢὯὢὩὣὪ"})
            {var pool=((FieldInfo)Member(typeof(FishingSkillCaster),field)).FieldType;Member(pool,"ὡὫὮὠὡὩὮὨὮὡὬ");count++;}
            foreach(var name in new[]{"FishingSkillTeethItem","FishingSkillRecoveryItem","FishingSkillTrapItem","FishingSkillShellShieldItem"})
            {Member(Type(name),"ὤὨὪὫὧὥὫὮὣὮὥ");count++;}
            foreach(var pair in new[]{new[]{"ὣὠὦὨὩὫὩὩὣὤὮ","ὦὬὢὢὮὩὨὨὠὠὯ"},new[]{"ὧὭὥὩὯὦὧὠὧὡὦ","ὩὤὮὠὠὥὪὢὩὯὡ"}})
            {var hp=((PropertyInfo)Member(Type(pair[0]),pair[1])).PropertyType;Member(hp,"ὬὤὦὩὮὨὠὬὯὯὣ");count++;}
            return count;
        }
    }
}
