using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using System.Drawing;

namespace LivenessDetection.Services
{
    public class LivenessDetectionService
    {
        private readonly CascadeClassifier _faceCascade;
        private readonly CascadeClassifier _eyeCascade;
        private readonly string _capturedImagesPath;
        private readonly ILogger<LivenessDetectionService> _logger;

        public LivenessDetectionService(ILogger<LivenessDetectionService> logger)
        {
            _logger = logger;
            
            // Initialize cascades for face and eye detection
            _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
            _eyeCascade = new CascadeClassifier("haarcascade_eye.xml");
            
            // Setup directory for captured images
            _capturedImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "CapturedImages");
            if (!Directory.Exists(_capturedImagesPath))
            {
                Directory.CreateDirectory(_capturedImagesPath);
            }
        }

        public class DetectionResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; } = string.Empty;
            public int FaceCount { get; set; }
            public bool IsBlurred { get; set; }
            public bool HasEyes { get; set; }
            public double BlurScore { get; set; }
            public string? CapturedImagePath { get; set; }
        }

        // public DetectionResult ProcessImage(byte[] imageData)
        // {
        //     try
        //     {
        //         using var mat = new Mat();
        //         CvInvoke.Imdecode(imageData, ImreadModes.Color, mat);

        //         if (mat.IsEmpty)
        //         {
        //             return new DetectionResult 
        //             { 
        //                 IsValid = false, 
        //                 Message = "Failed to decode image" 
        //             };
        //         }

        //         var result = new DetectionResult();

        //         // Step 1: Detect faces
        //         var faces = DetectFaces(mat);
        //         result.FaceCount = faces.Length;

        //         if (faces.Length == 0)
        //         {
        //             result.IsValid = false;
        //             result.Message = "No face detected. Please position your face in front of the camera.";
        //             return result;
        //         }

        //         if (faces.Length > 1)
        //         {
        //             result.IsValid = false;
        //             result.Message = $"Multiple faces detected ({faces.Length}). Only one person should be in frame.";
        //             return result;
        //         }

        //         // Step 2: Check for blur
        //         var blurScore = CalculateBlurScore(mat);
        //         result.BlurScore = blurScore;
        //         result.IsBlurred = blurScore < 100; // Threshold for blur detection

        //         if (result.IsBlurred)
        //         {
        //             result.IsValid = false;
        //             result.Message = $"Image is too blurry (score: {blurScore:F2}). Please ensure good lighting and hold steady.";
        //             return result;
        //         }

        //         // Step 3: Detect eyes for liveness (anti-spoofing)
        //         var faceRegion = faces[0];
        //         var faceRoi = new Mat(mat, faceRegion);
        //         var eyes = DetectEyes(faceRoi);
        //         result.HasEyes = eyes.Length >= 2;

        //         if (!result.HasEyes)
        //         {
        //             result.IsValid = false;
        //             result.Message = "Liveness check failed. Please ensure your eyes are clearly visible and not covered.";
        //             return result;
        //         }

        //         // All checks passed - save the image
        //         result.IsValid = true;
        //         result.Message = "Verification successful!";
        //         result.CapturedImagePath = SaveImage(mat);

        //         return result;
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error processing image");
        //         return new DetectionResult 
        //         { 
        //             IsValid = false, 
        //             Message = $"Error processing image: {ex.Message}" 
        //         };
        //     }
        // }

        public DetectionResult ProcessImage(byte[] imageData, bool saveImage = false)
{
    try
    {
        using var mat = new Mat();
        CvInvoke.Imdecode(imageData, ImreadModes.Color, mat);

        if (mat.IsEmpty)
            return new DetectionResult { IsValid = false, Message = "Failed to decode image" };

        var result = new DetectionResult();

        // Step 1: Detect faces
        var faces = DetectFaces(mat);
        result.FaceCount = faces.Length;

        if (faces.Length != 1)
        {
            result.IsValid = false;
            result.Message = faces.Length == 0 
                ? "No face detected. Please position your face in front of the camera."
                : $"Multiple faces detected ({faces.Length}). Only one person should be in frame.";
            return result;
        }

        // Step 2: Check blur
        var blurScore = CalculateBlurScore(mat);
        result.BlurScore = blurScore;
        result.IsBlurred = blurScore < 100;

        if (result.IsBlurred)
        {
            result.IsValid = false;
            result.Message = $"Image is too blurry (score: {blurScore:F2}). Please ensure good lighting and hold steady.";
            return result;
        }

        // Step 3: Detect eyes
        var faceRoi = new Mat(mat, faces[0]);
        var eyes = DetectEyes(faceRoi);
        result.HasEyes = eyes.Length >= 2;

        if (!result.HasEyes)
        {
            result.IsValid = false;
            result.Message = "Liveness check failed. Please ensure your eyes are clearly visible and not covered.";
            return result;
        }

        // All checks passed
        result.IsValid = true;
        result.Message = "Verification successful!";
        if (saveImage)
        {
            result.CapturedImagePath = SaveImage(mat);
        }

        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing image");
        return new DetectionResult { IsValid = false, Message = $"Error processing image: {ex.Message}" };
    }
}


        private Rectangle[] DetectFaces(Mat image)
        {
            using var grayImage = new Mat();
            CvInvoke.CvtColor(image, grayImage, ColorConversion.Bgr2Gray);
            CvInvoke.EqualizeHist(grayImage, grayImage);

            var faces = _faceCascade.DetectMultiScale(
                grayImage,
                scaleFactor: 1.1,
                minNeighbors: 8,
                minSize: new Size(60, 60)
            );

            return faces;
        }

        private Rectangle[] DetectEyes(Mat faceImage)
        {
            using var grayFace = new Mat();
            CvInvoke.CvtColor(faceImage, grayFace, ColorConversion.Bgr2Gray);

            var eyes = _eyeCascade.DetectMultiScale(
                grayFace,
                scaleFactor: 1.01,
                minNeighbors: 4,
                minSize: new Size(20, 20)
            );

            return eyes;
        }

        private double CalculateBlurScore(Mat image)
        {
            // Use Laplacian variance to measure blur
            // Higher values = sharper image
            using var gray = new Mat();
            using var laplacian = new Mat();
            
            CvInvoke.CvtColor(image, gray, ColorConversion.Bgr2Gray);
            CvInvoke.Laplacian(gray, laplacian, DepthType.Cv64F);

            MCvScalar mean = new MCvScalar();
            MCvScalar stddev = new MCvScalar();
            CvInvoke.MeanStdDev(laplacian, ref mean, ref stddev);

            // Return variance (stddev squared)
            return stddev.V0 * stddev.V0;
        }

        private string SaveImage(Mat image)
        {
            var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var filePath = Path.Combine(_capturedImagesPath, fileName);
            
            CvInvoke.Imwrite(filePath, image);
            
            _logger.LogInformation($"Image saved: {filePath}");
            return filePath;
        }
    }
}