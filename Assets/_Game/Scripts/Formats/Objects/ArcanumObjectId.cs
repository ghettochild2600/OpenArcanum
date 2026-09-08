using System;

namespace Arcanum.Formats.Objects
{
    /// <summary>The serialized variants of Arcanum's 24-byte <c>ObjectID</c>.</summary>
    public enum ArcanumObjectIdType : short
    {
        Handle = -2,
        Blocked = -1,
        Null = 0,
        Authored = 1,
        Guid = 2,
        Positional = 3,
    }

    /// <summary>
    /// Semantic identity for one serialized <c>ObjectID</c>. Equality deliberately ignores padding and
    /// unused union bytes, matching the original engine's <c>objid_is_equal</c> implementation.
    /// </summary>
    public readonly struct ArcanumObjectId : IEquatable<ArcanumObjectId>
    {
        public const int SerializedSize = 24;

        public ArcanumObjectIdType Type { get; }
        public short RawType { get; }
        public string Key { get; }
        public int? MapNumber { get; }

        public bool IsPersistent => RawType >= (short)ArcanumObjectIdType.Authored
                                    && RawType <= (short)ArcanumObjectIdType.Positional;
        public bool IsRuntimeHandle => Type == ArcanumObjectIdType.Handle;
        public bool IsPrototypeMarker => Type == ArcanumObjectIdType.Blocked;
        public bool IsNull => Type == ArcanumObjectIdType.Null;
        public bool IsKnownType => RawType >= (short)ArcanumObjectIdType.Handle
                                   && RawType <= (short)ArcanumObjectIdType.Positional;

        private ArcanumObjectId(ArcanumObjectIdType type, short rawType, string key, int? mapNumber = null)
        {
            Type = type;
            RawType = rawType;
            Key = key;
            MapNumber = mapNumber;
        }

        public static ArcanumObjectId FromBytes(byte[] bytes)
        {
            if (bytes == null) return default;
            if (bytes.Length != SerializedSize)
                throw new ArgumentException($"ObjectID must be exactly {SerializedSize} bytes.", nameof(bytes));

            short rawType = ReadInt16(bytes, 0);
            var type = (ArcanumObjectIdType)rawType;
            switch (type)
            {
                case ArcanumObjectIdType.Handle:
                    return new ArcanumObjectId(type, rawType, $"Handle_{ReadUInt64(bytes, 8):X16}");
                case ArcanumObjectIdType.Blocked:
                    return new ArcanumObjectId(type, rawType, "Blocked");
                case ArcanumObjectIdType.Null:
                    return new ArcanumObjectId(type, rawType, "NULL");
                case ArcanumObjectIdType.Authored:
                    return new ArcanumObjectId(type, rawType, $"A_{ReadUInt32(bytes, 8):X8}");
                case ArcanumObjectIdType.Guid:
                    return new ArcanumObjectId(type, rawType,
                        $"G_{bytes[8]:X2}{bytes[9]:X2}{bytes[10]:X2}{bytes[11]:X2}_" +
                        $"{bytes[12]:X2}{bytes[13]:X2}_{bytes[14]:X2}{bytes[15]:X2}_" +
                        $"{bytes[16]:X2}{bytes[17]:X2}_{bytes[18]:X2}{bytes[19]:X2}" +
                        $"{bytes[20]:X2}{bytes[21]:X2}{bytes[22]:X2}{bytes[23]:X2}");
                case ArcanumObjectIdType.Positional:
                {
                    uint x = ReadUInt32(bytes, 8);
                    uint y = ReadUInt32(bytes, 12);
                    uint temporaryId = ReadUInt32(bytes, 16);
                    int map = ReadInt32(bytes, 20);
                    return new ArcanumObjectId(type, rawType,
                        $"P_{x:X8}_{y:X8}_{temporaryId:X8}_{unchecked((uint)map):X8}", map);
                }
                default:
                    return new ArcanumObjectId(type, rawType,
                        $"Invalid_{unchecked((ushort)rawType):X4}_{BitConverter.ToString(bytes)}");
            }
        }

        /// <summary>Creates the engine's persistent identity for a static sector record after load.
        /// This is <c>objid_id_perm_by_load_order</c>: full location, sector-list temp id and map id.</summary>
        public static ArcanumObjectId CreatePositional(long location, int temporaryId, int mapNumber)
        {
            uint x = unchecked((uint)location);
            uint y = unchecked((uint)(location >> 32));
            return new ArcanumObjectId(
                ArcanumObjectIdType.Positional,
                (short)ArcanumObjectIdType.Positional,
                $"P_{x:X8}_{y:X8}_{unchecked((uint)temporaryId):X8}_{unchecked((uint)mapNumber):X8}",
                mapNumber);
        }

        /// <summary>Creates a stable GUID identity for a runtime object whose identity is not sector-authored.</summary>
        public static ArcanumObjectId CreateGuid(Guid guid)
        {
            byte[] guidBytes = guid.ToByteArray();
            var serialized = new byte[SerializedSize];
            serialized[0] = (byte)ArcanumObjectIdType.Guid;
            Array.Copy(guidBytes, 0, serialized, 8, guidBytes.Length);
            return FromBytes(serialized);
        }

        public bool Equals(ArcanumObjectId other)
            => RawType == other.RawType && (IsNull || string.Equals(Key, other.Key, StringComparison.Ordinal));

        public override bool Equals(object obj) => obj is ArcanumObjectId other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (RawType * 397) ^ (IsNull || Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key));
            }
        }

        public override string ToString() => Key ?? "<absent>";

        public static bool operator ==(ArcanumObjectId left, ArcanumObjectId right) => left.Equals(right);
        public static bool operator !=(ArcanumObjectId left, ArcanumObjectId right) => !left.Equals(right);

        private static short ReadInt16(byte[] bytes, int offset)
            => unchecked((short)(bytes[offset] | (bytes[offset + 1] << 8)));

        private static int ReadInt32(byte[] bytes, int offset) => unchecked((int)ReadUInt32(bytes, offset));

        private static uint ReadUInt32(byte[] bytes, int offset)
            => (uint)(bytes[offset]
                      | (bytes[offset + 1] << 8)
                      | (bytes[offset + 2] << 16)
                      | (bytes[offset + 3] << 24));

        private static ulong ReadUInt64(byte[] bytes, int offset)
            => ReadUInt32(bytes, offset) | ((ulong)ReadUInt32(bytes, offset + 4) << 32);
    }
}
