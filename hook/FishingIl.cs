using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace BD2Fishing.Runtime
{
    internal static class FishingIl
    {
        private static readonly Dictionary<short,OpCode> codes=Build();
        private static Dictionary<short,OpCode> Build()
        {var result=new Dictionary<short,OpCode>();foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static))if(f.FieldType==typeof(OpCode)){var c=(OpCode)f.GetValue(null);result[c.Value]=c;}return result;}
        internal static bool CallsParser(MethodInfo method,Type response)
        {
            var il=method.GetMethodBody()?.GetILAsByteArray();if(il==null)return false;
            for(int at=0;at<il.Length;)
            {
                int value=il[at++];if(value==0xfe)value=0xfe00|il[at++];
                var op=codes[unchecked((short)value)];
                if(op.OperandType==OperandType.InlineMethod)
                {
                    var target=method.Module.ResolveMethod(BitConverter.ToInt32(il,at),method.DeclaringType.IsGenericType?method.DeclaringType.GetGenericArguments():null,method.IsGenericMethod?method.GetGenericArguments():null);
                    if(target.DeclaringType==response && target.Name=="get_Parser")return true;
                }
                switch(op.OperandType)
                {
                    case OperandType.InlineNone:break;
                    case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:at++;break;
                    case OperandType.InlineVar:at+=2;break;
                    case OperandType.InlineI8:case OperandType.InlineR:at+=8;break;
                    case OperandType.InlineSwitch:int count=BitConverter.ToInt32(il,at);at+=4+count*4;break;
                    default:at+=4;break;
                }
                if(at>il.Length)throw new InvalidOperationException("回调 IL 不完整");
            }
            return false;
        }
    }
}
