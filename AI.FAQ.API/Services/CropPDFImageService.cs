using SkiaSharp;
using UglyToad.PdfPig.Rendering.Skia;

namespace AI.FAQ.API.Services
{
    public static class CropPDFImageService
    {
        public static SkiaSharp.SKBitmap RenderPdfToImage(Stream pdfStream)
        {
            pdfStream.Position = 0; // Ensure stream is at the beginning
            if (!pdfStream.CanSeek)
            {
                var ms = new MemoryStream();
                pdfStream.CopyTo(ms);
                ms.Position = 0;
                pdfStream = ms;
            }

            using var document = UglyToad.PdfPig.PdfDocument.Open(
                pdfStream,
                SkiaRenderingParsingOptions.Instance
            );

            document.AddSkiaPageFactory();

            int pageNumber = 1;
            float scale = 2.0f;

            // Use the overload your version supports
            using var bitmap = document.GetPageAsSKBitmap(pageNumber, scale);

            return bitmap.Copy();
        }

        public static void CropAndSave(SKBitmap pageBmp, IReadOnlyList<float> polygon,
           float scaleX, float scaleY, string outPath)
        {
            var (x, y, w, h) = PolygonToRect(polygon);

            var rect = new SKRectI(
                (int)(x * scaleX),
                (int)(y * scaleY),
                (int)((x + w) * scaleX),
                (int)((y + h) * scaleY)
            );

            using var subset = new SKBitmap(rect.Width, rect.Height);
            pageBmp.ExtractSubset(subset, rect);

            using var img = SKImage.FromBitmap(subset);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            using var fs = System.IO.File.OpenWrite(outPath);
            data.SaveTo(fs);
        }

        private static (float x, float y, float w, float h) PolygonToRect(IReadOnlyList<float> p)
        {
            var xs = new List<float>();
            var ys = new List<float>();

            for (int i = 0; i < p.Count; i += 2)
            {
                xs.Add(p[i]);
                ys.Add(p[i + 1]);
            }

            float minX = xs.Min();
            float maxX = xs.Max();
            float minY = ys.Min();
            float maxY = ys.Max();

            return (minX, minY, maxX - minX, maxY - minY);
        }

    }
}
