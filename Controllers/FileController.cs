using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

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
                return BadRequest("Không có file.");

            if (file.Length > 1 * 1024 * 1024) 
                return BadRequest("Chỉ hỗ trợ file .txt.");

            var ext = Path.GetExtension(file.FileName).ToLower();

            if (ext is not (".txt" or ".pdf" or ".docx"))
                return BadRequest("Chỉ hỗ trợ .txt, .pdf, .docx.");

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
                    ".txt" => "TXT",
                    "pdf"  => "PDF",
                    "docx" => "Word",
                    _      => ext.ToUpper()
                };

                if (string.IsNullOrWhiteSpace(content))
                {
                    return BadRequest("Không đọc được nội dung file.");
                }

                return Ok(new
                {
                    fileName = file.FileName,
                    fileType,
                    content,
                    size = file.Length
                });
            } catch (Exception ex)
            {
                return BadRequest("Lỗi đọc file: " + ex.Message);
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
        private string ReadPdf(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            using var pdf = PdfDocument.Open(ms.ToArray());

            var text = string.Join("\n\n", pdf.GetPages()
                .Select(p => p.Text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (string.IsNullOrWhiteSpace(text))
                throw new Exception("PDF này là dạng scan/ảnh, không đọc được text.");

            return text;
        }

        // Đọc Word (.docx) bằng OpenXml
        private string ReadDocx(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var doc = WordprocessingDocument.Open(stream, false);

            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new Exception("File Word không có nội dung.");

            return string.Join("\n", body.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));
        }
    }
}