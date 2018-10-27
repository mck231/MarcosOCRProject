using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MarcosOCRProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TesseractFileManagerController : ControllerBase
    {
        [HttpPost("UploadFiles")]
        public async Task<IActionResult> Post(List<IFormFile> files)
        {
            long size = files.Sum(f => f.Length);

            // full path to file in temp location
            var filePath = Path.GetTempFileName();

            foreach (var formFile in files)
            {
                if (formFile.Length > 0)
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await formFile.CopyToAsync(stream);
                    }
                }
            }

            // process uploaded files
            // Don't rely on or trust the FileName property without validation.

            return Ok(new { count = files.Count, size, filePath });
        }
        private static void Tesseract(string[] args)
        {
            var solutionDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;

            var tesseractPath = solutionDirectory + @"\tesseract-master.1153";
            var testFiles = Directory.EnumerateFiles(solutionDirectory + @"\samples");

            var maxDegreeOfParallelism = Environment.ProcessorCount;
            Parallel.ForEach(testFiles, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism }, (fileName) =>
            {
                var imageFile = System.IO.File.ReadAllBytes(fileName);
                var text = ParseText(tesseractPath, imageFile, "eng");
                Console.WriteLine("File:" + fileName + "\n" + text + "\n");
            });
            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }

        private static string ParseText(string tesseractPath, byte[] imageFile, params string[] lang)
        {
            string output = string.Empty;
            var tempOutputFile = Path.GetTempPath() + Guid.NewGuid();
            var tempImageFile = Path.GetTempFileName();

            try
            {
                System.IO.File.WriteAllBytes(tempImageFile, imageFile);

                ProcessStartInfo info = new ProcessStartInfo();
                info.WorkingDirectory = tesseractPath;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.UseShellExecute = false;
                info.FileName = "cmd.exe";
                info.Arguments =
                    "/c tesseract.exe " +
                    // Image file.
                    tempImageFile + " " +
                    // Output file (tesseract add '.txt' at the end)
                    tempOutputFile +
                    // Languages.
                    " -l " + string.Join("+", lang);

                // Start tesseract.
                Process process = Process.Start(info);
                if (process != null)
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        // Exit code: success.
                        output = System.IO.File.ReadAllText(tempOutputFile + ".txt");
                    }
                    else
                    {
                        throw new Exception("Error. Tesseract stopped with an error code = " + process.ExitCode);
                    }
                }
            }
            finally
            {
                System.IO.File.Delete(tempImageFile);
                System.IO.File.Delete(tempOutputFile + ".txt");
            }
            return output;
        }
    }
}
