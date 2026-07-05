using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace UI;

public class TernaryExtension : MarkupExtension
{
    public required BindingBase Condition { get; set; }
    public required object True { get; set; }
    public required object False { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
        => new MultiBinding()
        {
            Bindings = [Condition],
            Converter = new FuncMultiValueConverter<bool, object?>(c =>
            {
                return Convert(c.First() ? True : False, serviceProvider);
            })
        };

    private static object? Convert(object value, IServiceProvider serviceProvider)
    {
        IProvideValueTarget? targetService = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        Type? targetType = targetService?.TargetProperty switch
        {
            AvaloniaProperty property => property.PropertyType,
            PropertyInfo property => property.PropertyType,
            _ => null
        };
        Type type = value.GetType();
        if (targetType is null || targetType.IsAssignableFrom(type))
            return value;
        TypeConverter converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(type))
            return converter.ConvertFrom(value);
        return value;
    }
}