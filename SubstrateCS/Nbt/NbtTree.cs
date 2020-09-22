using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using Substrate.Core;

namespace Substrate.Nbt
{
    /// <summary>
    /// Contains the root node of an NBT tree and handles IO of tree nodes.
    /// </summary>
    /// <remarks>
    /// NBT, or Named Byte Tag, is a tree-based data structure for storing most Minecraft data.
    /// NBT_Tree is more of a helper class for NBT trees that handles reading and writing nodes to data streams.
    /// Most of the API takes a TagValue or derived node as the root of the tree, rather than an NBT_Tree object itself.
    /// </remarks>
    public class NbtTree : ICopyable<NbtTree>
    {
        private Stream _stream = null;
        private TagNodeCompound _root = null;
        private string _rootName = "";

        private static TagNodeNull _nulltag = new TagNodeNull();

        /// <summary>
        /// Gets the root node of this tree.
        /// </summary>
        public TagNodeCompound Root
        {
            get { return _root; }
        }

        /// <summary>
        /// Gets or sets the name of the tree's root node.
        /// </summary>
        public string Name
        {
            get { return _rootName; }
            set { _rootName = value; }
        }

        /// <summary>
        /// Constructs a wrapper around a new NBT tree with an empty root node.
        /// </summary>
        public NbtTree ()
        {
            _root = new TagNodeCompound();
        }

        /// <summary>
        /// Constructs a wrapper around another NBT tree.
        /// </summary>
        /// <param name="tree">The root node of an NBT tree.</param>
        public NbtTree (TagNodeCompound tree)
        {
            _root = tree;
        }

        /// <summary>
        /// Constructs a wrapper around another NBT tree and gives it a name.
        /// </summary>
        /// <param name="tree">The root node of an NBT tree.</param>
        /// <param name="name">The name for the root node.</param>
        public NbtTree (TagNodeCompound tree, string name)
        {
            _root = tree;
            _rootName = name;
        }

        /// <summary>
        /// Constructs and wrapper around a new NBT tree parsed from a source data stream.
        /// </summary>
        /// <param name="s">An open, readable data stream containing NBT data.</param>
        public NbtTree (Stream s)
        {
            ReadFrom(s);
        }

        /// <summary>
        /// Rebuild the internal NBT tree from a source data stream.
        /// </summary>
        /// <param name="s">An open, readable data stream containing NBT data.</param>
        public void ReadFrom (Stream s)
        {
            if (s != null) {
                _stream = s;
                _root = ReadRoot();
                _stream = null;
            }
        }

        /// <summary>
        /// Writes out the internal NBT tree to a destination data stream.
        /// </summary>
        /// <param name="s">An open, writable data stream.</param>
        public void WriteTo (Stream s)
        {
            if (s != null) {
                _stream = s;

                if (_root != null) {
                    WriteTag(_rootName, _root);
                }

                _stream = null;
            }
        }

        private TagNode ReadValue (TagType type)
        {
            switch (type) {
                case TagType.TAG_END:
                    return null;

                case TagType.TAG_BYTE:
                    return ReadByte();

                case TagType.TAG_SHORT:
                    return ReadShort();

                case TagType.TAG_INT:
                    return ReadInt();

                case TagType.TAG_LONG:
                    return ReadLong();

                case TagType.TAG_FLOAT:
                    return ReadFloat();

                case TagType.TAG_DOUBLE:
                    return ReadDouble();

                case TagType.TAG_BYTE_ARRAY:
                    return ReadByteArray();

                case TagType.TAG_STRING:
                    return ReadString();

                case TagType.TAG_LIST:
                    return ReadList();

                case TagType.TAG_COMPOUND:
                    return ReadCompound();

                case TagType.TAG_INT_ARRAY:
                    return ReadIntArray();
                    
                case TagType.TAG_LONG_ARRAY:
                    return ReadLongArray();

                case TagType.TAG_SHORT_ARRAY:
                    return ReadShortArray();
            }

            throw new Exception();
        }

