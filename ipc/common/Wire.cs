// Shared by independent tools. Compatible with the game's Mono profile and .NET 8.
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BD2.LocalIpc
{
    public static class Wire
    {
        public static string RuntimeName(System.Reflection.Assembly assembly){var name=assembly.GetName().Name;var at=name.LastIndexOf(".Hot.",StringComparison.Ordinal);return at<0?name:name.Substring(0,at);}
        public const int Version = 1;
        public const int MaximumFrame = 16 * 1024 * 1024;
        public const int TimeoutMilliseconds = 1500;
        public static string Endpoint(int pid, long started) { return "BD2.Local.v1." + pid + "." + started; }
        public static string Channel(string root)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd('\\', '/').ToUpperInvariant()))).Replace("-", "");
        }
        public static byte[] Encode(Action<BinaryWriter> write)
        {
            using (var buffer = new MemoryStream())
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true)) { write(writer); writer.Flush(); }
                if (buffer.Length > MaximumFrame) throw new InvalidDataException("IPC message is too large");
                return buffer.ToArray();
            }
        }
        public static byte[] ReadFrame(Stream stream)
        {
            var size = ReadExactly(stream, 4); int count = BitConverter.ToInt32(size, 0);
            if (count < 1 || count > MaximumFrame) throw new InvalidDataException("Invalid IPC frame length");
            return ReadExactly(stream, count);
        }
        public static byte[] ReadExactly(Stream stream, int size)
        {
            var result = new byte[size]; int offset = 0;
            while (offset < size) { int read = stream.Read(result, offset, size - offset); if (read == 0) throw new EndOfStreamException(); offset += read; }
            return result;
        }
        public static void WriteFrame(Stream stream, byte[] data)
        {
            if (data == null || data.Length < 1 || data.Length > MaximumFrame) throw new InvalidDataException("Invalid IPC frame length");
            var size = BitConverter.GetBytes(data.Length); stream.Write(size, 0, size.Length); stream.Write(data, 0, data.Length); stream.Flush();
        }
        public static byte[] ReadBytes(BinaryReader reader)
        {
            int size = reader.ReadInt32();
            if (size < 0 || size > MaximumFrame || size > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Invalid IPC payload length");
            return ReadExactly(reader.BaseStream, size);
        }
        public static void Bytes(BinaryWriter writer, byte[] value) { writer.Write(value.Length); writer.Write(value); }
        public static void End(BinaryReader reader) { if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing IPC data"); }
        public static string Nonce() { var bytes = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes); return Convert.ToBase64String(bytes); }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
        public static void VerifyServer(NamedPipeClientStream pipe, int expectedPid, long expectedStart)
        {
            uint actual;
            if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out actual) || actual != expectedPid) throw new IOException("IPC server process mismatch");
            using (var p = Process.GetProcessById(expectedPid))
                if (p.StartTime.ToUniversalTime().Ticks != expectedStart) throw new IOException("IPC game instance changed");
        }
    }
    public sealed class LeaseRevokedException : IOException
    { public LeaseRevokedException() : base("Control was handed to another window or tool. Connect explicitly to take control again.") { } }
}
