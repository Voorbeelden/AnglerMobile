using System.Globalization;
using System.Text.RegularExpressions;
using Plugin.Maui.OCR;

namespace AnglerHub.Mobile.Services;

/// <summary>
/// "Smart reading": take a photo of the digital scale, try to read the weight off it
/// automatically. Runs fully on-device (ML Kit on Android, Vision on iOS), so it also
/// works with zero signal — which is the normal case at the waterside.
///
/// Important design rule: this never replaces user confirmation. OCR on a scale photo
/// is never 100% reliable, so the recognized value is only ever pre-filled as a
/// suggestion in the weight field — the angler still has to confirm or correct it
/// before it's actually recorded. See WeighingEntryViewModel.CaptureAndReadAsync().
/// </summary>
public class ScaleReadingService
{
	private readonly IOcrService _ocr;

	public ScaleReadingService(IOcrService ocr)
	{
		_ocr = ocr;
	}

	public async Task InitAsync() => await _ocr.InitAsync();

	/// <summary>Returns null if the user cancels — that's not an error, just falls back to manual entry.</summary>
	public async Task<FileResult?> CapturePhotoAsync()
	{
		if (!MediaPicker.Default.IsCaptureSupported) return null;

		return await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
		{
			Title = "Photo of the scale",
		});
	}

	/// <summary>
	/// Tries to extract a plausible weight (1-3 decimals, between 0 and 50kg — anything
	/// outside that range for a competition catch is almost certainly a misread, so we'd
	/// rather return nothing than a nonsense value).
	/// </summary>
	public async Task<ScaleReadingResult> ReadWeightAsync(string photoPath)
	{
		try
		{
			var bytes = await File.ReadAllBytesAsync(photoPath);
			var result = await _ocr.RecognizeTextAsync(bytes);

			if (!result.Success || result.Elements.Count == 0)
			{
				return ScaleReadingResult.NotFound(photoPath);
			}

			foreach (var element in result.Elements)
			{
				var candidate = ExtractWeight(element.Text);
				if (candidate is null) continue;

				return new ScaleReadingResult
				{
					Success = true,
					Weight = candidate,
					PhotoPath = photoPath,
					// ML Kit gives us a bounding box per recognized text block, which lets
					// the server auto-crop the stored photo down to just the number.
					CropX = element.X,
					CropY = element.Y,
					CropWidth = element.Width,
					CropHeight = element.Height,
				};
			}

			return ScaleReadingResult.NotFound(photoPath);
		}
		catch
		{
			// OCR should never be able to crash the app — just fall back to manual entry.
			return ScaleReadingResult.NotFound(photoPath);
		}
	}

	/// <summary>
	/// Picks a plausible weight out of the recognized text (e.g. "2.450 kg", "2,450",
	/// "02.450"). The decimal separator on a 7-segment display doesn't always come back
	/// as a clean "." or "," from the recognizer, so a few likely variants are accepted too.
	/// </summary>
	private static decimal? ExtractWeight(string? text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;

		var match = Regex.Match(text, @"(\d{1,3}[.,'` ]\d{1,3})");

		if (!match.Success)
		{
			// Also allow a plain integer (some scales just show grams, e.g. "2450")
			// and convert it down to kg.
			var wholeMatch = Regex.Match(text, @"\b(\d{3,5})\b");
			if (wholeMatch.Success && decimal.TryParse(wholeMatch.Groups[1].Value, out var grams) && grams is > 10 and < 50000)
			{
				return grams / 1000m;
			}
			return null;
		}

		var normalized = Regex.Replace(match.Value, @"[.,'` ]", ".");

		if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
		{
			return null;
		}

		return value is > 0 and <= 50 ? value : null;
	}
}

public class ScaleReadingResult
{
	public bool Success { get; set; }
	public decimal? Weight { get; set; }
	public string? PhotoPath { get; set; }
	public int CropX { get; set; }
	public int CropY { get; set; }
	public int CropWidth { get; set; }
	public int CropHeight { get; set; }

	public static ScaleReadingResult NotFound(string photoPath) => new() { Success = false, PhotoPath = photoPath };
}
