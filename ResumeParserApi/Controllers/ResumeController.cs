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

            var results = new List<object>();

            foreach (var file in files)
            {
                var fileExtension = Path.GetExtension(file.FileName).ToLower();
                if (fileExtension != ".pdf")
                    return BadRequest("Only PDF format is supported.");

                try
                {
                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    //  process using memory stream
                    (string ocrText, dynamic nameData) = await ResumeParserHelpers.ProcessPdfStream(memoryStream);

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
                        qualifications,
                        skills,
                        city,
                        state
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { File = file.FileName, Error = ex.Message });
                }
            }

            return Ok(results);
        }

    }
}
