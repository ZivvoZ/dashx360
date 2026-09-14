using System;
using System.Buffers;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XboxMetroLauncher.Utilities;

internal sealed record CompactBitmap(BitmapSource Bitmap, Int32Rect VisibleBounds, Int32Rect PixelBounds, DrawingImage Artwork)
{
    internal static CompactBitmap Load(string path)
    {
        using FileStream stream = File.OpenRead(path);
        BitmapSource source = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        if (source.PixelWidth != 1920 || source.PixelHeight != 1080)
            throw new InvalidOperationException($"Blades artwork must be 1920x1080: '{path}'.");
        if (source.Format != PixelFormats.Pbgra32)
            source = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);

        int stride = source.PixelWidth * 4;
        byte[] pixels = ArrayPool<byte>.Shared.Rent(stride * source.PixelHeight);
        try
        {
            source.CopyPixels(pixels, stride, 0);
            int minX=source.PixelWidth, minY=source.PixelHeight, maxX=-1, maxY=-1;
            for (int y=0; y<source.PixelHeight; y++)
            for (int x=0; x<source.PixelWidth; x++)
            {
                if (pixels[y*stride+x*4+3] == 0) continue;
                minX=Math.Min(minX,x); minY=Math.Min(minY,y);
                maxX=Math.Max(maxX,x); maxY=Math.Max(maxY,y);
            }
            Int32Rect visible = maxX < minX ? new(0,0,1920,1080) : new(minX,minY,maxX-minX+1,maxY-minY+1);
            // A transparent border preserves filtering; the drawing retains canvas coordinates.
            int left=Math.Max(0,visible.X-1), top=Math.Max(0,visible.Y-1);
            int right=Math.Min(1920,visible.X+visible.Width+1), bottom=Math.Min(1080,visible.Y+visible.Height+1);
            Int32Rect bounds = new(left,top,right-left,bottom-top);
            int croppedStride=bounds.Width*4;
            byte[] cropped = ArrayPool<byte>.Shared.Rent(croppedStride*bounds.Height);
            BitmapSource bitmap;
            try
            {
                for (int y=0; y<bounds.Height; y++)
                    Buffer.BlockCopy(pixels,(top+y)*stride+left*4,cropped,y*croppedStride,croppedStride);
                bitmap=BitmapSource.Create(bounds.Width,bounds.Height,96,96,PixelFormats.Pbgra32,null,cropped,croppedStride);
                bitmap.Freeze();
            }
            finally { ArrayPool<byte>.Shared.Return(cropped); }
            DrawingGroup drawing=new();
            using (DrawingContext context=drawing.Open())
            {
                context.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,1920,1080));
                context.DrawImage(bitmap,new Rect(left,top,bounds.Width,bounds.Height));
            }
            drawing.Freeze();
            DrawingImage artwork=new(drawing);
            artwork.Freeze();
            return new(bitmap,visible,bounds,artwork);
        }
        finally { ArrayPool<byte>.Shared.Return(pixels); }
    }
}
