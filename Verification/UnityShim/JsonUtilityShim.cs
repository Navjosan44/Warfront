// ---------------------------------------------------------------------------------------------
// JsonUtility shim for the WARFRONT verification harness.
//
// Unity's JsonUtility serializes public instance fields (and [SerializeField] private fields),
// supports primitives, string, enum-as-int, nested [Serializable] classes, List<T> and arrays, and
// writes pretty JSON as `"Field": value` with 4-space indentation. This shim reproduces that
// behaviour for the document shapes WARFRONT uses so the save pipeline can be unit tested without
// the editor. It is not shipped in the game.
// ---------------------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UnityEngine
{
    public static class JsonUtility
    {
        public static string ToJson(object obj)
        {
            return ToJson(obj, false);
        }

        public static string ToJson(object obj, bool prettyPrint)
        {
            StringBuilder sb = new StringBuilder(1024);
            JsonWriter.Write(sb, obj, prettyPrint, 0);
            return sb.ToString();
        }

        public static T FromJson<T>(string json)
        {
            object value = FromJson(json, typeof(T));
            return value == null ? default(T) : (T)value;
        }

        public static object FromJson(string json, Type type)
        {
            if (json == null) throw new ArgumentNullException("json");
            JsonValue parsed = JsonParser.Parse(json);
            if (parsed == null || parsed.IsNull) return null;
            return JsonWriter.FromJsonValue(parsed, type);
        }

        public static void FromJsonOverwrite(string json, object objectToOverwrite)
        {
            if (objectToOverwrite == null) return;
            JsonValue parsed = JsonParser.Parse(json);
            JsonWriter.Populate(parsed, objectToOverwrite);
        }
    }

    internal static class JsonWriter
    {
        public static void Write(StringBuilder sb, object value, bool pretty, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            Type type = value.GetType();

            if (type == typeof(string))
            {
                WriteString(sb, (string)value);
                return;
            }
            if (type == typeof(bool))
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }
            if (type.IsEnum)
            {
                sb.Append(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (type == typeof(char))
            {
                WriteString(sb, value.ToString());
                return;
            }
            if (type.IsPrimitive || type == typeof(decimal))
            {
                sb.Append(FormatNumber(value));
                return;
            }

            if (value is IList list && !(value is string))
            {
                WriteList(sb, list, pretty, depth);
                return;
            }

            WriteObject(sb, value, type, pretty, depth);
        }

        private static void WriteList(StringBuilder sb, IList list, bool pretty, int depth)
        {
            if (list.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(',');
                NewLine(sb, pretty, depth + 1);
                Write(sb, list[i], pretty, depth + 1);
            }
            NewLine(sb, pretty, depth);
            sb.Append(']');
        }

        private static void WriteObject(StringBuilder sb, object value, Type type, bool pretty, int depth)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            List<FieldInfo> serializable = new List<FieldInfo>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || field.IsInitOnly) continue;
                if (field.IsDefined(typeof(NonSerializedAttribute), true)) continue;
                if (field.FieldType.IsPointer || field.FieldType.IsInterface) continue;
                serializable.Add(field);
            }

            if (serializable.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append('{');
            for (int i = 0; i < serializable.Count; i++)
            {
                FieldInfo field = serializable[i];
                if (i > 0) sb.Append(',');
                NewLine(sb, pretty, depth + 1);
                WriteString(sb, field.Name);
                sb.Append(pretty ? ": " : ":");
                object fieldValue = null;
                try
                {
                    fieldValue = field.GetValue(value);
                }
                catch (Exception)
                {
                    fieldValue = null;
                }
                Write(sb, fieldValue, pretty, depth + 1);
            }
            NewLine(sb, pretty, depth);
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            for (int i = 0; i < depth * 4; i++) sb.Append(' ');
        }

        private static string FormatNumber(object value)
        {
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is double d)
            {
                string text = d.ToString("R", CultureInfo.InvariantCulture);
                return text.Contains(".") || text.Contains("E") || text.Contains("e") ? text : text + ".0";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static object FromJsonValue(JsonValue value, Type type)
        {
            if (value == null) return DefaultOf(type);
            if (type == typeof(string)) return value.IsNull ? null : value.AsString();
            if (type == typeof(bool)) return value.AsBool();
            if (type.IsEnum) return Enum.ToObject(type, (int)value.AsLong());
            if (type == typeof(int)) return (int)value.AsLong();
            if (type == typeof(long)) return value.AsLong();
            if (type == typeof(short)) return (short)value.AsLong();
            if (type == typeof(byte)) return (byte)value.AsLong();
            if (type == typeof(uint)) return (uint)value.AsLong();
            if (type == typeof(ulong)) return (ulong)value.AsLong();
            if (type == typeof(float)) return value.AsFloat();
            if (type == typeof(double)) return value.AsDouble();
            if (type == typeof(char)) return value.IsNull ? '\0' : value.AsString()[0];

            if (value.IsNull) return DefaultOf(type);

            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                if (!value.IsArray) return DefaultOf(type);
                Array array = Array.CreateInstance(elementType, value.Items.Count);
                for (int i = 0; i < value.Items.Count; i++) array.SetValue(FromJsonValue(value.Items[i], elementType), i);
                return array;
            }

            if (typeof(IList).IsAssignableFrom(type))
            {
                Type elementType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                IList list = (IList)Activator.CreateInstance(type);
                if (!value.IsArray) return list;
                for (int i = 0; i < value.Items.Count; i++) list.Add(FromJsonValue(value.Items[i], elementType));
                return list;
            }

            object instance = Activator.CreateInstance(type);
            Populate(value, instance);
            return instance;
        }

        public static void Populate(JsonValue value, object target)
        {
            if (target == null || value == null || !value.IsObject) return;
            Type type = target.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || field.IsInitOnly) continue;
                if (field.IsDefined(typeof(NonSerializedAttribute), true)) continue;
                JsonValue member = value.Get(field.Name);
                if (member == null) continue;
                try
                {
                    object converted = FromJsonValue(member, field.FieldType);
                    if (converted != null || !field.FieldType.IsValueType) field.SetValue(target, converted);
                }
                catch (Exception)
                {
                    // Ignore individual field failures: sanitisation handles partial data.
                }
            }
        }

        private static object DefaultOf(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    internal sealed class JsonValue
    {
        public enum Kind
        {
            Null,
            Boolean,
            Number,
            String,
            Array,
            Object
        }

        public Kind ValueKind { get; set; }
        public bool BoolValue { get; set; }
        public double NumberValue { get; set; }
        public string StringValue { get; set; }
        public List<JsonValue> Items { get; set; }
        public Dictionary<string, JsonValue> Members { get; set; }

        public bool IsNull { get { return ValueKind == Kind.Null; } }
        public bool IsArray { get { return ValueKind == Kind.Array; } }
        public bool IsObject { get { return ValueKind == Kind.Object; } }

        public JsonValue Get(string name)
        {
            if (Members == null) return null;
            JsonValue value;
            return Members.TryGetValue(name, out value) ? value : null;
        }

        public string AsString()
        {
            switch (ValueKind)
            {
                case Kind.String: return StringValue;
                case Kind.Number: return NumberValue.ToString(CultureInfo.InvariantCulture);
                case Kind.Boolean: return BoolValue ? "True" : "False";
                default: return null;
            }
        }

        public bool AsBool()
        {
            if (ValueKind == Kind.Boolean) return BoolValue;
            if (ValueKind == Kind.Number) return Math.Abs(NumberValue) > double.Epsilon;
            if (ValueKind == Kind.String) return bool.TryParse(StringValue, out bool parsed) && parsed;
            return false;
        }

        public long AsLong()
        {
            if (ValueKind == Kind.Number) return (long)Math.Round(NumberValue);
            if (ValueKind == Kind.Boolean) return BoolValue ? 1 : 0;
            if (ValueKind == Kind.String && long.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out long parsed)) return parsed;
            return 0;
        }

        public double AsDouble()
        {
            if (ValueKind == Kind.Number) return NumberValue;
            if (ValueKind == Kind.String && double.TryParse(StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            return 0d;
        }

        public float AsFloat()
        {
            return (float)AsDouble();
        }
    }

    internal static class JsonParser
    {
        public static JsonValue Parse(string text)
        {
            int index = 0;
            SkipWhitespace(text, ref index);
            JsonValue value = ParseValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index < text.Length)
            {
                throw new FormatException("Unexpected trailing content at position " + index);
            }
            return value;
        }

        private static JsonValue ParseValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length) throw new FormatException("Unexpected end of JSON input");

            char c = text[index];
            switch (c)
            {
                case '{': return ParseObject(text, ref index);
                case '[': return ParseArray(text, ref index);
                case '"': return new JsonValue { ValueKind = JsonValue.Kind.String, StringValue = ParseString(text, ref index) };
                case 't':
                    Expect(text, ref index, "true");
                    return new JsonValue { ValueKind = JsonValue.Kind.Boolean, BoolValue = true };
                case 'f':
                    Expect(text, ref index, "false");
                    return new JsonValue { ValueKind = JsonValue.Kind.Boolean, BoolValue = false };
                case 'n':
                    Expect(text, ref index, "null");
                    return new JsonValue { ValueKind = JsonValue.Kind.Null };
                default: return ParseNumber(text, ref index);
            }
        }

        private static JsonValue ParseObject(string text, ref int index)
        {
            JsonValue value = new JsonValue { ValueKind = JsonValue.Kind.Object, Members = new Dictionary<string, JsonValue>(16) };
            index++;
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == '}')
            {
                index++;
                return value;
            }

            while (index < text.Length)
            {
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != '"') throw new FormatException("Expected property name at position " + index);
                string name = ParseString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':') throw new FormatException("Expected ':' at position " + index);
                index++;
                JsonValue member = ParseValue(text, ref index);
                value.Members[name] = member;
                SkipWhitespace(text, ref index);
                if (index >= text.Length) break;
                if (text[index] == ',')
                {
                    index++;
                    continue;
                }
                if (text[index] == '}')
                {
                    index++;
                    return value;
                }
                throw new FormatException("Unexpected character '" + text[index] + "' at position " + index);
            }

            throw new FormatException("Unterminated object");
        }

        private static JsonValue ParseArray(string text, ref int index)
        {
            JsonValue value = new JsonValue { ValueKind = JsonValue.Kind.Array, Items = new List<JsonValue>(8) };
            index++;
            SkipWhitespace(text, ref index);
            if (index < text.Length && text[index] == ']')
            {
                index++;
                return value;
            }

            while (index < text.Length)
            {
                value.Items.Add(ParseValue(text, ref index));
                SkipWhitespace(text, ref index);
                if (index >= text.Length) break;
                if (text[index] == ',')
                {
                    index++;
                    continue;
                }
                if (text[index] == ']')
                {
                    index++;
                    return value;
                }
                throw new FormatException("Unexpected character '" + text[index] + "' at position " + index);
            }

            throw new FormatException("Unterminated array");
        }

        private static string ParseString(string text, ref int index)
        {
            index++;
            StringBuilder sb = new StringBuilder(32);
            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"') return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (index >= text.Length) break;
                char escape = text[index++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Invalid unicode escape");
                        string hex = text.Substring(index, 4);
                        sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default: sb.Append(escape); break;
                }
            }
            throw new FormatException("Unterminated string");
        }

        private static JsonValue ParseNumber(string text, ref int index)
        {
            int start = index;
            while (index < text.Length)
            {
                char c = text[index];
                if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                {
                    index++;
                    continue;
                }
                break;
            }

            string token = text.Substring(start, index - start);
            if (!double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
            {
                throw new FormatException("Invalid number '" + token + "' at position " + start);
            }
            return new JsonValue { ValueKind = JsonValue.Kind.Number, NumberValue = parsed };
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length || string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Expected '" + literal + "' at position " + index);
            }
            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    index++;
                    continue;
                }
                break;
            }
        }
    }
}
