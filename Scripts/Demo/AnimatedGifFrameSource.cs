using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImage = System.Drawing.Image;
using GodotImage = Godot.Image;

#pragma warning disable CA1416 // Every entry point is guarded by OperatingSystem.IsWindows().

namespace ModularFighter.Demo;

/// <summary>
/// Streams one composited GIF frame into one Godot texture. This keeps the two
/// long hyper-combo animations at one resident texture instead of importing
/// hundreds of full-size PNG textures.
/// </summary>
public sealed class AnimatedGifFrameSource : IDisposable
{
	private const int GifFrameDelayPropertyId = 0x5100;
	private const int DefaultFrameDelayMilliseconds = 30;

	private readonly DrawingImage _source;
	private readonly Stream _encodedStream;
	private readonly FrameDimension _frameDimension;
	private readonly DrawingBitmap _renderTarget;
	private readonly Graphics _renderGraphics;
	private readonly byte[] _drawingRow;
	private readonly byte[] _rgbaPixels;
	private readonly int[] _frameDelayMilliseconds;
	private readonly GodotImage _godotImage;
	private double _elapsedMilliseconds;
	private bool _disposed;

	public string ResourcePath { get; }
	public int Width => _source.Width;
	public int Height => _source.Height;
	public int FrameCount { get; }
	public int CurrentFrame { get; private set; }
	public ImageTexture Texture { get; }

	private AnimatedGifFrameSource(string resourcePath)
	{
		ResourcePath = resourcePath;
		_encodedStream = OpenEncodedStream(resourcePath);
		_source = DrawingImage.FromStream(_encodedStream);
		_frameDimension = new FrameDimension(_source.FrameDimensionsList[0]);
		FrameCount = Mathf.Max(1, _source.GetFrameCount(_frameDimension));
		_frameDelayMilliseconds = ReadFrameDelays(_source, FrameCount);
		_renderTarget = new DrawingBitmap(Width, Height, PixelFormat.Format32bppArgb);
		_renderGraphics = Graphics.FromImage(_renderTarget);
		_renderGraphics.CompositingMode = CompositingMode.SourceCopy;
		_renderGraphics.InterpolationMode = InterpolationMode.NearestNeighbor;
		_renderGraphics.PixelOffsetMode = PixelOffsetMode.Half;
		_drawingRow = new byte[Width * 4];
		_rgbaPixels = new byte[Width * Height * 4];
		UploadFrame(0);
		_godotImage = GodotImage.CreateFromData(Width, Height, false, GodotImage.Format.Rgba8, _rgbaPixels);
		Texture = ImageTexture.CreateFromImage(_godotImage);
	}

	public static bool TryOpen(string resourcePath, out AnimatedGifFrameSource source, out string error)
	{
		source = null;
		error = "";
		if (!OperatingSystem.IsWindows())
		{
			error = "animated GIF playback currently requires the Windows build";
			return false;
		}
		if (string.IsNullOrWhiteSpace(resourcePath))
		{
			error = "no GIF resource path was supplied";
			return false;
		}
		try
		{
			source = new AnimatedGifFrameSource(resourcePath);
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			source?.Dispose();
			source = null;
			return false;
		}
	}

	private static Stream OpenEncodedStream(string resourcePath)
	{
		string embeddedName = $"ModularFighter.Assets.Backgrounds.{Path.GetFileName(resourcePath)}";
		Stream embedded = typeof(AnimatedGifFrameSource).Assembly.GetManifestResourceStream(embeddedName);
		if (embedded != null) return embedded;
		if (!Godot.FileAccess.FileExists(resourcePath))
			throw new FileNotFoundException($"GIF resource '{resourcePath}' is neither embedded nor available through Godot FileAccess");
		return new MemoryStream(Godot.FileAccess.GetFileAsBytes(resourcePath), false);
	}

	public void Advance(double deltaSeconds)
	{
		if (_disposed || FrameCount <= 1 || deltaSeconds <= 0.0) return;
		_elapsedMilliseconds += deltaSeconds * 1000.0;
		int safety = FrameCount;
		bool frameChanged = false;
		while (safety-- > 0 && _elapsedMilliseconds >= _frameDelayMilliseconds[CurrentFrame])
		{
			_elapsedMilliseconds -= _frameDelayMilliseconds[CurrentFrame];
			CurrentFrame = (CurrentFrame + 1) % FrameCount;
			frameChanged = true;
		}
		// At high playback multipliers, several source frames elapse during one
		// 60 Hz game tick. Only decode/upload the final visible frame for that tick.
		if (!frameChanged) return;
		UploadFrame(CurrentFrame);
		_godotImage.SetData(Width, Height, false, GodotImage.Format.Rgba8, _rgbaPixels);
		Texture.Update(_godotImage);
	}

	private void UploadFrame(int frameIndex)
	{
		_source.SelectActiveFrame(_frameDimension, frameIndex);
		_renderGraphics.Clear(System.Drawing.Color.Transparent);
		_renderGraphics.DrawImageUnscaled(_source, 0, 0);
		var bounds = new Rectangle(0, 0, Width, Height);
		BitmapData bitmapData = _renderTarget.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
		try
		{
			for (int y = 0; y < Height; y++)
			{
				IntPtr rowPointer = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
				Marshal.Copy(rowPointer, _drawingRow, 0, _drawingRow.Length);
				int destinationRow = y * Width * 4;
				for (int x = 0; x < Width; x++)
				{
					int sourcePixel = x * 4;
					int destinationPixel = destinationRow + sourcePixel;
					_rgbaPixels[destinationPixel] = _drawingRow[sourcePixel + 2];
					_rgbaPixels[destinationPixel + 1] = _drawingRow[sourcePixel + 1];
					_rgbaPixels[destinationPixel + 2] = _drawingRow[sourcePixel];
					_rgbaPixels[destinationPixel + 3] = _drawingRow[sourcePixel + 3];
				}
			}
		}
		finally
		{
			_renderTarget.UnlockBits(bitmapData);
		}
	}

	private static int[] ReadFrameDelays(DrawingImage source, int frameCount)
	{
		var delays = new int[frameCount];
		Array.Fill(delays, DefaultFrameDelayMilliseconds);
		try
		{
			byte[] delayBytes = source.GetPropertyItem(GifFrameDelayPropertyId).Value;
			int storedFrames = Mathf.Min(frameCount, delayBytes.Length / sizeof(int));
			for (int i = 0; i < storedFrames; i++)
			{
				int centiseconds = BitConverter.ToInt32(delayBytes, i * sizeof(int));
				delays[i] = Mathf.Max(10, centiseconds * 10);
			}
		}
		catch (ArgumentException)
		{
			// A GIF without timing metadata uses the standard 30 ms fallback above.
		}
		return delays;
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		Texture?.Dispose();
		_godotImage?.Dispose();
		_renderGraphics?.Dispose();
		_renderTarget?.Dispose();
		_source?.Dispose();
		_encodedStream?.Dispose();
	}
}

#pragma warning restore CA1416
