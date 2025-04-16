using ImageMagick;
using Tesseract;
using System.Text;
using System.Text.RegularExpressions;

namespace ResumeParserApi.Helpers
{
    public static class ResumeParserHelpers
    {
        public static async Task<(string ocrText, dynamic parsedName)> ProcessPdfStream(Stream pdfStream)
        {
            var allText = new StringBuilder();

            using var images = new MagickImageCollection();
            var settings = new MagickReadSettings { Density = new Density(300) };

            // Read from stream instead of file path
            images.Read(pdfStream, settings);

            foreach (var image in images)
            {
                image.Format = MagickFormat.Png;

                using var memStream = new MemoryStream();
                image.Write(memStream);
                memStream.Position = 0;

                using var img = Pix.LoadFromMemory(memStream.ToArray());
                string tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");

                using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
                using var page = engine.Process(img);
                allText.AppendLine(page.GetText());
            }

            var ocrText = allText.ToString();
            var nameInfo = ExtractNameFromText(ocrText);

            return (ocrText, nameInfo);
        }

        public static async Task<(string ocrText, dynamic parsedName)> ProcessPdfAndGenerateAssets(string pdfPath, string tempFolderPath)
        {
            var imagePaths = ConvertPdfToPngs(pdfPath, tempFolderPath);

            var allText = new StringBuilder();
            foreach (var imagePath in imagePaths)
            {
                string text = RunOcrOnImage(imagePath);
                allText.AppendLine(text);
            }

            var txtPath = Path.ChangeExtension(pdfPath, ".txt");
            var ocrText = allText.ToString();
            await File.WriteAllTextAsync(txtPath, ocrText);

            var nameInfo = ExtractNameFromText(ocrText);

            return (ocrText, nameInfo);
        }

        private static List<string> ConvertPdfToPngs(string pdfPath, string outputFolder)
        {
            var imagePaths = new List<string>();
            using var images = new MagickImageCollection();
            var settings = new MagickReadSettings { Density = new Density(300) };

            images.Read(pdfPath, settings);
            int page = 1;

            foreach (var image in images)
            {
                image.Format = MagickFormat.Png;
                string imagePath = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(pdfPath)}_page{page}.png");
                image.Write(imagePath);
                imagePaths.Add(imagePath);
                page++;
            }

            return imagePaths;
        }

        private static string RunOcrOnImage(string imagePath)
        {
            string tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata");

            using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);
            return page.GetText();
        }

        public static (string city, string state) GetCityState(string text)
        {
            var regex = new Regex(@"\b([A-Za-z]+(?: [A-Za-z]+)*),\s*([A-Za-z]+(?: [A-Za-z]+)*),\s*(\b[A-Za-z]+\b)\b");
            var match = regex.Match(text);
            if (match.Success)
                return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

            regex = new Regex(@"\b([A-Za-z]+(?: [A-Za-z]+)*),\s*([A-Za-z]+(?: [A-Za-z]+)*)\b");
            match = regex.Match(text);
            if (match.Success)
                return (match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim());

            return ("", "");
        }

        public static List<string> LoadSkills()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "skills.txt");
            return File.Exists(path)
                ? File.ReadAllLines(path).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
                : new List<string>();
        }

        public static List<string> ExtractSkills(string ocrText)
        {
            var skills = LoadSkills();
            if (string.IsNullOrWhiteSpace(ocrText) || skills.Count == 0) return new List<string>();

            var text = Regex.Replace(ocrText.ToLower(), @"[^\w\s]", " ");

            return skills
                .Where(skill => Regex.IsMatch(text, $@"\b{Regex.Escape(skill.ToLower())}\b", RegexOptions.IgnoreCase))
                .Distinct()
                .ToList();
        }

        public static List<string> LoadQualifications()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "qualification.txt");
            return File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        }

        public static List<string> ExtractQualifications(string ocrText)
        {
            var qualifications = LoadQualifications();
            if (string.IsNullOrWhiteSpace(ocrText) || qualifications.Count == 0) return new List<string>();

            return qualifications
                .Where(q => Regex.IsMatch(ocrText, $@"\b{Regex.Escape(q)}\b", RegexOptions.IgnoreCase))
                .ToList();
        }

        public static (string PrimaryEmail, List<string> OtherEmails) ExtractEmails(string text)
        {
            var emailPattern = @"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+";
            var emails = Regex.Matches(text, emailPattern)
                              .Select(m => m.Value.Trim().ToLower())
                              .Distinct()
                              .ToList();

            if (emails.Count == 0) return ("", new List<string>());
            return (emails[0], emails.Skip(1).ToList());
        }

        public static string ExtractPhoneNumber(string text)
        {
            var patterns = new[]
            {
                @"\+91[\s\-\.]?\d{5}[\s\-\.]?\d{5}",
                @"\(?\d{3,4}\)?[\s\-\.]?\d{3,5}[\s\-\.]?\d{3,5}",
                @"\d{10}"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success)
                    return Regex.Replace(match.Value, @"[\s\-\.()]", "");
            }

            return "";
        }

        private static object ExtractNameFromText(string text)
        {
            string fullName = GetNameFromTop(text);
            if (string.IsNullOrWhiteSpace(fullName)) return new { FirstName = "", MiddleName = "", LastName = "", FatherName = "" };

            var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string firstName = "", middleName = "", lastName = "", fatherName = "";

            if (nameParts.Length == 2)
            {
                firstName = Capitalize(nameParts[0]);
                lastName = Capitalize(nameParts[1]);
            }
            else if (nameParts.Length == 3)
            {
                firstName = Capitalize(nameParts[0]);
                middleName = Capitalize(nameParts[1]);
                lastName = Capitalize(nameParts[2]);
            }
            else if (nameParts.Length >= 4)
            {
                firstName = Capitalize(nameParts[0]);
                middleName = string.Join(" ", nameParts.Skip(1).Take(nameParts.Length - 2));
                lastName = Capitalize(nameParts[^1]);
            }

            fatherName = middleName;

            return new
            {
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                FatherName = fatherName
            };
        }

        private static string GetNameFromTop(string text)
        {
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var bestCandidate = "";
            int bestScore = 0;

            for (int i = 0; i < Math.Min(14, lines.Length); i++)
            {
                var line = lines[i].Trim();
                if (line.Length < 5 || line.Any(char.IsDigit) || line.Any(c => !char.IsLetter(c) && !char.IsWhiteSpace(c))) continue;

                var keywords = new[] { "summary", "education", "contact", "experience", "skills" };
                if (keywords.Any(k => line.ToLower().Contains(k))) continue;

                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2 && words.Length <= 4 && words.All(w => char.IsUpper(w[0])))
                {
                    if (words.Length > bestScore)
                    {
                        bestCandidate = line;
                        bestScore = words.Length;
                    }
                }
            }

            return bestCandidate;
        }

        private static string Capitalize(string word)
        {
            return string.IsNullOrWhiteSpace(word) ? "" : char.ToUpper(word[0]) + word.Substring(1).ToLower();
        }
    }
}
