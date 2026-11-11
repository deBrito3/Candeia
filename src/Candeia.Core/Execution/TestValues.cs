using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Candeia.Execution;

// Neutral test values (JSON) turned into .NET values for the player's method, compared with what it
// returned, and written back the way C# would show them.
static class TestValues
{
    public static object? Convert(JsonElement value, Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } inner) return value.ValueKind == JsonValueKind.Null ? null : Convert(value, inner);
        if (value.ValueKind == JsonValueKind.Null) return type.IsValueType ? throw new NotSupportedException($"null for {type.Name}") : null;
        if (type == typeof(int)) return value.GetInt32();
        if (type == typeof(long)) return value.GetInt64();
        if (type == typeof(short)) return value.GetInt16();
        if (type == typeof(byte)) return value.GetByte();
        if (type == typeof(double)) return value.GetDouble();
        if (type == typeof(float)) return value.GetSingle();
        if (type == typeof(decimal)) return value.GetDecimal();
        if (type == typeof(bool)) return value.GetBoolean();
        if (type == typeof(string)) return value.GetString();
        if (type == typeof(char)) return value.GetString() is [var c] ? c : throw new NotSupportedException("char needs one character");
        if (type.IsArray)
        {
            var element = type.GetElementType()!;
            var items = value.EnumerateArray().Select(v => Convert(v, element)).ToArray();
            var array = Array.CreateInstance(element, items.Length);
            for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i);
            return array;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (IList)Activator.CreateInstance(type)!;
            foreach (var item in value.EnumerateArray()) list.Add(Convert(item, type.GetGenericArguments()[0]));
            return list;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>) && type.GetGenericArguments()[0] == typeof(string))
        {
            var map = (IDictionary)Activator.CreateInstance(type)!;
            foreach (var property in value.EnumerateObject()) map[property.Name] = Convert(property.Value, type.GetGenericArguments()[1]);
            return map;
        }
        throw new NotSupportedException($"the test value type {type.Name} is not supported");
    }

    public static bool Equal(object? actual, object? expected, double tolerance)
    {
        if (actual is null || expected is null) return actual is null && expected is null;
        if (IsNumber(actual) && IsNumber(expected))
        {
            if (actual is double or float or decimal || expected is double or float or decimal)
            {
                double a = System.Convert.ToDouble(actual, CultureInfo.InvariantCulture), b = System.Convert.ToDouble(expected, CultureInfo.InvariantCulture);
                return Math.Abs(a - b) <= tolerance * Math.Max(1, Math.Abs(b));
            }
            return System.Convert.ToDecimal(actual, CultureInfo.InvariantCulture) == System.Convert.ToDecimal(expected, CultureInfo.InvariantCulture);
        }
        if (actual is string || expected is string) return actual is string x && expected is string y && string.Equals(x, y, StringComparison.Ordinal);
        if (actual is IDictionary left && expected is IDictionary right)
        {
            if (left.Count != right.Count) return false;
            foreach (DictionaryEntry entry in right)
                if (!left.Contains(entry.Key) || !Equal(left[entry.Key], entry.Value, tolerance)) return false;
            return true;
        }
        if (actual is IEnumerable first && expected is IEnumerable second)
        {
            var a = first.Cast<object?>().ToList();
            var b = second.Cast<object?>().ToList();
            return a.Count == b.Count && a.Zip(b).All(pair => Equal(pair.First, pair.Second, tolerance));
        }
        return actual.Equals(expected);
    }

    static bool IsNumber(object value) => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    // How C# would write the value: true, 3.5, "texto", 'c', [1, 2], { "a": 1 }.
    public static string Format(object? value, int depth = 0)
    {
        if (depth > 4) return "…";
        switch (value)
        {
            case null: return "null";
            case bool b: return b ? "true" : "false";
            case string s: return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
            case char c: return "'" + c + "'";
            case double d: return d.ToString("R", CultureInfo.InvariantCulture);
            case float f: return f.ToString("R", CultureInfo.InvariantCulture);
            case IFormattable formattable when IsNumber(value): return formattable.ToString(null, CultureInfo.InvariantCulture);
            case IDictionary map:
                var pairs = new StringBuilder("{ ");
                int count = 0;
                foreach (DictionaryEntry entry in map) { if (count++ > 0) pairs.Append(", "); pairs.Append(Format(entry.Key, depth + 1)).Append(": ").Append(Format(entry.Value, depth + 1)); }
                return pairs.Append(" }").ToString();
            case IEnumerable items:
                return "[" + string.Join(", ", items.Cast<object?>().Take(50).Select(i => Format(i, depth + 1))) + "]";
            default: return value.ToString() ?? "";
        }
    }

    // The expected value written from its JSON form, for tests that never ran.
    public static string Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        JsonValueKind.String => Format(value.GetString()),
        JsonValueKind.Array => "[" + string.Join(", ", value.EnumerateArray().Select(item => Format(item))) + "]",
        JsonValueKind.Object => "{ " + string.Join(", ", value.EnumerateObject().Select(p => Format(p.Name) + ": " + Format(p.Value))) + " }",
        _ => value.GetRawText(),
    };
}
