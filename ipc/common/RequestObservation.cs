using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace BD2.LocalIpc
{
    // Bookkeeping only. Never modifies the game's queue, callbacks, responses or retries.
    public sealed class ObservedRequest
    {
        public object Request, Target, Tag;
        public MethodBase Callback;
        public string Kind;
        public long Started;
    }
    public sealed class RequestObservation
    {
        public const int TimeoutSeconds = 30;
        public const int Capacity = 128;
        private readonly List<ObservedRequest> entries = new List<ObservedRequest>();
        public int Count { get { return entries.Count; } }
        public long Oldest { get { return entries.Count == 0 ? 0 : entries.Min(x => x.Started); } }
        public ObservedRequest Add(object request, Delegate callback, string kind, long now, object tag = null)
        {
            if (request == null) throw new ArgumentNullException("request");
            var item = new ObservedRequest { Request = request, Callback = callback == null ? null : callback.Method,
                Target = callback == null ? null : callback.Target, Kind = kind, Started = now, Tag = tag };
            // A bounded observer cannot hold the native sender hostage. Its owner receives the
            // displaced record and preserves any transaction in its own reconciliation state.
            ObservedRequest displaced = null;
            if (entries.Count == Capacity) { displaced = entries[0]; entries.RemoveAt(0); }
            entries.Add(item);
            return displaced;
        }
        public ObservedRequest Complete(MethodBase method, object target)
        {
            var matches = entries.Where(x => x.Callback == method && ReferenceEquals(x.Target, target)).ToArray();
            // No FIFO by request kind: an old response must not acknowledge a newer operation.
            if (matches.Length != 1) return null;
            entries.Remove(matches[0]); return matches[0];
        }
        public ObservedRequest CompleteRequest(object request)
        {
            var item = entries.FirstOrDefault(x => ReferenceEquals(x.Request, request));
            if (item != null) entries.Remove(item);
            return item;
        }
        public ObservedRequest[] Reconcile(long now, bool nativeIdle)
        {
            if (!nativeIdle) return new ObservedRequest[0];
            var retired = entries.Where(x => now >= x.Started && now - x.Started >= TimeSpan.FromSeconds(TimeoutSeconds).Ticks).ToArray();
            foreach (var item in retired) entries.Remove(item);
            return retired;
        }
    }
    public sealed class NativeNetworkProbe
    {
        private Type type;
        private FieldInfo[] queues, batches;
        private static Type Packet(Type container)
        {
            if (!container.IsGenericType) return null;
            var definition = container.GetGenericTypeDefinition();
            var args = container.GetGenericArguments();
            if (definition == typeof(Queue<>)) return args[0];
            if (definition == typeof(Dictionary<,>) && args[0] == typeof(string)) return args[1];
            return null;
        }
        private static bool IsPacket(Type t)
        {
            if (t == null) return false;
            for (var next = t; next != null; next = next.BaseType)
                if (next.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Any(f => f.FieldType.FullName == "Google.Protobuf.IMessage")) return true;
            return false;
        }
        public bool TryIdle(object manager, out bool idle)
        {
            idle = false;
            if (manager == null) return false;
            try
            {
                if (manager.GetType() != type)
                {
                    type = manager.GetType();
                    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(f => IsPacket(Packet(f.FieldType))).ToArray();
                    queues = fields.Where(f => f.FieldType.GetGenericTypeDefinition() == typeof(Queue<>)).ToArray();
                    batches = fields.Where(f => f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)).ToArray();
                }
                // Both normal and batched sends must be observed. Unknown layout is not idle.
                if (queues.Length != 1 || batches.Length != 1) return false;
                idle = Empty(queues[0].GetValue(manager)) && Empty(batches[0].GetValue(manager));
                return true;
            }
            catch (Exception) { return false; }
        }
        private static bool Empty(object value)
        {
            if (value == null) return false;
            var collection = value as ICollection;
            return collection != null && collection.Count == 0;
        }
    }
}
