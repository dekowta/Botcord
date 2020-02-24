using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Xml.Linq;

namespace Botcord.Core.Extensions
{
    public static class XmlLinqExtensions
    {
        public static XElement CreateElement(this XDocument doc, string name)
        {
            XElement element = new XElement(name);
            doc.Add(element);
            return element;
        }

        public static XElement CreateElement(this XElement element, string name)
        {
            XElement childElement = new XElement(name);
            element.Add(childElement);
            return childElement;
        }

        public static XAttribute CreateAttribute(this XElement element, string name, string value)
        {
            XAttribute attribute = new XAttribute(name, value);
            element.Add(attribute);
            return attribute;
        }

        public static XElement FindElementByAttribute(this XElement element, string attribute, string value)
        {
            XElement found = element.Elements().FirstOrDefault(e =>
            {
                string name = string.Empty;
                if (e.Attribute(attribute) != null)
                    name = e.Attribute(attribute).Value;
                if (name == value)
                    return true;
                else
                    return false;
            });

            return found;
        }

        public static bool HasAttribute(this XElement element, string name)
        {
            return element.Attribute(name) != null;
        }

        public static bool UpdateAttribute<T>(this XElement element, string name, T value)
        {
            if (element.HasAttribute(name))
            {
                element.Attribute(name).Value = value.ToString();
                return true;
            }

            return false;
        }

        public static string GetAttribute(this XElement element, string name)
        {
            if (!element.HasAttributes)
                return string.Empty;
            if (element.Attribute(name) == null)
                return string.Empty;

            return element.Attribute(name).Value;
        }

        public static T GetAttributeOrDefault<T>(this XElement element, string name)
        {
            if (!element.HasAttributes)
                return default(T);
            if (element.Attribute(name) == null)
                return default(T);

            string attribute = element.Attribute(name).Value;
            return (T)Convert.ChangeType(attribute, typeof(T));
        }

        public static bool TryGetAttribute<T>(this XElement element, string name, out T item)
        {
            if (!element.HasAttributes)
            { item = default(T); return false; }
            if (element.Attribute(name) == null)
            { item = default(T); return false; }

            string attribute = element.Attribute(name).Value;
            try
            {
                item = (T)Convert.ChangeType(attribute, typeof(T));
                return true;
            }
            catch
            {
                item = default(T);
                return false;
            }
        }

        public static List<XElement> ListElements(this XElement element)
        {
            return element.Elements().ToList();
        }
    }
}
