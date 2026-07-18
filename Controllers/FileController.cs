using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Tesseract;

namespace ChatBotApi.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FileController : ControllerBase
    {
        // POST /api/files/upload — nhận file TXT, trả về nội dung text
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không có file." });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "File quá lớn, tối đa 10MB." });

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ext is not (".txt" or ".pdf" or ".docx"))
                return BadRequest(new { error = "Chỉ hỗ trợ .txt, .pdf, .docx." });

            try
            {
                var content = ext switch
                {
                    ".txt"  => await ReadTxt(file),
                    ".pdf"  => ReadPdf(file),
                    ".docx" => ReadDocx(file),
                    _       => throw new Exception("Định dạng không hỗ trợ.")
                };

                var fileType = ext switch
                {
                    ".txt"  => "TXT",
                    ".pdf"  => "PDF",
                    ".docx" => "Word",
                    _       => ext.ToUpper()
                };

                if (string.IsNullOrWhiteSpace(content))
                    return BadRequest(new { error = "Không đọc được nội dung file." });

                return Ok(new { fileName = file.FileName, fileType, content, size = file.Length });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Lỗi đọc file: " + ex.Message });
            }
        }

        // Đọc TXT
        private async Task<string> ReadTxt(IFormFile file)
        {
            using var reader = new StreamReader(
                file.OpenReadStream(),
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );
            return await reader.ReadToEndAsync();
        }

        // Đọc PDF bằng PdfPig
        // Đọc PDF — thử extract text trước, nếu không có thì OCR từng trang
        private string ReadPdf(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var pdfBytes = ms.ToArray();

            using var pdf = PdfDocument.Open(pdfBytes);

            var pages = pdf.GetPages().ToList();

            // Thử đọc text thuần trước
            var textPages = pages
                .Select(p => p.Text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (textPages.Count > 0)
            {
                // PDF có text → trả về luôn, không cần OCR
                return string.Join("\n\n", textPages);
            }

            // PDF toàn ảnh → OCR từng trang bằng Tesseract
            var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
            var results = new List<string>();

            using var engine = new TesseractEngine(tessDataPath, "vie+eng", EngineMode.Default);

            foreach (var page in pages)
            {
                // Render trang PDF thành ảnh bitmap
                // PdfPig không tự render ảnh nên cần dùng PDFium hoặc lấy ảnh nhúng trực tiếp
                var images = page.GetImages().ToList();

                foreach (var img in images)
                {
                    try
                    {
                        using var imgMs = new MemoryStream(img.RawBytes.ToArray());
                        using var pix = Pix.LoadFromMemory(imgMs.ToArray());
                        using var ocrPage = engine.Process(pix);
                        var ocrText = ocrPage.GetText().Trim();
                        if (!string.IsNullOrWhiteSpace(ocrText))
                            results.Add(ocrText);
                    }
                    catch { /* bỏ qua ảnh lỗi */ }
                }
            }

            if (results.Count == 0)
                throw new Exception("Không đọc được nội dung PDF — thử file khác.");

            return string.Join("\n\n", results);
        }

        // Đọc Word (.docx) — lấy text + OCR ảnh nhúng
        private string ReadDocx(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var doc = WordprocessingDocument.Open(stream, false);

            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new Exception("File Word không có nội dung.");

            var results = new List<string>();

            // Đọc text thuần từ các đoạn văn
            var textContent = string.Join("\n", body.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (!string.IsNullOrWhiteSpace(textContent))
                results.Add(textContent);

            // OCR các ảnh nhúng trong file docx
            var imageParts = doc.MainDocumentPart?.ImageParts?.ToList();
            if (imageParts != null && imageParts.Count > 0)
            {
                var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
                using var engine = new TesseractEngine(tessDataPath, "vie+eng", EngineMode.Default);

                foreach (var imagePart in imageParts)
                {
                    try
                    {
                        using var imgStream = imagePart.GetStream();
                        using var imgMs = new MemoryStream();
                        imgStream.CopyTo(imgMs);

                        using var pix = Pix.LoadFromMemory(imgMs.ToArray());
                        using var ocrPage = engine.Process(pix);
                        var ocrText = ocrPage.GetText().Trim();

                        if (!string.IsNullOrWhiteSpace(ocrText))
                            results.Add($"[Nội dung ảnh trong tài liệu]\n{ocrText}");
                    }
                    catch { /* bỏ qua ảnh lỗi */ }
                }
            }

            if (results.Count == 0)
                throw new Exception("File Word không có nội dung.");

            return string.Join("\n\n", results);
        }
    }
}