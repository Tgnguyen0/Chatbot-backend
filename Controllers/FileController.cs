using Microsoft.AspNetCore.Mvc;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Tesseract;

namespace ChatBotApi.Controllers
{
    /// <summary>
    /// Controller quản lý các tác vụ xử lý tệp tin (Đọc văn bản từ TXT, PDF, DOCX và OCR hình ảnh)
    /// </summary>
    [ApiController]
    [Route("api/files")]
    public class FileController : ControllerBase
    {
        /// <summary>
        /// API endpoint tiếp nhận tệp tải lên từ client, đọc và trích xuất toàn bộ nội dung văn bản.
        /// </summary>
        /// <param name="file">Tệp đính kèm được tải lên (IFormFile)</param>
        /// <returns>JSON chứa thông tin tệp (tên, định dạng, kích thước) và chuỗi văn bản đã trích xuất</returns>
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            // 1. Kiểm tra sự tồn tại của tệp
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Không có file." });

            // 2. Giới hạn dung lượng tệp tải lên (Tối đa 10MB)
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { error = "File quá lớn, tối đa 10MB." });

            // Lấy phần mở rộng của tệp và chuyển về chữ thường để so sánh
            var ext = Path.GetExtension(file.FileName).ToLower();

            // 3. Ràng buộc các định dạng được phép xử lý (.txt, .pdf, .docx)
            if (ext is not (".txt" or ".pdf" or ".docx"))
                return BadRequest(new { error = "Chỉ hỗ trợ .txt, .pdf, .docx." });

