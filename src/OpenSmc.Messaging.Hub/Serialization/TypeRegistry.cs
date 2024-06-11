﻿using System.Collections.Concurrent;

namespace OpenSmc.Messaging.Serialization;

public class TypeRegistry(ITypeRegistry parent) : ITypeRegistry
{
    private static readonly Type[] BasicTypes =
    [
        typeof(string),
        typeof(int),
        typeof(long),
        typeof(short),
        typeof(byte),
        typeof(sbyte),
        typeof(uint),
        typeof(ulong),
        typeof(ushort),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(char),
        typeof(bool),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
        typeof(byte[])
    ];

    private readonly ConcurrentDictionary<string, Type> typeByName =
        new(BasicTypes.Select(t => new KeyValuePair<string, Type>(t.Name, t)));
    private readonly ConcurrentDictionary<Type, string> nameByType =
        new(BasicTypes.Select(t => new KeyValuePair<Type, string>(t, t.Name)));
    private readonly ConcurrentDictionary<string, Func<object, object>> keysByType = new();

    public ITypeRegistry WithType(Type type) => WithType(type, FormatType(type));

    public ITypeRegistry WithType(Type type, string typeName) => WithType(type, typeName, null);

    public ITypeRegistry WithType(Type type, string typeName, Func<object, object> key)
    {
        typeByName[typeName] = type;
        nameByType[type] = typeName;
        if (key != null)
            keysByType[typeName] = key;
        return this;
    }

    public Func<object, object> GetKeyFunction(string collection) =>
        keysByType.GetValueOrDefault(collection);

    public bool TryGetType(string name, out Type type)
    {
        if (typeByName.TryGetValue(name, out type))
            return true;
        if (name.Contains('[') && name.EndsWith(']'))
        {
            var typeName = name.Substring(0, name.IndexOf('['));
            var baseType = typeByName.GetValueOrDefault(typeName);

            if (baseType == null)
                return false;

            var genericArgs = name.Substring(
                    name.IndexOf('[') + 1,
                    name.Length - name.IndexOf('[') - 2
                )
                .Split(',');
            var genericTypeArgs = new Type[genericArgs.Length];

            for (int i = 0; i < genericArgs.Length; i++)
            {
                if (TryGetType(genericArgs[i].Trim(), out var genericTypeArg))
                {
                    genericTypeArgs[i] = genericTypeArg;
                }
                else
                {
                    baseType = null;
                    return false;
                }
            }
            type = baseType.MakeGenericType(genericTypeArgs);
            return true;
        }
        return parent?.TryGetType(name, out type) ?? false;
    }

    public bool TryGetTypeName(Type type, out string typeName)
    {
        if (nameByType.TryGetValue(type, out typeName))
            return true;

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            var genericArguments = type.GetGenericArguments();
            var genericTypeArguments = new string[genericArguments.Length];
            for (int i = 0; i < genericArguments.Length; i++)
            {
                if (!TryGetTypeName(genericArguments[i], out var genericTypeArgument))
                {
                    typeName = null;
                    return false;
                }
                genericTypeArguments[i] = genericTypeArgument;
            }
            typeName =
                $"{FormatType(genericTypeDefinition)}[{string.Join(',', genericTypeArguments)}]";
            return true;
        }

        typeName = null;
        return false;
    }

    public string GetOrAddTypeName(Type type)
    {
        if (nameByType.TryGetValue(type, out var typeName))
            return typeName;

        typeName = FormatType(type);
        typeByName[typeName] = type;
        return nameByType[type] = typeName;
    }

    public ITypeRegistry WithTypesFromAssembly(Type type, Func<Type, bool> filter) =>
        WithTypes(type.Assembly.GetTypes().Where(filter));

    public ITypeRegistry WithTypes(IEnumerable<Type> types)
    {
        foreach (var t in types)
            WithType(t);
        return this;
    }

    public string FormatType(Type mainType)
    {
        var mainTypeName = (mainType.FullName ?? mainType.Name).Replace('\u002B', '.');
        if (!mainType.IsGenericType || mainType.IsGenericTypeDefinition)
            return mainTypeName;

        var typeDefinition = mainType.GetGenericTypeDefinition();
        if (typeDefinition == typeof(Nullable<>))
            return FormatType(mainType.GetGenericArguments()[0]) + "?";

        var text =
            $"{GetOrAddTypeName(typeDefinition)}[{string.Join(',', mainType.GetGenericArguments().Select(GetOrAddTypeName))}]";
        return text;
    }
}
