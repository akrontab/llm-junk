using UglyToad.PdfPig;
using System.Text;

namespace DataIngestion.FileReaders
{
    internal class PdfFileReader : IFileReader
    {
        public async Task<string> GetFileContentAsync(Stream data)
        {
            // PdfPig needs to be able to seek through the PDF structure
            // If the stream doesn't support seeking (like a network stream),
            // we copy it to a MemoryStream first.
            if (!data.CanSeek)
            {
                var ms = new MemoryStream();
                await data.CopyToAsync(ms);
                ms.Position = 0;
                data = ms;
            }

            var sb = new StringBuilder();

            using (var document = PdfDocument.Open(data))
            {
                foreach (var page in document.GetPages())
                {
                    // Extract text from the page
                    var pageText = page.Text;

                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        // We add a page marker so the chunker knows where logical breaks are
                        sb.AppendLine($"--- Page {page.Number} ---");
                        sb.AppendLine(pageText);
                        sb.AppendLine();
                    }
                }
            }

            return sb.ToString();
        }
    }
}