using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

/// <summary>A deliberately additive save field: an older record without it gets a fresh default instance (nested objects) or keeps the current value (a system's or a world tile's own fields).</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SaveOptionalFieldAttribute : Attribute { }

/// <summary>Field-only snapshot codec. Types come from the compiled schema, never a type name supplied by a file.</summary>
public static class SaveStateCodec
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static FieldInfo Field(Type type, string name) => type.GetField(name, Flags) ??
        type.GetField("<" + name + ">k__BackingField", Flags) ?? throw new InvalidOperationException(type.Name + "." + name + " is missing from the save schema.");
    public static StateNode Capture(object target, params string[] fields)
    {
        var node = new StateNode { kind = "object" };
        foreach (var name in fields)
        {
            var field = Field(target.GetType(), name);
            var child = Write(field.GetValue(target), field.FieldType); child.name = name; node.children.Add(child);
        }
        return node;
    }
    // A field marked [SaveOptionalField] may be missing from an older save: it keeps the value it has.
    public static void Restore(object target, StateNode node, params string[] fields)
    {
        if (node == null || node.kind != "object" || node.children.Select(c => c.name).Distinct().Count() != node.children.Count ||
            node.children.Any(c => !fields.Contains(c.name))) throw new InvalidOperationException("Incomplete state record.");
        foreach (string name in fields)
        {
            var field = Field(target.GetType(), name);
            var child = node.children.SingleOrDefault(c => c.name == name);
            if (child == null)
            {
                if (!field.IsDefined(typeof(SaveOptionalFieldAttribute), false)) throw new InvalidOperationException("Incomplete state record.");
                continue;
            }
            field.SetValue(target, Read(child, field.FieldType, field.GetValue(target)));
        }
    }
    private static bool Scalar(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);
    public static StateNode Write(object value, Type type)
    {
        var n = new StateNode();
        if (value == null || value is UnityEngine.Object u && u == null) { n.kind = "null"; return n; }
        if (Scalar(type)) { n.kind = "scalar"; n.value = Convert.ToString(value, CultureInfo.InvariantCulture); return n; }
        if (value is GameTechnologySlot tech) { n.kind = "technology"; n.value = tech.gameUnit.name; return n; }
        if (value is UnityEngine.Object asset)
        {
            if (!(asset is ScriptableObject) && !(asset is Sprite) && !(asset is TextAsset)) throw new InvalidOperationException("Scene references cannot be saved: " + type.Name);
            n.kind = "asset"; n.value = asset.name; return n;
        }
        if (value is IDictionary dictionary)
        {
            n.kind = "dictionary"; var args = type.GetGenericArguments();
            foreach (DictionaryEntry entry in dictionary)
                n.children.Add(new StateNode { children = new List<StateNode> { Write(entry.Key, args[0]), Write(entry.Value, args[1]) } });
            return n;
        }
        if (value is IEnumerable collection)
        {
            n.kind = "list"; Type element = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
            foreach (object item in collection) n.children.Add(Write(item, element));
            return n;
        }
        if (type.Assembly != typeof(SaveStateCodec).Assembly) throw new InvalidOperationException("Unsupported save value: " + type.Name);
        n.kind = "object";
        foreach (var f in DataFields(type)) { var child = Write(f.GetValue(value), f.FieldType); child.name = f.Name; n.children.Add(child); }
        return n;
    }
    public static object Read(StateNode node, Type type, object existing = null)
    {
        if (node == null) throw new InvalidOperationException("Missing state value.");
        if (node.kind == "null") return null;
        if (Scalar(type))
        {
            if (node.kind != "scalar") throw new InvalidOperationException("Wrong scalar shape.");
            var value = type.IsEnum ? Enum.Parse(type, node.value) : Convert.ChangeType(node.value, type, CultureInfo.InvariantCulture);
            if (value is float f && (float.IsNaN(f) || float.IsInfinity(f)) || value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                throw new InvalidOperationException("Non-finite save value.");
            return value;
        }
        if (type == typeof(GameTechnologySlot)) return GameUnitsLogic.Instance.GetTechnologySlot(node.value) ?? throw new InvalidOperationException("Missing technology " + node.value);
        if (typeof(UnityEngine.Object).IsAssignableFrom(type))
        {
            if (existing is UnityEngine.Object old && old != null && old.name == node.value) return old;
            var matches = Resources.FindObjectsOfTypeAll(type).Where(a => a.name == node.value).ToArray();
            if (matches.Length == 0) matches = Resources.LoadAll("", type).Where(a => a.name == node.value).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Missing or ambiguous content: " + type.Name + " / " + node.value);
            return matches[0];
        }
        if (type.IsArray)
        {
            var array = Array.CreateInstance(type.GetElementType(), node.children.Count);
            var old = existing as Array;
            for (int i = 0; i < array.Length; i++) array.SetValue(Read(node.children[i], type.GetElementType(), old != null && i < old.Length ? old.GetValue(i) : null), i);
            return array;
        }
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            var dict = (IDictionary)(existing ?? Activator.CreateInstance(type)); dict.Clear(); var args = type.GetGenericArguments();
            foreach (var pair in node.children) dict.Add(Read(pair.children[0], args[0]), Read(pair.children[1], args[1]));
            return dict;
        }
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            var collection = existing ?? Activator.CreateInstance(type);
            type.GetMethod("Clear").Invoke(collection, null);
            var add = type.GetMethod("Add") ?? type.GetMethod("Enqueue");
            foreach (var child in node.children) add.Invoke(collection, new[] { Read(child, type.GetGenericArguments()[0]) });
            return collection;
        }
        if (type.Assembly != typeof(SaveStateCodec).Assembly) throw new InvalidOperationException("Unsupported save type.");
        object result = existing ?? (type.GetConstructor(Flags, null, Type.EmptyTypes, null) != null ? Activator.CreateInstance(type, true) : FormatterServices.GetUninitializedObject(type));
        var fields = DataFields(type);
        if (node.children.Select(c => c.name).Distinct().Count() != node.children.Count ||
            node.children.Any(c => !fields.Any(f => f.Name == c.name))) throw new InvalidOperationException("State schema changed: " + type.Name);
        foreach (var field in fields)
        {
            var child = node.children.SingleOrDefault(n => n.name == field.Name);
            if (child == null)
            {
                if (!field.IsDefined(typeof(SaveOptionalFieldAttribute), false)) throw new InvalidOperationException("State schema changed: " + type.Name);
                field.SetValue(result, field.FieldType == typeof(string) ? null : Activator.CreateInstance(field.FieldType));
                continue;
            }
            field.SetValue(result, Read(child, field.FieldType, field.GetValue(result)));
        }
        return result;
    }
    private static readonly Dictionary<Type, FieldInfo[]> dataFields = new Dictionary<Type, FieldInfo[]>();
    private static FieldInfo[] DataFields(Type type)
    {
        // Locked: a background save formats its columns while the main thread may capture.
        lock (dataFields)
        {
            if (!dataFields.TryGetValue(type, out var fields))
                dataFields[type] = fields = type.GetFields(Flags).Where(f => !f.IsStatic && !typeof(Delegate).IsAssignableFrom(f.FieldType)).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            return fields;
        }
    }

    // ===== COLUMNS =====
    // Many objects of one type (the world's tiles) saved field by field: each field one list of strings, one per object.
    // A column holds scalars, or small structs of scalars (a HexCoord) written as their fields joined by commas.

    /// <summary>The field, checked to be one a column can hold (throws otherwise).</summary>
    public static FieldInfo ColumnField(Type type, string name)
    {
        var field = Field(type, name);
        if (!Scalar(field.FieldType) && !ScalarStruct(field.FieldType))
            throw new InvalidOperationException(type.Name + "." + name + " cannot be saved in a column (scalars and structs of scalars only).");
        return field;
    }

    private static bool ScalarStruct(Type t) => t.IsValueType && !t.IsPrimitive && !t.IsEnum &&
        DataFields(t).Length > 0 && DataFields(t).All(f => Scalar(f.FieldType) && f.FieldType != typeof(string));

    /// <summary>The targets' fields as columns of text. Touches nothing but the targets (a worker thread may run it on
    /// copies of them).</summary>
    public static List<SavedColumn> PackColumns<T>(IReadOnlyList<T> targets, string[] fields) where T : class
    {
        var columns = new List<SavedColumn>(fields.Length);
        foreach (string name in fields)
        {
            var field = ColumnField(typeof(T), name);
            var parts = Scalar(field.FieldType) ? null : DataFields(field.FieldType);
            var column = new SavedColumn { field = name, values = new List<string>(targets.Count) };
            for (int i = 0; i < targets.Count; i++)
            {
                object value = field.GetValue(targets[i]);
                if (value == null) { column.nulls.Add(i); column.values.Add(""); }
                else if (parts == null) column.values.Add(Convert.ToString(value, CultureInfo.InvariantCulture));
                else
                {
                    var text = new string[parts.Length];
                    for (int p = 0; p < parts.Length; p++) text[p] = Convert.ToString(parts[p].GetValue(value), CultureInfo.InvariantCulture);
                    column.values.Add(string.Join(",", text));
                }
            }
            columns.Add(column);
        }
        return columns;
    }

    /// <summary>Set each target's fields from the columns. A missing column is allowed only for a [SaveOptionalField] (the
    /// targets keep their value).</summary>
    public static void RestoreColumns<T>(IReadOnlyList<T> targets, List<SavedColumn> columns, string[] fields) where T : class
    {
        if (columns == null || columns.Select(c => c.field).Distinct().Count() != columns.Count || columns.Any(c => !fields.Contains(c.field)) ||
            columns.Any(c => c.values == null || c.values.Count != targets.Count)) throw new InvalidOperationException("Incomplete state record.");
        foreach (string name in fields)
        {
            var field = ColumnField(typeof(T), name);
            var column = columns.FirstOrDefault(c => c.field == name);
            if (column == null)
            {
                if (!field.IsDefined(typeof(SaveOptionalFieldAttribute), false)) throw new InvalidOperationException("Incomplete state record.");
                continue;
            }
            var nulls = new HashSet<int>(column.nulls ?? new List<int>());
            var parts = Scalar(field.FieldType) ? null : DataFields(field.FieldType);
            for (int i = 0; i < targets.Count; i++)
            {
                object value;
                if (nulls.Contains(i)) value = null;
                else if (parts == null) value = ParseScalar(column.values[i], field.FieldType);
                else
                {
                    var text = column.values[i].Split(',');
                    if (text.Length != parts.Length) throw new InvalidOperationException("Wrong shape: " + name);
                    value = Activator.CreateInstance(field.FieldType);
                    for (int p = 0; p < parts.Length; p++) parts[p].SetValue(value, ParseScalar(text[p], parts[p].FieldType));
                }
                if (value == null && field.FieldType.IsValueType) throw new InvalidOperationException("Missing state value.");
                field.SetValue(targets[i], value);
            }
        }
    }

    private static object ParseScalar(string text, Type type)
    {
        object value;
        if (type == typeof(string)) return text;
        if (type == typeof(int)) value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        else if (type == typeof(bool)) value = bool.Parse(text);
        else if (type == typeof(float)) value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        else if (type == typeof(long)) value = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        else if (type.IsEnum) value = Enum.Parse(type, text);
        else value = Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
        if (value is float f && (float.IsNaN(f) || float.IsInfinity(f)) || value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
            throw new InvalidOperationException("Non-finite save value.");
        return value;
    }
}
