#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Generic;
using TwinCAT.TypeSystem;

namespace OC.Communication
{
    public class TcAdsDevice
    {
        public TcAdsDevice(string name, string type)
        {
            Name = name;
            Type = type;
        }
        
        public string Name { get; }
        public string Type { get; }
        public List<ISymbol> Symbols { get; } = new List<ISymbol>();
        public bool IsOpen { get; set; }
    }
}
#endif
