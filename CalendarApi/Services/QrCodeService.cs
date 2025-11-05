using QRCoder;
using SkiaSharp;

namespace CalendarApi.Services
{
    public class QrCodeService
    {
        public byte[] GenerateQrCode(string url)
        {
            // Generate QR code data
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);

            // Use SkiaSharp renderer instead of System.Drawing
            var qrCode = new SKBitmap(qrData.ModuleMatrix.Count * 20, qrData.ModuleMatrix.Count * 20);

            using (var canvas = new SKCanvas(qrCode))
            {
                canvas.Clear(SKColors.White);

                int pixelsPerModule = 20;
                for (int y = 0; y < qrData.ModuleMatrix.Count; y++)
                {
                    for (int x = 0; x < qrData.ModuleMatrix.Count; x++)
                    {
                        if (qrData.ModuleMatrix[y][x])
                        {
                            var rect = new SKRect(
                                x * pixelsPerModule,
                                y * pixelsPerModule,
                                (x + 1) * pixelsPerModule,
                                (y + 1) * pixelsPerModule
                            );
                            using var paint = new SKPaint { Color = SKColors.Black };
                            canvas.DrawRect(rect, paint);
                        }
                    }
                }
            }

            // Save to PNG
            using var image = SKImage.FromBitmap(qrCode);
            using var stream = new MemoryStream();
            image.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
            return stream.ToArray();
        }
    }
}
