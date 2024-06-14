﻿using System.Reactive.Linq;
using System.Text.Json;
using Json.Patch;
using Json.Pointer;
using Microsoft.AspNetCore.Components;
using OpenSmc.Data;
using OpenSmc.Data.Serialization;
using OpenSmc.Layout;
using OpenSmc.Messaging;

namespace OpenSmc.Blazor
{
    public partial class BlazorView<TViewModel> : IDisposable
        where TViewModel : UiControl
    {
        [Inject] private IMessageHub Hub { get; set; }
        protected override void OnParametersSet()
        {
            ResetBindings();
            base.OnParametersSet();
            if (ViewModel != null)
            {
                DataBind<string>(ViewModel.Skin, x => Skin = x);
                DataBind<string>(ViewModel.Label, x => Label = x);
            }
        }

        protected string Skin { get; set; }

        protected string Label { get; set; }

        protected object BindProperty(object instance, string propertyName)
        {
            if(instance == null)
                return null;

            var type = instance.GetType();
            var property = type.GetProperty(propertyName);
            if(property == null)
                return null;
            return property.GetValue(instance, null);
        }



        protected List<IDisposable> Disposables { get; } = new();

        public void Dispose()
        {
            foreach (var d in bindings.Concat(Disposables))
            {
                d.Dispose();
            }
        }

        protected UiControl GetControl(ChangeItem<JsonElement> item, string area)
        {
            return item.Value.TryGetProperty(LayoutAreaReference.Areas, out var controls) &&
                   controls.TryGetProperty(area, out var node)
                ? node.Deserialize<UiControl>(Stream.Hub.JsonSerializerOptions)
                : null;
        }

        private readonly List<IDisposable> bindings = new();
        protected virtual void DataBind<T>(object value, Action<T> bindingAction)
        {
            bindings.Add(GetObservable<T>(value).Subscribe(bindingAction));
        }
        protected T SubmitChange<T>(T value, JsonPointerReference reference)
        {
            if (reference != null)
                Stream.Update(ci => new ChangeItem<JsonElement>(
                    Stream.Id,
                    Stream.Reference,
                    GetPatch(value, reference, ci),
                    Hub.Address,
                    Hub.Version
                ));
            return value;
        }

        private JsonElement GetPatch<T>(T value, JsonPointerReference reference, JsonElement current)
        {
            var pointer = JsonPointer.Parse(reference.Pointer);

            var existing = pointer.Evaluate(current);
            if (value == null) 
                return existing == null 
                    ? current 
                    : new JsonPatch(PatchOperation.Remove(pointer)).Apply(current);

            var valueSerialized = JsonSerializer.SerializeToNode(value, Hub.JsonSerializerOptions);

            return existing == null 
                ? new JsonPatch(PatchOperation.Add(pointer, valueSerialized)).Apply(current) 
                : new JsonPatch(PatchOperation.Replace(pointer, valueSerialized)).Apply(current);
        }

        public void ResetBindings()
        {
            foreach (var d in bindings)
            {
                d.Dispose();
            }
            bindings.Clear();
        }

        protected virtual IObservable<T> GetObservable<T>(object value)
        {
            if (value is null)
                return Observable.Empty<T>();
            if (value is IObservable<T> observable)
                return observable;
            if (value is WorkspaceReference reference)
                return Stream.Reduce(reference).Select(ConvertTo<T>);
            if (value is T t)
                return Observable.Return(t);
            // TODO V10: Should we add more ways to convert? Converting to primitives? (11.06.2024, Roland Bürgi)
            throw new InvalidOperationException($"Cannot bind to {value.GetType().Name}");
        }

        private T ConvertTo<T>(IChangeItem changeItem)
        {
            var value = changeItem.Value;
            if (value == null)
                return default;
            if (value is JsonElement node)
                return node.Deserialize<T>(Stream.Hub.JsonSerializerOptions);
            if (value is T t)
                return t;
            throw new InvalidOperationException($"Cannot convert to {typeof(T).Name}");
        }

    }
}
