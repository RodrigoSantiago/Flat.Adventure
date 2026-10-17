using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Game.Utils {

    public static class BinarySerializer {
        private enum ValueType : byte {
            Null = 0,
            Bool = 1,
            Number = 2,
            String = 3,
            Object = 4,
            Array = 5
        }

        private enum NumberType : byte {
            Int = 0,
            Double = 1,
            Byte = 2,
            Decimal = 3
        }

        #region Public API

        public static byte[] Write<T>(T value) {
            using var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            WriteValue(writer, value, typeof(T));
            return ms.ToArray();
        }

        public static T Read<T>(byte[] data) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var reader = new BinaryReader(data);
            var result = ReadValue(reader, typeof(T));
            return (T)result;
        }

        #endregion

        #region Writer Logic

        private static void WriteValue(BinaryWriter writer, object value, Type declaredType) {
            if (value == null) {
                writer.WriteByte((byte)ValueType.Null);
                return;
            }

            Type actualType = value.GetType();

            if (actualType == typeof(bool)) {
                writer.WriteByte((byte)ValueType.Bool);
                writer.WriteByte((bool)value ? (byte)1 : (byte)0);
            } else if (IsNumericType(actualType)) {
                writer.WriteByte((byte)ValueType.Number);
                WriteNumericValue(writer, value, actualType);
            } else if (actualType == typeof(string)) {
                writer.WriteByte((byte)ValueType.String);
                WriteString(writer, (string)value);
            } else if (IsEnumerableType(actualType)) {
                writer.WriteByte((byte)ValueType.Array);
                WriteArray(writer, value, actualType);
            } else {
                writer.WriteByte((byte)ValueType.Object);
                WriteObject(writer, value, actualType);
            }
        }

        private static void WriteNumericValue(BinaryWriter writer, object value, Type type) {
            if (IsIntegerType(type)) {
                writer.WriteByte((byte)NumberType.Int);
                long longValue = Convert.ToInt64(value);
                writer.WriteVarInt(longValue);
            } else if (type == typeof(decimal)) {
                writer.WriteByte((byte)NumberType.Decimal);
                writer.WriteDecimalLE((decimal)value);
            } else {
                writer.WriteByte((byte)NumberType.Double);
                double doubleValue = Convert.ToDouble(value);
                writer.WriteDoubleLE(doubleValue);
            }
        }

        private static void WriteString(BinaryWriter writer, string str) {
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            writer.WriteVarUInt((ulong)bytes.Length);
            writer.WriteBytes(bytes);
        }

        private static void WriteObject(BinaryWriter writer, object obj, Type type) {
            using var objMs = new MemoryStream();
            var objWriter = new BinaryWriter(objMs);

            var members = GetSerializableMembers(type);
            foreach (var member in members) {
                object val = member.GetValue(obj);
                if (val == null) continue;

                using var fieldMs = new MemoryStream();
                var fieldWriter = new BinaryWriter(fieldMs);

                // [nameLength][name] (ASCII)
                fieldWriter.WriteAsciiString(member.Name);
                // [type][value]
                WriteValue(fieldWriter, val, member.Type);

                byte[] fieldData = fieldMs.ToArray();
                objWriter.WriteVarUInt((ulong)fieldData.Length);
                objWriter.WriteBytes(fieldData);
            }

            byte[] objData = objMs.ToArray();
            writer.WriteVarUInt((ulong)objData.Length);
            writer.WriteBytes(objData);
        }

        private static void WriteArray(BinaryWriter writer, object arrayObj, Type arrayType) {
            Type elementType = GetElementType(arrayType);
            IList list = NormalizeToList(arrayObj);
            ulong count = (ulong)list.Count;

            writer.WriteVarUInt(count);

            if (elementType == typeof(byte)) {
                writer.WriteByte((byte)ValueType.Number);
                writer.WriteByte((byte)NumberType.Byte);
                if (arrayObj is byte[] rawBytes) {
                    writer.WriteBytes(rawBytes);
                } else {
                    for (int i = 0; i < list.Count; i++) {
                        writer.WriteByte((byte)list[i]);
                    }
                }
            } else if (elementType == typeof(bool)) {
                writer.WriteByte((byte)ValueType.Bool);
                int byteCount = (list.Count + 7) / 8;
                byte[] packed = new byte[byteCount];
                for (int i = 0; i < list.Count; i++) {
                    if ((bool)list[i]) {
                        packed[i / 8] |= (byte)(1 << (i % 8));
                    }
                }

                writer.WriteBytes(packed);
            } else if (IsIntegerType(elementType)) {
                writer.WriteByte((byte)ValueType.Number);
                writer.WriteByte((byte)NumberType.Int);
                for (int i = 0; i < list.Count; i++) {
                    writer.WriteVarInt(Convert.ToInt64(list[i]));
                }
            } else if (elementType == typeof(decimal)) {
                writer.WriteByte((byte)ValueType.Number);
                writer.WriteByte((byte)NumberType.Decimal);
                for (int i = 0; i < list.Count; i++) {
                    writer.WriteDecimalLE((decimal)list[i]);
                }
            }else if (elementType == typeof(float) || elementType == typeof(double)) {
                writer.WriteByte((byte)ValueType.Number);
                writer.WriteByte((byte)NumberType.Double);
                for (int i = 0; i < list.Count; i++) {
                    writer.WriteDoubleLE(Convert.ToDouble(list[i]));
                }
            } else if (elementType == typeof(string)) {
                writer.WriteByte((byte)ValueType.String);
                for (int i = 0; i < list.Count; i++) {
                    string s = (string)list[i];
                    if (s == null) {
                        writer.WriteByte(0);
                    } else {
                        writer.WriteByte(1);
                        WriteString(writer, s);
                    }
                }
            } else if (elementType == typeof(object)) {
                writer.WriteByte(0);
                for (int i = 0; i < list.Count; i++) {
                    object item = list[i];
                    WriteValue(writer, item, item?.GetType() ?? typeof(object));
                }
            } else {
                writer.WriteByte((byte)ValueType.Object);
                for (int i = 0; i < list.Count; i++) {
                    object item = list[i];
                    if (item == null) {
                        writer.WriteByte((byte)ValueType.Null);
                    } else {
                        WriteObject(writer, item, item.GetType());
                    }
                }
            }
        }

        #endregion

        #region Reader Logic

        private static object ReadValue(BinaryReader reader, Type targetType) {
            ValueType type = (ValueType)reader.ReadByte();

            switch (type) {
                case ValueType.Null:
                    return null;

                case ValueType.Bool:
                    byte b = reader.ReadByte();
                    return ConvertType(b != 0, targetType);

                case ValueType.Number:
                    return ReadNumber(reader, targetType);

                case ValueType.String:
                    string s = ReadString(reader);
                    return ConvertType(s, targetType);

                case ValueType.Object:
                    return ReadObject(reader, targetType);

                case ValueType.Array:
                    return ReadArray(reader, targetType);

                default:
                    throw new InvalidDataException($"Unknown value type: {type}");
            }
        }

        private static object ReadNumber(BinaryReader reader, Type targetType) {
            NumberType numType = (NumberType)reader.ReadByte();
            if (numType == NumberType.Int) {
                long val = reader.ReadVarInt();
                return ConvertType(val, targetType);
            } else if (numType == NumberType.Double) {
                double val = reader.ReadDoubleLE();
                return ConvertType(val, targetType);
            } else if (numType == NumberType.Decimal) {
                decimal val = reader.ReadDecimalLE();
                return ConvertType(val, targetType);
            }

            throw new InvalidDataException($"Invalid numeric type: {numType}");
        }

        private static string ReadString(BinaryReader reader) {
            ulong length = reader.ReadVarUInt();
            byte[] bytes = reader.ReadBytes((int)length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static object ReadObject(BinaryReader reader, Type targetType) {
            ulong objectSize = reader.ReadVarUInt();
            int endPosition = reader.Position + (int)objectSize;

            if (endPosition > reader.Length)
                throw new InvalidDataException("Object size overflow");

            object instance = Nullable.GetUnderlyingType(targetType) == null && targetType != typeof(object)
                ? Activator.CreateInstance(targetType)
                : Activator.CreateInstance(targetType ?? typeof(object));

            var members = GetSerializableMembersMap(instance.GetType());

            while (reader.Position < endPosition) {
                ulong fieldSize = reader.ReadVarUInt();
                int fieldEndPosition = reader.Position + (int)fieldSize;

                if (fieldEndPosition > endPosition)
                    throw new InvalidDataException("Object size overflow");

                string fieldName = reader.ReadAsciiString();

                if (members.TryGetValue(fieldName, out var member)) {
                    object fieldValue = ReadValue(reader, member.Type);
                    member.SetValue(instance, fieldValue);
                } else {
                    reader.Position = fieldEndPosition;
                }

                reader.Position = fieldEndPosition;
            }

            return instance;
        }

        private static object ReadArray(BinaryReader reader, Type targetType) {
            ulong count = reader.ReadVarUInt();
            if (count > (ulong)reader.BytesRemaining)
                throw new InvalidDataException("Unexpected end of data");

            byte headerByte = reader.ReadByte();
            Type elemType = GetElementType(targetType);

            if (headerByte == (byte)ValueType.Number) {
                NumberType numType = (NumberType)reader.ReadByte();

                if (numType == NumberType.Byte) {
                    byte[] rawBytes = reader.ReadBytes((int)count);
                    
                    if (targetType == typeof(byte[])) {
                        return rawBytes;
                    }

                    IList rawList = CreateListInstance(elemType, (int)count);
                    for (int i = 0; i < rawBytes.Length; i++) {
                        rawList.Add(ConvertType(rawBytes[i], elemType));
                    }

                    return ConvertListToTargetType(rawList, targetType, elemType);
                }

                if (numType == NumberType.Int) {
                    IList intList = CreateListInstance(elemType, (int)count);
                    for (ulong i = 0; i < count; i++) {
                        long val = reader.ReadVarInt();
                        intList.Add(ConvertType(val, elemType));
                    }

                    return ConvertListToTargetType(intList, targetType, elemType);
                }

                if (numType == NumberType.Double) {
                    IList doubleList = CreateListInstance(elemType, (int)count);
                    for (ulong i = 0; i < count; i++) {
                        double val = reader.ReadDoubleLE();
                        doubleList.Add(ConvertType(val, elemType));
                    }

                    return ConvertListToTargetType(doubleList, targetType, elemType);
                }
                
                if (numType == NumberType.Decimal) {
                    IList decimalList = CreateListInstance(elemType, (int)count);
                    for (ulong i = 0; i < count; i++) {
                        decimal val = reader.ReadDecimalLE();
                        decimalList.Add(ConvertType(val, elemType));
                    }

                    return ConvertListToTargetType(decimalList, targetType, elemType);
                }
            } else if (headerByte == (byte)ValueType.Bool) {
                IList boolList = CreateListInstance(elemType, (int)count);
                int byteCount = ((int)count + 7) / 8;
                byte[] packed = reader.ReadBytes(byteCount);
                for (int i = 0; i < (int)count; i++) {
                    bool val = (packed[i / 8] & (1 << (i % 8))) != 0;
                    boolList.Add(ConvertType(val, elemType));
                }

                return ConvertListToTargetType(boolList, targetType, elemType);
            } else if (headerByte == (byte)ValueType.String) {
                IList stringList = CreateListInstance(elemType, (int)count);
                for (ulong i = 0; i < count; i++) {
                    byte exists = reader.ReadByte();
                    stringList.Add(exists == 0 ? null : ReadString(reader));
                }

                return ConvertListToTargetType(stringList, targetType, elemType);
            } else if (headerByte == 0) { // (object[])
                IList genericList = CreateListInstance(elemType, (int)count);
                for (ulong i = 0; i < count; i++) {
                    genericList.Add(ReadValue(reader, elemType));
                }

                return ConvertListToTargetType(genericList, targetType, elemType);
            } else if (headerByte == (byte)ValueType.Object) {
                IList objList = CreateListInstance(elemType, (int)count);
                for (ulong i = 0; i < count; i++) {
                    byte typeTag = reader.PeekByte();
                    if (typeTag == (byte)ValueType.Null) {
                        reader.ReadByte();
                        objList.Add(null);
                    } else {
                        objList.Add(ReadObject(reader, elemType));
                    }
                }

                return ConvertListToTargetType(objList, targetType, elemType);
            }

            throw new InvalidDataException($"Unsupported header value: {headerByte}");
        }

        #endregion

        #region Reflection & Conversions

        private class MemberAccessor {
            public string Name { get; set; }
            public Type Type { get; set; }
            public Action<object, object> SetValue { get; set; }
            public Func<object, object> GetValue { get; set; }
        }

        private static List<MemberAccessor> GetSerializableMembers(Type type) {
            var list = new List<MemberAccessor>();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            foreach (var prop in type.GetProperties(flags)) {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (prop.GetIndexParameters().Length > 0) continue;

                list.Add(new MemberAccessor {
                    Name = prop.Name,
                    Type = prop.PropertyType,
                    GetValue = obj => prop.GetValue(obj),
                    SetValue = (obj, val) => prop.SetValue(obj, val)
                });
            }

            foreach (var field in type.GetFields(flags)) {
                if (field.IsInitOnly || field.IsLiteral) continue;

                list.Add(new MemberAccessor {
                    Name = field.Name,
                    Type = field.FieldType,
                    GetValue = obj => field.GetValue(obj),
                    SetValue = (obj, val) => field.SetValue(obj, val)
                });
            }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            return list;
        }

        private static Dictionary<string, MemberAccessor> GetSerializableMembersMap(Type type) {
            var map = new Dictionary<string, MemberAccessor>(StringComparer.Ordinal);
            foreach (var member in GetSerializableMembers(type)) {
                map[member.Name] = member;
            }

            return map;
        }

        private static bool IsNumericType(Type type) {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return IsIntegerType(type) || type == typeof(float) || type == typeof(double) || type == typeof(decimal);
        }

        private static bool IsIntegerType(Type type) {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong);
        }

        private static bool IsEnumerableType(Type type) {
            return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
        }

        private static Type GetElementType(Type type) {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return type.GetGenericArguments()[0];

            foreach (var i in type.GetInterfaces()) {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return i.GetGenericArguments()[0];
            }

            return typeof(object);
        }

        private static IList NormalizeToList(object collection) {
            if (collection is IList list) return list;
            var result = new ArrayList();
            foreach (var item in (IEnumerable)collection) result.Add(item);
            return result;
        }

        private static IList CreateListInstance(Type elemType, int capacity) {
            Type listType = typeof(List<>).MakeGenericType(elemType);
            return (IList)Activator.CreateInstance(listType, capacity);
        }

        private static object ConvertListToTargetType(IList list, Type targetType, Type elemType) {
            if (targetType.IsArray) {
                Array array = Array.CreateInstance(elemType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }

            return list;
        }

        private static object ConvertType(object value, Type targetType) {
            if (value == null) return null;

            Type actualTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (actualTarget.IsInstanceOfType(value))
                return value;

            try {
                if (actualTarget == typeof(bool)) return Convert.ToBoolean(value);
                if (actualTarget == typeof(byte)) return Convert.ToByte(value);
                if (actualTarget == typeof(sbyte)) return Convert.ToSByte(value);
                if (actualTarget == typeof(short)) return Convert.ToInt16(value);
                if (actualTarget == typeof(ushort)) return Convert.ToUInt16(value);
                if (actualTarget == typeof(int)) return Convert.ToInt32(value);
                if (actualTarget == typeof(uint)) return Convert.ToUInt32(value);
                if (actualTarget == typeof(long)) return Convert.ToInt64(value);
                if (actualTarget == typeof(ulong)) return Convert.ToUInt64(value);
                if (actualTarget == typeof(float)) return Convert.ToSingle(value);
                if (actualTarget == typeof(double)) return Convert.ToDouble(value);
                if (actualTarget == typeof(decimal)) return Convert.ToDecimal(value);
                if (actualTarget == typeof(string)) return value.ToString();

                return Convert.ChangeType(value, actualTarget);
            } catch {
                return  Activator.CreateInstance(targetType);
            }
        }

        #endregion

        #region Binary Helpers (Writer / Reader)

        private class BinaryWriter {
            private readonly Stream _stream;

            public BinaryWriter(Stream stream) => _stream = stream;

            public void WriteByte(byte value) => _stream.WriteByte(value);

            public void WriteBytes(byte[] bytes) => _stream.Write(bytes, 0, bytes.Length);

            public void WriteVarUInt(ulong value) {
                while (value >= 0x80) {
                    _stream.WriteByte((byte)((value & 0x7F) | 0x80));
                    value >>= 7;
                }

                _stream.WriteByte((byte)(value & 0x7F));
            }

            public void WriteVarInt(long value) {
                ulong zigZag = (ulong)((value << 1) ^ (value >> 63));
                WriteVarUInt(zigZag);
            }

            public void WriteDoubleLE(double value) {
                byte[] bytes = BitConverter.GetBytes(value);
                if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                _stream.Write(bytes, 0, 8);
            }
            
            public void WriteDecimalLE(decimal value) {
                int[] bits = decimal.GetBits(value);
                for (int i = 0; i < 4; i++) {
                    byte[] bytes = BitConverter.GetBytes(bits[i]);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    _stream.Write(bytes, 0, 4);
                }
            }

            public void WriteAsciiString(string str) {
                if (str.Length > 255)
                    throw new InvalidOperationException($"The field name '{str}' overflow 255 characters.");

                byte[] bytes = new byte[str.Length];
                for (int i = 0; i < str.Length; i++) {
                    char c = str[i];
                    if (c > 127)
                        throw new ArgumentException($"The field name '{str}' is not ASCII");
                    bytes[i] = (byte)c;
                }

                WriteByte((byte)bytes.Length);
                WriteBytes(bytes);
            }
        }

        private class BinaryReader {
            private readonly byte[] _data;

            public int Position { get; set; }
            public int Length => _data.Length;
            public int BytesRemaining => Length - Position;

            public BinaryReader(byte[] data) => _data = data;

            public byte ReadByte() {
                if (Position >= _data.Length)
                    throw new InvalidDataException("Unexpected end of data");
                return _data[Position++];
            }

            public byte PeekByte() {
                if (Position >= _data.Length)
                    throw new InvalidDataException("Unexpected end of data");
                return _data[Position];
            }

            public byte[] ReadBytes(int count) {
                if (count < 0 || Position + count > _data.Length)
                    throw new InvalidDataException("Unexpected end of data");
                byte[] result = new byte[count];
                Buffer.BlockCopy(_data, Position, result, 0, count);
                Position += count;
                return result;
            }

            public ulong ReadVarUInt() {
                ulong result = 0;
                int shift = 0;
                while (true) {
                    if (shift >= 64)
                        throw new InvalidDataException("VarUInt malformed.");
                    byte b = ReadByte();
                    result |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0) break;
                    shift += 7;
                }

                return result;
            }

            public long ReadVarInt() {
                ulong zigZag = ReadVarUInt();
                return (long)(zigZag >> 1) ^ (-(long)(zigZag & 1));
            }

            public double ReadDoubleLE() {
                byte[] bytes = ReadBytes(8);
                if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                return BitConverter.ToDouble(bytes, 0);
            }
            
            public decimal ReadDecimalLE() {
                int[] bits = new int[4];
                for (int i = 0; i < 4; i++) {
                    byte[] bytes = ReadBytes(4);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    bits[i] = BitConverter.ToInt32(bytes, 0);
                }

                return new decimal(bits);
            }

            public string ReadAsciiString() {
                byte length = ReadByte();
                byte[] bytes = ReadBytes(length);
                char[] chars = new char[length];
                for (int i = 0; i < length; i++) {
                    if (bytes[i] > 127)
                        throw new InvalidDataException("Unexpected character(ASCII)");
                    chars[i] = (char)bytes[i];
                }

                return new string(chars);
            }
        }

        #endregion
    }
}