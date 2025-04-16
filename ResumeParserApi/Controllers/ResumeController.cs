using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ResumeParserApi.Helpers;

namespace ResumeParserApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResumeController : ControllerBase
    {
        [HttpPost("parse")]
        public async Task<IActionResult> ParseResumes([FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            var tempFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "temp");
            Directory.CreateDirectory(tempFolderPath);  // Ensure folder exists

            var results = new List<object>();

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (fileExtension != ".pdf")
                    return BadRequest("Only PDF format is supported at this stage.");

                var savedPdfPath = Path.Combine(tempFolderPath, file.FileName);

                // Save the uploaded PDF file to the temp folder
                using (var stream = new FileStream(savedPdfPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Extract OCR text and name data from the PDF
                var (ocrText, nameData) = await ResumeParserHelpers.ProcessPdfAndGenerateAssets(savedPdfPath, tempFolderPath);

                // Extract phone number from OCR text
                var phone = ResumeParserHelpers.ExtractPhoneNumber(ocrText);
                var (primaryEmail, otherEmails) = ResumeParserHelpers.ExtractEmails(ocrText);
                var qualifications = ResumeParserHelpers.ExtractQualifications(ocrText);
                var skills = ResumeParserHelpers.ExtractSkills(ocrText);
                var (city, state) = ResumeParserHelpers.GetCityState(ocrText);


                results.Add(new
                {
                    FirstName = nameData?.FirstName,
                    MiddleName = nameData?.MiddleName,
                    LastName = nameData?.LastName,
                    FatherName = nameData?.FatherName,
                    PhoneNumber = phone,
                    PrimaryEmail = primaryEmail,
                    OtherEmails = otherEmails,
                    qualifications = qualifications,
                    skills = skills,
                    city = city,
                    state = state
                });
            }

            return Ok(results);
        }
    }
}
