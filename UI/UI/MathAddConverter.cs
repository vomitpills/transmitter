using System;
using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

namespace UI;

public abstract class ArithmeticConverter : IValueConverter
{
    private static bool TryConvertArg(object? arg, out double result)
    {
        switch (arg)
        {
            case double d:
                result = d;
                return true;

            case string s:
                return double.TryParse(s, out result);

            default:
                result = default;
                return false;
        }
    }

    protected static bool TryParseArgs(object? value, object? parameter, out double a, out double b) => TryConvertArg(value, out a) & TryConvertArg(parameter, out b);

    public abstract object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new InvalidOperationException();
}

public class MathAddConverter : ArithmeticConverter
{
    public static readonly MathAddConverter Instance = new();

    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (!TryParseArgs(value, parameter, out double a, out double b))
            return new BindingNotification(new ArgumentOutOfRangeException(null, "Could not perform arithmetic on prov. args"), BindingErrorType.Error);
        return a + b;
    }
}