using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OC.UI.ComponentLayout
{
    public static class ComponentLayoutXmlSerializer
    {
        public static void Write(string filePath, ComponentLayoutData data)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new XmlSerializer(typeof(ComponentLayoutData));
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false)
            };

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using var writer = XmlWriter.Create(stream, settings);
            serializer.Serialize(writer, data);
        }

        public static ComponentLayoutData Read(string filePath)
        {
            var serializer = new XmlSerializer(typeof(ComponentLayoutData));
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return (ComponentLayoutData)serializer.Deserialize(stream);
        }
    }
}
