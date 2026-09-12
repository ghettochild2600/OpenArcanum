using System.IO;
using Arcanum.Formats.Objects;
using NUnit.Framework;

namespace Arcanum.Formats.Tests
{
    /// <summary>
    /// Surfacing of the in-scope object fields (HP_PTS, ITEM_FLAGS, GOLD_QUANTITY, CRITTER_FLAGS, NPC_FACTION,
    /// portal/container lock-and-key, written text). Builds minimal synthetic <i>instance</i> records — only the
    /// chosen fields' change-bitmap bits are set, so the reader reads exactly those — and asserts the values land
    /// AND the cursor finishes exactly at the record end (no field-walk desync).
    /// </summary>
    public sealed class ObjectInstanceReaderTests
    {
        private const int OdTypeInt32 = 3; // ObjectFieldData.OdType code for OD_TYPE_INT32

        // Every field we newly surface must be INT32 — a wrong ordinal (non-INT32) would desync the walk.
        [Test]
        public void SurfacedFieldsAreInt32()
        {
            int[] fields = { 27, 28, 29, 46, 47, 48, 56, 57, 58, 87, 165, 186, 202, 203, 204, 218, 219,
                224, 225, 226, 231, 292 };
            foreach (int f in fields)
                Assert.That(ObjectFieldData.OdType[f], Is.EqualTo(OdTypeInt32), $"field ordinal {f} must be INT32");
        }

        [Test]
        public void ReadsGoldItemAndCommonFields()
        {
            // Gold (type 8) enumerates common + ITEM parent group + gold group.
            byte[] rec = BuildInstance(ObjectType.Gold, (27, 999), (87, 0x40), (165, 4200)); // HP_PTS, ITEM_FLAGS, GOLD_QUANTITY
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.HpPoints, Is.EqualTo(999));
            Assert.That(inst.ItemFlags, Is.EqualTo(0x40));
            Assert.That(inst.GoldQuantity, Is.EqualTo(4200));
            Assert.That(off, Is.EqualTo(rec.Length), "cursor landed exactly at the record end");
        }

        [Test]
        public void ReadsNpcCritterAndFactionFields()
        {
            byte[] rec = BuildInstance(ObjectType.Npc, (27, 3), (28, -2), (29, 7), (218, 0x01),
                (224, 4), (225, -1), (226, 9), (231, 17), (292, 5));
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.HpPoints, Is.EqualTo(3));
            Assert.That(inst.HpAdjustment, Is.EqualTo(-2));
            Assert.That(inst.HpDamage, Is.EqualTo(7));
            Assert.That(inst.FatiguePoints, Is.EqualTo(4));
            Assert.That(inst.FatigueAdjustment, Is.EqualTo(-1));
            Assert.That(inst.FatigueDamage, Is.EqualTo(9));
            Assert.That(inst.CritterFlags, Is.EqualTo(0x01));
            Assert.That(inst.Portrait, Is.EqualTo(17));
            Assert.That(inst.Faction, Is.EqualTo(5));
            Assert.That(off, Is.EqualTo(rec.Length));
        }

        [Test]
        public void ReadsPortalLockAndKey()
        {
            byte[] rec = BuildInstance(ObjectType.Portal, (46, 0x02), (47, 25), (48, 1234)); // FLAGS, LOCK_DIFFICULTY, KEY_ID
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.PortalFlags, Is.EqualTo(0x02));
            Assert.That(inst.LockDifficulty, Is.EqualTo(25));
            Assert.That(inst.KeyId, Is.EqualTo(1234));
            Assert.That(off, Is.EqualTo(rec.Length));
        }

        [Test]
        public void AbsentFieldsAreNull()
        {
            byte[] rec = BuildInstance(ObjectType.Gold); // no overridden fields
            int off = 0;
            ObjectInstance inst = ObjectInstanceReader.Read(rec, ref off);
            Assert.That(inst.HpPoints, Is.Null);
            Assert.That(inst.HpAdjustment, Is.Null);
            Assert.That(inst.HpDamage, Is.Null);
            Assert.That(inst.FatiguePoints, Is.Null);
            Assert.That(inst.FatigueAdjustment, Is.Null);
            Assert.That(inst.FatigueDamage, Is.Null);
            Assert.That(inst.GoldQuantity, Is.Null);
            Assert.That(inst.ItemFlags, Is.Null);
            Assert.That(off, Is.EqualTo(rec.Length));
        }

        [Test]
        public void ObjectIdKeysFollowEngineSemanticEquality()
        {
            byte[] first = new byte[24];
            byte[] second = new byte[24];
            first[0] = second[0] = (byte)ArcanumObjectIdType.Authored;
            first[8] = second[8] = 0x34;
            first[9] = second[9] = 0x12;
            first[4] = 0xAA;   // ignored ObjectID padding
            second[23] = 0xBB; // ignored union bytes for OID_TYPE_A

            ArcanumObjectId a = ArcanumObjectId.FromBytes(first);
            ArcanumObjectId b = ArcanumObjectId.FromBytes(second);

            Assert.That(a.IsPersistent, Is.True);
            Assert.That(a.Key, Is.EqualTo("A_00001234"));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(ObjectInstance.OidKey(first), Is.EqualTo(a.Key));
        }

        [Test]
        public void PositionalObjectIdIncludesMapScope()
        {
            byte[] bytes = new byte[24];
            bytes[0] = (byte)ArcanumObjectIdType.Positional;
            WriteInt32(bytes, 8, 101);
            WriteInt32(bytes, 12, 202);
            WriteInt32(bytes, 16, 7);
            WriteInt32(bytes, 20, 42);

            ArcanumObjectId identity = ArcanumObjectId.FromBytes(bytes);

            Assert.That(identity.Key, Is.EqualTo("P_00000065_000000CA_00000007_0000002A"));
            Assert.That(identity.MapNumber, Is.EqualTo(42));
        }

        // Builds a minimal instance: header + the type's change bitmap (bits set for the given fields) + each
        // field's INT32 value. Fields MUST be passed in field-enum order (ascending ordinal works here, since each
        // is either a common field or in one of the type's groups). Uses the real engine tables via InternalsVisibleTo.
        private static byte[] BuildInstance(ObjectType type, params (int fld, int val)[] int32Fields)
        {
            int typeRaw = (int)type;
            int ndw = ObjectFieldEngine.DwordCount[typeRaw];
            var field48 = new uint[ndw];
            foreach (var (fld, _) in int32Fields)
                field48[ObjectFieldEngine.ChangeIdx[fld]] |= ObjectFieldEngine.Mask[fld];

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(119);          // version
            w.Write(new byte[24]); // prototype_oid
            w.Write(new byte[24]); // object_oid
            w.Write(typeRaw);      // type
            w.Write((short)0);     // num_fields (instance only; value unused by the reader)
            foreach (uint d in field48) w.Write(d);
            foreach (var (_, val) in int32Fields) w.Write(val); // only the set fields, in enum order
            return ms.ToArray();
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            byte[] encoded = System.BitConverter.GetBytes(value);
            System.Buffer.BlockCopy(encoded, 0, bytes, offset, encoded.Length);
        }
    }
}
