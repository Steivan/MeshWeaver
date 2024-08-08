﻿namespace MeshWeaver.Messaging.Serialization;

public interface ITypeRegistry
{
    ITypeRegistry WithType<TEvent>() => WithType(typeof(TEvent));
    ITypeRegistry WithType(Type type);
    ITypeRegistry WithType(Type type, string typeName);
    KeyFunction GetKeyFunction(string collection);
    KeyFunction GetKeyFunction(Type type);
    ITypeRegistry WithKeyFunction(string collection, KeyFunction keyFunction);
    bool TryGetType(string name, out Type type);
    bool TryGetTypeName(Type type, out string typeName);
    public ITypeRegistry WithTypesFromAssembly<T>(Func<Type, bool> filter)
        => WithTypesFromAssembly(typeof(T), filter);

    public ITypeRegistry WithTypesFromAssembly(Type type, Func<Type, bool> filter);
    ITypeRegistry WithTypes(IEnumerable<Type> select);
    string GetOrAddTypeName(Type valueType);
    ITypeRegistry WithKeyFunctionProvider(Func<Type, KeyFunction> key);
}