        private TagNode ReadByte ()
        {
            var gzByte = _stream.ReadByte();
            if (gzByte == -1) {
                throw new NBTException(NBTException.MSG_GZIP_ENDOFSTREAM);
            }

            var val = new TagNodeByte((byte)gzByte);

            return val;
        }

        private TagNode ReadShort ()
        {
            var gzBytes = new byte[2];
            _stream.Read(gzBytes, 0, 2);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            var val = new TagNodeShort(BitConverter.ToInt16(gzBytes, 0));

            return val;
        }

        private TagNode ReadInt ()
        {
            var gzBytes = new byte[4];
            _stream.Read(gzBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            var val = new TagNodeInt(BitConverter.ToInt32(gzBytes, 0));

            return val;
        }

        private TagNode ReadLong ()
        {
            var gzBytes = new byte[8];
            _stream.Read(gzBytes, 0, 8);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            var val = new TagNodeLong(BitConverter.ToInt64(gzBytes, 0));

            return val;
        }

        private TagNode ReadFloat ()
        {
            var gzBytes = new byte[4];
            _stream.Read(gzBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            var val = new TagNodeFloat(BitConverter.ToSingle(gzBytes, 0));

            return val;
        }

        private TagNode ReadDouble ()
        {
            var gzBytes = new byte[8];
            _stream.Read(gzBytes, 0, 8);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            var val = new TagNodeDouble(BitConverter.ToDouble(gzBytes, 0));

            return val;
        }

        private TagNode ReadByteArray ()
        {
            var lenBytes = new byte[4];
            _stream.Read(lenBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            var length = BitConverter.ToInt32(lenBytes, 0);
            if (length < 0) {
                throw new NBTException(NBTException.MSG_READ_NEG);
            }

            var data = new byte[length];
            _stream.Read(data, 0, length);

            var val = new TagNodeByteArray(data);

            return val;
        }

        private TagNode ReadString ()
        {
            var lenBytes = new byte[2];
            _stream.Read(lenBytes, 0, 2);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            var len = BitConverter.ToInt16(lenBytes, 0);
            if (len < 0) {
                throw new NBTException(NBTException.MSG_READ_NEG);
            }

            var strBytes = new byte[len];
            _stream.Read(strBytes, 0, len);

            var str = Encoding.UTF8;

            var val = new TagNodeString(str.GetString(strBytes));

            return val;
        }

        private TagNode ReadList ()
        {
            var gzByte = _stream.ReadByte();
            if (gzByte == -1) {
                throw new NBTException(NBTException.MSG_GZIP_ENDOFSTREAM);
            }

            var val = new TagNodeList((TagType)gzByte);
            if (val.ValueType > (TagType)Enum.GetValues(typeof(TagType)).GetUpperBound(0)) {
                throw new NBTException(NBTException.MSG_READ_TYPE);
            }

            var lenBytes = new byte[4];
            _stream.Read(lenBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            var length = BitConverter.ToInt32(lenBytes, 0);
            if (length < 0) {
                throw new NBTException(NBTException.MSG_READ_NEG);
            }

            if (val.ValueType == TagType.TAG_END)
                return new TagNodeList(TagType.TAG_BYTE);

            for (var i = 0; i < length; i++) {
                val.Add(ReadValue(val.ValueType));
            }

            return val;
        }

        private TagNode ReadCompound ()
        {
            var val = new TagNodeCompound();

            while (ReadTag(val)) ;

            return val;
        }

        private TagNode ReadIntArray ()
        {
            var lenBytes = new byte[4];
            _stream.Read(lenBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            var length = BitConverter.ToInt32(lenBytes, 0);
            if (length < 0) {
                throw new NBTException(NBTException.MSG_READ_NEG);
            }

            var data = new int[length];
            var buffer = new byte[4];
            for (var i = 0; i < length; i++) {
                _stream.Read(buffer, 0, 4);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }
                data[i] = BitConverter.ToInt32(buffer, 0);
            }

            var val = new TagNodeIntArray(data);

            return val;
        }

        private TagNode ReadLongArray ()
        {
            var lenBytes = new byte[4];
            _stream.Read(lenBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            var length = BitConverter.ToInt32(lenBytes, 0);
            if (length < 0) {
                throw new NBTException(NBTException.MSG_READ_NEG);
            }

            var data = new long[length];
            var buffer = new byte[8];
            for (var i = 0; i < length; i++) {
                _stream.Read(buffer, 0, 8);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }
                data[i] = BitConverter.ToInt64(buffer, 0);
            }

            var val = new TagNodeLongArray(data);

            return val;
        }

        private TagNode ReadShortArray ()
        {
            var lenBytes = new byte[4];
            _stream.Read(lenBytes, 0, 4);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            var length = BitConverter.ToInt32(lenBytes, 0);
            if (length < 0) {
                throw new NBTException(NBTException.MSG_READ_NEG);
            }

            var data = new short[length];
            var buffer = new byte[2];
            for (var i = 0; i < length; i++) {
                _stream.Read(buffer, 0, 2);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }
                data[i] = BitConverter.ToInt16(buffer, 0);
            }

            var val = new TagNodeShortArray(data);

            return val;
        }

        private TagNodeCompound ReadRoot ()
        {
            var type = (TagType)_stream.ReadByte();
            if (type == TagType.TAG_COMPOUND) {
                _rootName = ReadString().ToTagString().Data; // name
                return ReadValue(type) as TagNodeCompound;
            }

            return null;
        }

        private bool ReadTag (TagNodeCompound parent)
        {
            var type = (TagType)_stream.ReadByte();
            if (type != TagType.TAG_END) {
                var name = ReadString().ToTagString().Data;
                parent[name] = ReadValue(type);
                return true;
            }

            return false;
        }

        private void WriteValue (TagNode val)
        {
            switch (val.GetTagType()) {
                case TagType.TAG_END:
                    break;

                case TagType.TAG_BYTE:
                    WriteByte(val.ToTagByte());
                    break;

                case TagType.TAG_SHORT:
                    WriteShort(val.ToTagShort());
                    break;

                case TagType.TAG_INT:
                    WriteInt(val.ToTagInt());
                    break;

                case TagType.TAG_LONG:
                    WriteLong(val.ToTagLong());
                    break;

                case TagType.TAG_FLOAT:
                    WriteFloat(val.ToTagFloat());
                    break;

                case TagType.TAG_DOUBLE:
                    WriteDouble(val.ToTagDouble());
                    break;

                case TagType.TAG_BYTE_ARRAY:
                    WriteByteArray(val.ToTagByteArray());
                    break;

                case TagType.TAG_STRING:
                    WriteString(val.ToTagString());
                    break;

                case TagType.TAG_LIST:
                    WriteList(val.ToTagList());
                    break;

                case TagType.TAG_COMPOUND:
                    WriteCompound(val.ToTagCompound());
                    break;

                case TagType.TAG_INT_ARRAY:
                    WriteIntArray(val.ToTagIntArray());
                    break;

                case TagType.TAG_LONG_ARRAY:
                    WriteLongArray(val.ToTagLongArray());
                    break;

                case TagType.TAG_SHORT_ARRAY:
                    WriteShortArray(val.ToTagShortArray());
                    break;
            }
        }

        private void WriteByte (TagNodeByte val)
        {
            _stream.WriteByte(val.Data);
        }

        private void WriteShort (TagNodeShort val)
        {
            var gzBytes = BitConverter.GetBytes(val.Data);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            _stream.Write(gzBytes, 0, 2);
        }

        private void WriteInt (TagNodeInt val)
        {
            var gzBytes = BitConverter.GetBytes(val.Data);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            _stream.Write(gzBytes, 0, 4);
        }

        private void WriteLong (TagNodeLong val)
        {
            var gzBytes = BitConverter.GetBytes(val.Data);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            _stream.Write(gzBytes, 0, 8);
        }

        private void WriteFloat (TagNodeFloat val)
        {
            var gzBytes = BitConverter.GetBytes(val.Data);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            _stream.Write(gzBytes, 0, 4);
        }

        private void WriteDouble (TagNodeDouble val)
        {
            var gzBytes = BitConverter.GetBytes(val.Data);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(gzBytes);
            }

            _stream.Write(gzBytes, 0, 8);
        }

        private void WriteByteArray (TagNodeByteArray val)
        {
            var lenBytes = BitConverter.GetBytes(val.Length);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            _stream.Write(lenBytes, 0, 4);
            _stream.Write(val.Data, 0, val.Length);
        }

        private void WriteString (TagNodeString val)
        {
            var str = Encoding.UTF8;
            var gzBytes = str.GetBytes(val.Data);

            var lenBytes = BitConverter.GetBytes((short)gzBytes.Length);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            _stream.Write(lenBytes, 0, 2);

            _stream.Write(gzBytes, 0, gzBytes.Length);
        }

        private void WriteList (TagNodeList val)
        {
            var lenBytes = BitConverter.GetBytes(val.Count);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            _stream.WriteByte((byte)val.ValueType);
            _stream.Write(lenBytes, 0, 4);

            foreach (var v in val) {
                WriteValue(v);
            }
        }

        private void WriteCompound (TagNodeCompound val)
        {
            foreach (var item in val) {
                WriteTag(item.Key, item.Value);
            }

            WriteTag(null, _nulltag);
        }

        private void WriteIntArray (TagNodeIntArray val)
        {
            var lenBytes = BitConverter.GetBytes(val.Length);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            _stream.Write(lenBytes, 0, 4);

            var data = new byte[val.Length * 4];
            for (var i = 0; i < val.Length; i++) {
                var buffer = BitConverter.GetBytes(val.Data[i]);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }
                Array.Copy(buffer, 0, data, i * 4, 4);
            }

            _stream.Write(data, 0, data.Length);
        }

        private void WriteLongArray (TagNodeLongArray val)
        {
            var lenBytes = BitConverter.GetBytes(val.Length);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            _stream.Write(lenBytes, 0, 4);

            var data = new byte[val.Length * 8];
            for (var i = 0; i < val.Length; i++) {
                var buffer = BitConverter.GetBytes(val.Data[i]);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }
                Array.Copy(buffer, 0, data, i * 8, 8);
            }

            _stream.Write(data, 0, data.Length);
        }

