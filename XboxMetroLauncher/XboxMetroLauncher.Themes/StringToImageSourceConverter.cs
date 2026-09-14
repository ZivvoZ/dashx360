using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XboxMetroLauncher.Services;

namespace XboxMetroLauncher.Themes;

public sealed class StringToImageSourceConverter : IValueConverter
{
	public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = value?.ToString();
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		try
		{
			int num = ResolveDecodeWidth(text, parameter?.ToString());
			if (TryCreateRemoteUri(text, out Uri uri))
			{
				return LoadRemoteImage(uri, num);
			}
			text = AppPathResolver.Resolve(text);
			if (!File.Exists(text))
			{
				return null;
			}
			return ImageCacheService.GetDecodedImage(text, num);
		}
		catch
		{
			return null;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}

	private static bool TryCreateRemoteUri(string path, out Uri uri)
	{
		if (Uri.TryCreate(path, UriKind.Absolute, out uri) && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		uri = null;
		return false;
	}

	private static ImageSource? LoadRemoteImage(Uri uri, int decodeWidth)
	{
		return ImageCacheService.GetOrCreate($"remote-image|{uri.AbsoluteUri}|w={decodeWidth}", uri.AbsoluteUri, delegate
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache;
			bitmapImage.UriSource = uri;
			if (decodeWidth > 0)
			{
				bitmapImage.DecodePixelWidth = decodeWidth;
			}
			bitmapImage.EndInit();
			if (bitmapImage.CanFreeze) bitmapImage.Freeze();
			return bitmapImage;
		});
	}

	private static int ResolveDecodeWidth(string path, string? parameter)
	{
		if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		string text = path.Replace('\\', '/').ToLowerInvariant();
		if (text.Contains("/profile/") || text.Contains("/friendpool/") || text.Contains("gamerpic") || text.Contains("avatar"))
		{
			return 96;
		}
		if (text.Contains("/background") || text.Contains("/boot/") || text.Contains("home screen"))
		{
			return 1280;
		}
		if (!text.Contains("/tiles/") && !text.Contains("/marketplace/") && !text.Contains("/misc/") && !text.Contains("cover"))
		{
			text.Contains("art");
		}
		return 320;
	}
}
