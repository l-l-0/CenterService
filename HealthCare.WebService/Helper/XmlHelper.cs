//------------------------------------------------------------------------------
// @license
// Copyright © 北京盛福瑞安科技有限责任公司. All Rights Reserved.
// 
// Use of this source code is governed by an BSD-style.
//------------------------------------------------------------------------------

using System.IO;
using System.Xml.Serialization;

#pragma warning disable CS1591

namespace HealthCare.WebService.Helper
{
    public class Serializer
    {
        /// <summary>
        ///     Deserializes the specified XML document to Object
        /// </summary>
        public T Deserialize<T>(string xml) where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var reader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        /// <summary>
        ///     Serializes the specified Object to XML document
        /// </summary>
        public string Serialize<T>(T obj)
        {
            var serializer = new XmlSerializer(obj.GetType());
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, obj);
                return writer.ToString();
            }
        }
    }
}