using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OC.Communication;

namespace PythonLink
{
    /// <summary>
    /// Python 端看到的「過程映像」：所有裝置 Link 的變數依序排進兩塊 byte buffer。
    /// Input（Control）由 Python 寫、Unity 讀；Output（Status）由 Unity 寫、Python 讀。
    /// 版面由場景裡實際的 Link 自動算出，Python 從 manifest 得知每個變數的位置與型別。
    /// </summary>
    public class PythonLinkBuffer : IClientBuffer
    {
        public bool IsConnected { get; private set; }
        public byte[] InputBytes { get; private set; } = Array.Empty<byte>();
        public byte[] OutputBytes { get; private set; } = Array.Empty<byte>();
        public List<ClientVariable> InputVariables { get; } = new();
        public List<ClientVariable> OutputVariables { get; } = new();

        private readonly List<VariableInfo> _info = new();
        private readonly List<DeviceInfo> _devices = new();

        private struct VariableInfo
        {
            public string Name;
            public bool IsInput;
            public int Offset;
            public int Size;
            public string DType;
            public int Count;
        }

        private struct DeviceInfo
        {
            public string Path;
            public string Type;
            public string Name;
            public string Component;
        }

        // 系統 Link 的變數名稱與欄位名稱不一致（TimeScaling ↔ TimeScale）
        private static readonly Dictionary<string, Type> SpecialFields = new()
        {
            { "TimeScaling", typeof(float) },
        };

        private static readonly MethodInfo DescriptionsMethod =
            typeof(Link).GetMethod("GetClientVariableDescriptions", BindingFlags.Instance | BindingFlags.NonPublic);

        public void Build(IEnumerable<Link> links)
        {
            _info.Clear();
            _devices.Clear();
            InputVariables.Clear();
            OutputVariables.Clear();

            var inOffset = 0;
            var outOffset = 0;
            var pending = new List<(string name, bool isInput, int offset, int size)>();

            foreach (var link in links)
            {
                if (link == null || !link.Enable) continue;
                var descriptions = (List<ClientVariableDescription>)DescriptionsMethod.Invoke(link, null);
                if (descriptions == null) continue;

                _devices.Add(new DeviceInfo
                {
                    Path = link.ClientPath,
                    Type = link.Type,
                    Name = link.Name,
                    Component = link.Component != null ? link.Component.GetType().Name : "",
                });

                foreach (var d in descriptions)
                {
                    var (size, dtype, count) = Resolve(link, d.Path);
                    var isInput = d.Direction == ClientVariableDirection.Input;
                    var offset = isInput ? inOffset : outOffset;
                    if (isInput) inOffset += size; else outOffset += size;
                    pending.Add((d.Path, isInput, offset, size));
                    _info.Add(new VariableInfo
                    {
                        Name = d.Path, IsInput = isInput, Offset = offset, Size = size, DType = dtype, Count = count,
                    });
                }
            }

            InputBytes = new byte[inOffset];
            OutputBytes = new byte[outOffset];
            foreach (var (name, isInput, offset, size) in pending)
            {
                var v = new ClientVariable(name, isInput ? InputBytes : OutputBytes, size, offset);
                (isInput ? InputVariables : OutputVariables).Add(v);
            }
        }

        private static (int size, string dtype, int count) Resolve(Link link, string path)
        {
            var field = path.Substring(path.LastIndexOf('.') + 1);
            Type type;
            var count = 1;
            var fi = link.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public);
            if (fi != null)
            {
                type = fi.FieldType;
                if (type.IsArray)
                {
                    count = fi.GetValue(link) is Array arr ? arr.Length : 0;
                    type = type.GetElementType();
                }
            }
            else if (!SpecialFields.TryGetValue(field, out type))
            {
                type = typeof(byte);
            }

            var (size, dtype) = type switch
            {
                _ when type == typeof(byte) => (1, "u8"),
                _ when type == typeof(sbyte) => (1, "i8"),
                _ when type == typeof(ushort) => (2, "u16"),
                _ when type == typeof(short) => (2, "i16"),
                _ when type == typeof(uint) => (4, "u32"),
                _ when type == typeof(int) => (4, "i32"),
                _ when type == typeof(ulong) => (8, "u64"),
                _ when type == typeof(long) => (8, "i64"),
                _ when type == typeof(float) => (4, "f32"),
                _ when type == typeof(double) => (8, "f64"),
                _ => (1, "u8"),
            };
            return (size * count, dtype, count);
        }

        /// <summary>給 Python 的版面說明（JSON）。Unity 的 JsonUtility 不支援字典與巢狀清單，這裡手組。</summary>
        public string ManifestJson(string root)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"manifest\",\"version\":1,");
            sb.Append("\"root\":").Append(Quote(root)).Append(',');
            sb.Append("\"controlSize\":").Append(InputBytes.Length).Append(',');
            sb.Append("\"statusSize\":").Append(OutputBytes.Length).Append(',');
            sb.Append("\"variables\":[");
            for (var i = 0; i < _info.Count; i++)
            {
                var v = _info[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"name\":").Append(Quote(v.Name))
                  .Append(",\"dir\":").Append(v.IsInput ? "\"control\"" : "\"status\"")
                  .Append(",\"offset\":").Append(v.Offset)
                  .Append(",\"size\":").Append(v.Size)
                  .Append(",\"dtype\":\"").Append(v.DType).Append('"')
                  .Append(",\"count\":").Append(v.Count).Append('}');
            }
            sb.Append("],\"devices\":[");
            for (var i = 0; i < _devices.Count; i++)
            {
                var d = _devices[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"path\":").Append(Quote(d.Path))
                  .Append(",\"type\":").Append(Quote(d.Type))
                  .Append(",\"name\":").Append(Quote(d.Name))
                  .Append(",\"component\":").Append(Quote(d.Component)).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public void SetConnected(bool value) => IsConnected = value;

        // IClientBuffer 其餘成員：交換由 PythonLinkClient 直接處理，這裡不需要
        public void Connect(string netId, int port) { }
        public void Disconnect() { }
        public void Read() { }
        public void Write() { }
    }
}
