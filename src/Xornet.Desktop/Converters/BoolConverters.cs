using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Xornet.Desktop.Converters;

public static class BoolConverters
{
    public static readonly IValueConverter ToColor = new FuncValueConverter<bool, string, IBrush?>((value, parameter) =>
    {
        var parts = parameter?.ToString()?.Split('|') ?? new[] { "Green", "Red" };
        var colorName = value ? parts[0] : parts[1];
        return colorName?.ToLowerInvariant() switch
        {
            "green" => new SolidColorBrush(Colors.Green),
            "red" => new SolidColorBrush(Colors.Red),
            "gray" => new SolidColorBrush(Colors.Gray),
            "blue" => new SolidColorBrush(Colors.Blue),
            _ => new SolidColorBrush(Colors.White)
        };
    });

    public static readonly IValueConverter ToToggleText = new FuncValueConverter<bool, string>((value) =>
        value ? "Stop" : "Start");
}
