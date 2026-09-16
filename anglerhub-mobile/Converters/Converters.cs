using System.Globalization;

namespace AnglerHub.Mobile.Converters;

public class StringNotEmptyConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> !string.IsNullOrWhiteSpace(value as string);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool b && !b;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is bool b && !b;
}

/// <summary>
/// True/false -> one of two theme colors (e.g. a green "active" badge vs. a neutral one).
/// ConverterParameter = "ColorIfTrue|ColorIfFalse" (resource keys).
/// </summary>
public class BoolToColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var isTrue = value is bool b && b;
		var parts = (parameter as string)?.Split('|') ?? new[] { "Primary", "TextFaint" };
		var key = isTrue ? parts[0] : parts.Length > 1 ? parts[1] : parts[0];

		return Application.Current?.Resources.TryGetValue(key, out var color) == true
			? color
			: Colors.Gray;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

/// <summary>Int > 0 -> visible (e.g. a "3 pending weighings" badge).</summary>
public class CountToVisibleConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is int i && i > 0;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

/// <summary>DateTime? -> red (past) or green (today/future) for date chips on the dashboard.</summary>
public class DatePastToColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not DateTime date) return Colors.Gray;

		return date.Date < DateTime.Today
			? (Application.Current?.Resources.TryGetValue("DangerLight", out var red) == true ? red : Colors.Red)
			: (Application.Current?.Resources.TryGetValue("Primary", out var green) == true ? green : Colors.Green);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

// A handful of other small converters (opacity, chevron direction, resource-key
// lookups) follow the exact same one-purpose-per-class shape and are left out here.
