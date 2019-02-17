using System;
using System.IO;
using System.Xml;

namespace Fetcho.Common
{
    /// <summary>
    /// Helper class to read base 64 XML elements as streams
    /// </summary>
    /// <remarks>Note it doesn't dispose the underlying stream</remarks>
    public class XmlBase64ElementStream : Stream
    {
        private XmlReader xmlReader;

        public XmlBase64ElementStream(XmlReader xmlReader)
        {
            this.xmlReader = xmlReader;
            if (this.xmlReader.NodeType != XmlNodeType.Element)
                throw new Exception("Needs to be on the Element node. On " + this.xmlReader.NodeType);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => 0; set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (xmlReader.NodeType != XmlNodeType.Element && xmlReader.NodeType != XmlNodeType.Text)
                return 0;
            return xmlReader.ReadElementContentAsBase64(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }


    }
}