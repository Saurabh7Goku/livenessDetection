using LivenessDetection.Services;
using Microsoft.AspNetCore.Mvc;

namespace LivenessDetection.Controllers
{
    public class HomeController : Controller
    {
        private readonly LivenessDetectionService _livenessService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            LivenessDetectionService livenessService,
            ILogger<HomeController> logger)
        {
            _livenessService = livenessService;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessFrame([FromBody] ImageRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.ImageData))
                {
                    return BadRequest(new { success = false, message = "No image data provided" });
                }

                // Remove data:image/jpeg;base64, prefix if present
                var base64Data = request.ImageData.Contains(",") 
                    ? request.ImageData.Split(',')[1] 
                    : request.ImageData;

                var imageBytes = Convert.FromBase64String(base64Data);
                var result = await Task.Run(() => _livenessService.ProcessImage(imageBytes, request.SaveImage));

                return Ok(new
                    {
                        success = result.IsValid,
                        message = result.Message,
                        faceCount = result.FaceCount,
                        isBlurred = result.IsBlurred,
                        hasEyes = result.HasEyes,
                        blurScore = result.BlurScore,
                        imagePath = result.CapturedImagePath
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing frame");
                return StatusCode(500, new { success = false, message = "Server error processing image" });
            }
        }

       public class ImageRequest
{
    public string ImageData { get; set; } = string.Empty;
    public bool SaveImage { get; set; } = false; // New flag to control saving
}
    }
}