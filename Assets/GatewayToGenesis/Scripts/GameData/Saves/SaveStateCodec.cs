using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;

/// <summary>A deliberately additive nested save field; older records receive a fresh default instance.</summary>
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
    public static void Restore(object target, StateNode node, params string[] fields)
    {
        if (node == null || node.kind != "object" || node.children.Count != fields.Length ||
            node.children.Select(c => c.name).Distinct().Count() != fields.Length) throw new InvalidOperationException("Incomplete state record.");
        foreach (string name in fields)
        {
            var field = Field(target.GetType(), name);
            var child = node.children.Single(c => c.name == name);
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
                field.SetValue(result, Activator.CreateInstance(field.FieldType));
                continue;
            }
            field.SetValue(result, Read(child, field.FieldType, field.GetValue(result)));
        }
        return result;
    }
    private static FieldInfo[] DataFields(Type type) => type.GetFields(Flags).Where(f => !f.IsStatic && !typeof(Delegate).IsAssignableFrom(f.FieldType)).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
}