        private void WriteShortArray (TagNodeShortArray val)
        {
            var lenBytes = BitConverter.GetBytes(val.Length);

            if (BitConverter.IsLittleEndian) {
                Array.Reverse(lenBytes);
            }

            _stream.Write(lenBytes, 0, 4);

            var data = new byte[val.Length * 2];
            for (var i = 0; i < val.Length; i++) {
                var buffer = BitConverter.GetBytes(val.Data[i]);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(buffer);
                }
                Array.Copy(buffer, 0, data, i * 2, 2);
            }

            _stream.Write(data, 0, data.Length);
        }

        private void WriteTag (string name, TagNode val)
        {
            _stream.WriteByte((byte)val.GetTagType());

            if (val.GetTagType() != TagType.TAG_END) {
                WriteString(name);
                WriteValue(val);
            }
        }

        #region ICopyable<NBT_Tree> Members

        /// <summary>
        /// Creates a deep copy of the NBT_Tree and underlying nodes.
        /// </summary>
        /// <returns>A new NBT_tree.</returns>
        public NbtTree Copy ()
        {
            var tree = new NbtTree();
            tree._root = _root.Copy() as TagNodeCompound;

            return tree;
        }

        #endregion
    }

    // TODO: Revise exceptions?
    public class NBTException : Exception
    {
        public const String MSG_GZIP_ENDOFSTREAM = "Gzip Error: Unexpected end of stream";

        public const String MSG_READ_NEG = "Read Error: Negative length";
        public const String MSG_READ_TYPE = "Read Error: Invalid value type";

        public NBTException () { }

        public NBTException (String msg) : base(msg) { }

        public NBTException (String msg, Exception innerException) : base(msg, innerException) { }
    }

    public class InvalidNBTObjectException : Exception { }

    public class InvalidTagException : Exception { }

    public class InvalidValueException : Exception { }
}