            try
            {
                // 4. Bắt đầu đọc nội dung dựa theo định dạng tệp (dùng pattern matching)
                var content = ext switch
                {
                    ".txt"  => await ReadTxt(file),
                    ".pdf"  => ReadPdf(file),
                    ".docx" => ReadDocx(file),
                    _       => throw new Exception("Định dạng không hỗ trợ.")
                };

                // Chuẩn hóa tên định dạng hiển thị cho giao diện UI
                var fileType = ext switch
                {
                    ".txt"  => "TXT",
                    ".pdf"  => "PDF",
                    ".docx" => "Word",
                    _       => ext.ToUpper()
                };

                // Nếu sau khi trích xuất mà văn bản thu được bị trống
                if (string.IsNullOrWhiteSpace(content))
                    return BadRequest(new { error = "Không đọc được nội dung file." });

                // Trả về kết quả thành công cho Client
                return Ok(new { fileName = file.FileName, fileType, content, size = file.Length });
            }
            catch (Exception ex)
            {
                // Bắt các ngoại lệ phát sinh trong quá trình đọc file / OCR
                return BadRequest(new { error = "Lỗi đọc file: " + ex.Message });
            }
        }

        /// <summary>
        /// Đọc nội dung từ tệp văn bản thuần (.txt)
        /// </summary>
        /// <param name="file">Tệp TXT cần đọc</param>
        /// <returns>Chuỗi văn bản mã hóa UTF-8</returns>
        private async Task<string> ReadTxt(IFormFile file)
        {
            // Khởi tạo StreamReader đọc luồng dữ liệu tệp
            // Tự động phát hiện BOM (Byte Order Marks) và sử dụng mã hóa UTF-8 chuẩn
            using var reader = new StreamReader(
                file.OpenReadStream(),
                System.Text.Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );
            
            // Đọc bất đồng bộ toàn bộ nội dung tệp đến cuối
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Trích xuất nội dung từ tệp PDF. Kết hợp đọc Text thuần và OCR nếu là PDF quét dạng ảnh.
        /// </summary>
        /// <param name="file">Tệp PDF cần xử lý</param>
        /// <returns>Chuỗi văn bản thu được sau khi ghép tất cả các trang</returns>
        private string ReadPdf(IFormFile file)
        {
            // Mở luồng dữ liệu tệp và copy sang MemoryStream để đọc mảng byte
            using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var pdfBytes = ms.ToArray();

            // Mở tài liệu PDF bằng thư viện PdfPig
            using var pdf = PdfDocument.Open(pdfBytes);

            var pages = pdf.GetPages().ToList();

            // CHỦ TRƯƠNG 1: Thử đọc text thuần trực tiếp từ cấu trúc PDF
            var textPages = pages
                .Select(p => p.Text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            // Nếu PDF có sẵn lớp Text (vector/searchable PDF) -> Trả về ngay, không mất chi phí OCR
            if (textPages.Count > 0)
            {
                return string.Join("\n\n", textPages);
            }

            // CHỦ TRƯƠNG 2: PDF không có lớp Text (Scanned PDF/Toàn bộ là ảnh) -> Tiến hành OCR từng trang
            var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
            var results = new List<string>();

            // Khởi tạo Engine OCR Tesseract hỗ trợ đa ngôn ngữ (Tiếng Việt + Tiếng Anh)
            using var engine = new TesseractEngine(tessDataPath, "vie+eng", EngineMode.Default);

            foreach (var page in pages)
            {
                // Lấy danh sách các hình ảnh được nhúng trong trang PDF
                var images = page.GetImages().ToList();

                foreach (var img in images)
                {
                    try
                    {
                        // Đưa dữ liệu thô của ảnh vào MemoryStream
                        using var imgMs = new MemoryStream(img.RawBytes.ToArray());
                        
                        // Load dữ liệu ảnh vào đối tượng Pix của Tesseract
                        using var pix = Pix.LoadFromMemory(imgMs.ToArray());
                        
                        // Tiến hành nhận diện mặt chữ (OCR)
                        using var ocrPage = engine.Process(pix);
                        var ocrText = ocrPage.GetText().Trim();
                        
                        // Nếu nhận diện được chữ -> Lưu kết quả
                        if (!string.IsNullOrWhiteSpace(ocrText))
                            results.Add(ocrText);
                    }
                    catch 
                    { 
                        /* Bỏ qua các hình ảnh bị hỏng/lỗi định dạng để không làm gián đoạn luồng xử lý */ 
                    }
                }
            }

            // Nếu cả đọc Text và OCR đều không thu được kết quả
            if (results.Count == 0)
                throw new Exception("Không đọc được nội dung PDF — thử file khác.");

            return string.Join("\n\n", results);
        }

        /// <summary>
        /// Trích xuất nội dung từ tệp Microsoft Word (.docx). Đọc cả Paragraphs và OCR các ảnh đính kèm.
        /// </summary>
        /// <param name="file">Tệp Word cần xử lý</param>
        /// <returns>Toàn bộ văn bản bao gồm cả chữ nhận diện được trong hình ảnh nhúng</returns>
        private string ReadDocx(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            
            // Mở tài liệu Word bằng OpenXML SDK (chế độ chỉ đọc: isEditable = false)
            using var doc = WordprocessingDocument.Open(stream, false);

            // Kiểm tra và lấy phần thân (Body) của tài liệu Word
            var body = doc.MainDocumentPart?.Document?.Body
                ?? throw new Exception("File Word không có nội dung.");

            var results = new List<string>();

            // BƯỚC 1: Lấy toàn bộ Text thuần từ tất cả các đoạn văn (Paragraphs) trong tệp
            var textContent = string.Join("\n", body.Elements<Paragraph>()
                .Select(p => p.InnerText)
                .Where(t => !string.IsNullOrWhiteSpace(t)));

            if (!string.IsNullOrWhiteSpace(textContent))
                results.Add(textContent);

            // BƯỚC 2: Kiểm tra và OCR đối với các hình ảnh được nhúng trong tài liệu Word
            var imageParts = doc.MainDocumentPart?.ImageParts?.ToList();
            if (imageParts != null && imageParts.Count > 0)
            {
                var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");
                using var engine = new TesseractEngine(tessDataPath, "vie+eng", EngineMode.Default);

                foreach (var imagePart in imageParts)
                {
                    try
                    {
                        // Đọc luồng dữ liệu ảnh từ ImagePart của OpenXML
                        using var imgStream = imagePart.GetStream();
                        using var imgMs = new MemoryStream();
                        imgStream.CopyTo(imgMs);

                        // Thực hiện OCR bằng Tesseract Engine
                        using var pix = Pix.LoadFromMemory(imgMs.ToArray());
                        using var ocrPage = engine.Process(pix);
                        var ocrText = ocrPage.GetText().Trim();

                        // Nếu tìm thấy chữ trong ảnh nhúng -> Gắn thêm nhãn đánh dấu
                        if (!string.IsNullOrWhiteSpace(ocrText))
                            results.Add($"[Nội dung ảnh trong tài liệu]\n{ocrText}");
                    }
                    catch 
                    { 
                        /* Bỏ qua các ảnh nhúng bị lỗi đọc dữ liệu */ 
                    }
                }
            }

            // Nếu cả tài liệu không chứa văn bản lẫn hình ảnh có chữ
            if (results.Count == 0)
                throw new Exception("File Word không có nội dung.");

            return string.Join("\n\n", results);
        }
    }
}