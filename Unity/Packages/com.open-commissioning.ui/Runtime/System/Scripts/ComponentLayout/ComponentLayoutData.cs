using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace OC.UI.ComponentLayout
{
    [XmlRoot("ComponentLayout")]
    public class ComponentLayoutData
    {
        [XmlAttribute("version")]
        public int Version = 1;

        [XmlAttribute("sceneName")]
        public string SceneName;

        [XmlElement("Entry")]
        public List<ComponentLayoutEntry> Entries = new();
    }

    public class ComponentLayoutEntry
    {
        [XmlAttribute("id")]
        public string Id;

        [XmlAttribute("type")]
        public string Type;

        [XmlElement("LocalPosition")]
        public XmlVector3 LocalPosition;

        [XmlElement("LocalEulerAngles")]
        public XmlVector3 LocalEulerAngles;

        /// <summary>Legacy quaternion format; used when loading older save files.</summary>
        [XmlElement("LocalRotation")]
        public XmlQuaternion LocalRotation;
    }

    public class XmlVector3
    {
        [XmlAttribute("x")]
        public float X;

        [XmlAttribute("y")]
        public float Y;

        [XmlAttribute("z")]
        public float Z;

        public static XmlVector3 From(Vector3 v) => new() { X = v.x, Y = v.y, Z = v.z };

        public Vector3 ToVector3() => new(X, Y, Z);
    }

    public class XmlQuaternion
    {
        [XmlAttribute("x")]
        public float X;

        [XmlAttribute("y")]
        public float Y;

        [XmlAttribute("z")]
        public float Z;

        [XmlAttribute("w")]
        public float W;

        public static XmlQuaternion From(Quaternion q) => new() { X = q.x, Y = q.y, Z = q.z, W = q.w };

        public Quaternion ToQuaternion() => new(X, Y, Z, W);
    }
}
