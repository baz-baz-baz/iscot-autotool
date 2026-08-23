using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    public static class PassaggioConsegnePdfExporter
    {
        /// <summary>
        /// Numero massimo di pixel della bitmap di render. Una <see cref="RenderTargetBitmap"/>
        /// Pbgra32 occupa 4 byte per pixel in un unico blocco contiguo, che finisce sempre nella
        /// Large Object Heap: un rapportino 3000×4000 sono ~48 MB allocati in un colpo solo, e su
        /// una macchina d'officina con poca RAM libera l'allocazione può fallire del tutto
        /// (criticità E di PROJECT_MEMORY.md §6.4). Con questo tetto la bitmap non supera i ~32 MB
        /// e viene ridotta in scala proporzionalmente, cioè esattamente ciò che
        /// <c>gfx.DrawImage</c> faceva comunque subito dopo per adattarla alla pagina PDF.
        /// </summary>
        private const double MaxRenderPixels = 8_000_000;

        public static string ExportToPdf(FrameworkElement element, string tipoTreno)
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();

            int width = (int)element.ActualWidth;
            int height = (int)element.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                width = 1200;
                height = 900;
            }

            // La risoluzione di render viene ridotta solo se il rapportino supera il tetto: nei casi
            // normali (rapportini di dimensione ordinaria) dpi resta 96 e l'immagine è identica a
            // prima, bit per bit. Il ridimensionamento agisce sui DPI invece che sulle dimensioni in
            // pixel, così l'elemento non viene rimisurato e il layout catturato resta lo stesso.
            double dpi = 96.0;
            double totalPixels = (double)width * height;
            if (totalPixels > MaxRenderPixels)
            {
                double scale = Math.Sqrt(MaxRenderPixels / totalPixels);
                dpi = 96.0 * scale;
                width = Math.Max(1, (int)(width * scale));
                height = Math.Max(1, (int)(height * scale));
            }

            RenderTargetBitmap rtb = new(width, height, dpi, dpi, PixelFormats.Pbgra32);
            rtb.Render(element);

            // Il MemoryStream contiene il PNG dell'intero rapportino (facilmente vari MB, quindi
            // allocato nella Large Object Heap). Prima non veniva mai chiuso: ogni esportazione
            // lasciava il buffer in attesa del GC. Viene rilasciato in finally, cioè solo dopo
            // document.Save(), perché PdfSharp legge i byte dell'immagine al momento del salvataggio.
            MemoryStream ms = new();
            try
            {
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(ms);
                ms.Position = 0;

                string tempFolder = Path.Combine(Path.GetTempPath(), "PersonalAutomationTool", "PassaggioConsegne");
                Directory.CreateDirectory(tempFolder);

                string fileName = "Rapportino di turno.pdf";
                string pdfPath = Path.Combine(tempFolder, fileName);

                using (PdfDocument document = new())
                {
                    document.Info.Title = $"Rapportino Turno - {tipoTreno}";
                    PdfPage page = document.AddPage();
                    page.Orientation = PdfSharp.PageOrientation.Landscape;

                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    using (XImage xImage = XImage.FromStream(ms))
                    {
                        double pageWidth = page.Width.Point;
                        double pageHeight = page.Height.Point;

                        double scale = Math.Min(pageWidth / xImage.PixelWidth, pageHeight / xImage.PixelHeight);
                        double drawWidth = xImage.PixelWidth * scale;
                        double drawHeight = xImage.PixelHeight * scale;

                        double x = (pageWidth - drawWidth) / 2;
                        double y = (pageHeight - drawHeight) / 2;

                        gfx.DrawImage(xImage, x, y, drawWidth, drawHeight);
                    }

                    document.Save(pdfPath);
                }

                return pdfPath;
            }
            finally
            {
                ms.Dispose();
            }
        }
    }
}
