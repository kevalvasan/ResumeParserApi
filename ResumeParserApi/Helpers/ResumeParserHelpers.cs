using ImageMagick;
using Tesseract;
using System.Text;
using System.Text.RegularExpressions;

namespace ResumeParserApi.Helpers
{
    public static class ResumeParserHelpers
    {
        public static async Task<(string ocrText, dynamic parsedName)> ProcessPdfAndGenerateAssets(string pdfPath, string tempFolderPath)
        {
            // 1. Convert PDF pages to images
            var imagePaths = ConvertPdfToPngs(pdfPath, tempFolderPath);

            // 2. Run OCR on each image
            var allText = new StringBuilder();
            foreach (var imagePath in imagePaths)
            {
                string text = RunOcrOnImage(imagePath);
                allText.AppendLine(text);
            }

            // 3. Save to .txt file
            var txtPath = Path.ChangeExtension(pdfPath, ".txt");
            var ocrText = allText.ToString();
            await File.WriteAllTextAsync(txtPath, ocrText);

            // 4. Extract name info from OCR text
            var nameInfo = ExtractNameFromText(ocrText);

            // 5. Return both OCR text and structured name object
            return (ocrText, nameInfo);
        }


        private static List<string> ConvertPdfToPngs(string pdfPath, string outputFolder)
        {
            var imagePaths = new List<string>();
            using (var images = new MagickImageCollection())
            {
                var settings = new MagickReadSettings
                {
                    Density = new Density(300)
                };

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
            // Look for patterns like "City, State, Country" with support for multiple countries.
            var regex = new Regex(@"\b([A-Za-z]+(?: [A-Za-z]+)*),\s*([A-Za-z]+(?: [A-Za-z]+)*),\s*(\b[A-Za-z]+\b)\b");
            var match = regex.Match(text);

            // If a match is found, return city and state
            if (match.Success)
            {
                var city = match.Groups[1].Value.Trim();
                var state = match.Groups[2].Value.Trim();
                var country = match.Groups[3].Value.Trim();

                // Optionally, you can log or process the country as well
                // For now, we are only focusing on city and state
                return (city, state);
            }

            // Fallback - Search for "City, State" without the country (e.g., local addresses)
            regex = new Regex(@"\b([A-Za-z]+(?: [A-Za-z]+)*),\s*([A-Za-z]+(?: [A-Za-z]+)*)\b");
            match = regex.Match(text);

            // If a match is found, return city and state
            if (match.Success)
            {
                var city = match.Groups[1].Value.Trim();
                var state = match.Groups[2].Value.Trim();
                return (city, state);
            }

            // Return empty strings if no match is found
            return ("", "");
        }

        public static List<string> LoadSkills()
        {
            var skillsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "skills.txt");

            if (!File.Exists(skillsFilePath))
            {
                return new List<string>();
            }

            return File.ReadAllLines(skillsFilePath)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
        }

        public static List<string> ExtractSkills(string ocrText)
        {
            var skills = LoadSkills();
            var foundSkills = new List<string>();

            if (string.IsNullOrWhiteSpace(ocrText) || skills.Count == 0)
                return foundSkills;

            // Normalize text: lowercase and remove punctuation
            var normalizedText = Regex.Replace(ocrText.ToLower(), @"[^\w\s]", " ");

            foreach (var skill in skills)
            {
                var normalizedSkill = skill.ToLower().Trim();

                // Use simple word contains (loose match) instead of strict \b boundaries
                if (Regex.IsMatch(normalizedText, $@"\b{Regex.Escape(normalizedSkill)}\b", RegexOptions.IgnoreCase))
                {
                    foundSkills.Add(skill);
                }
            }

            return foundSkills.Distinct().ToList();
        }



        public static List<string> LoadQualifications()
        {
            var qualificationsFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "qualification.txt");

            if (!File.Exists(qualificationsFilePath))
            {
                return new List<string>(); // Return an empty list if the qualifications file does not exist
            }

            // Convert the string array to a List<string> using ToList()
            return File.ReadAllLines(qualificationsFilePath).ToList();
        }


        // Method to extract qualifications from the OCR text
        public static List<string> ExtractQualifications(string ocrText)
        {
            var qualifications = LoadQualifications(); // Load qualifications from file
            var foundQualifications = new List<string>();

            if (string.IsNullOrWhiteSpace(ocrText) || qualifications.Count == 0)
            {
                return foundQualifications; // Return an empty list if OCR text or qualifications list is empty
            }

            // Check if any qualification exists in the OCR text
            foreach (var qualification in qualifications)
            {
                // Use regular expression to check for each qualification in the OCR text
                if (Regex.IsMatch(ocrText, $@"\b{Regex.Escape(qualification)}\b", RegexOptions.IgnoreCase))
                {
                    foundQualifications.Add(qualification); // Add found qualification to the list
                }
            }

            return foundQualifications; // Return the list of found qualifications
        }

        public static (string PrimaryEmail, List<string> OtherEmails) ExtractEmails(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ("", new List<string>());

            var emailPattern = @"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+";
            var matches = Regex.Matches(text, emailPattern);

            var emailList = matches
                .Select(m => m.Value.Trim().ToLower())
                .Distinct()
                .ToList();

            if (emailList.Count == 0)
                return ("", new List<string>());

            var primaryEmail = emailList[0];
            var otherEmails = emailList.Skip(1).ToList();

            return (primaryEmail, otherEmails);
        }

        public static string ExtractPhoneNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // Define the phone number patterns (matching Indian numbers, etc.)
            var patterns = new string[]
            {
        @"\+91[\s\-\.]?\d{5}[\s\-\.]?\d{5}",         // +91 98765 43210 or +91-98765-43210
        @"\(?\d{3,4}\)?[\s\-\.]?\d{3,5}[\s\-\.]?\d{3,5}", // (022) 23456789 or 987-654-3210
        @"\d{10}"                                      // 10-digit flat number
            };

            // Loop through the patterns and match them
            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success)
                {
                    // Clean it up (remove spaces, dashes, dots, and parentheses)
                    return Regex.Replace(match.Value, @"[\s\-\.()]", "");
                }
            }

            return "";
        }


        private static object ExtractNameFromText(string text)
        {
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Trim())
                            .Where(line => line.Length > 1 && line.Length < 60)
                            .ToList();

            string firstName = "", middleName = "", lastName = "", fatherName = "";

            var nameKeywords = new[] { "summary", "education", "contact", "experience", "skills" };

            // Extract full name from the first 15 lines based on certain heuristics
            string fullName = GetNameFromTop(text);

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var nameParts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

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
                    lastName = Capitalize(nameParts[nameParts.Length - 1]);
                }

                // Assign middle name as father's name
                fatherName = middleName;
            }

            return new
            {
                FirstName = firstName,
                MiddleName = middleName,
                LastName = lastName,
                FatherName = fatherName
            };
        }

        // Helper method to get name from the first lines
        private static string GetNameFromTop(string text)
        {
            var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // We'll check the first 15 lines max
            var bestCandidate = "";
            var bestScore = 0;

            for (int i = 0; i < Math.Min(14, lines.Length); i++)
            {
                var line = lines[i].Trim();

                // Skip short or invalid lines
                if (line.Length < 5) continue;

                // Skip if the line contains digits or invalid characters
                if (line.Any(c => Char.IsDigit(c) || !Char.IsLetter(c) && !Char.IsWhiteSpace(c))) continue;

                // Skip if it's a known section title
                var nameKeywords = new[] { "summary", "education", "contact", "experience", "skills" };
                if (nameKeywords.Any(k => line.ToLower().Contains(k))) continue;

                // Heuristic: more words → better (up to 3-4)
                var wordCount = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount >= 2 && wordCount <= 4)
                {
                    var capitalWords = line.Split().Count(w => char.IsUpper(w[0]));
                    if (capitalWords == wordCount)
                    {
                        if (wordCount > bestScore)
                        {
                            bestCandidate = line;
                            bestScore = wordCount;
                        }
                    }
                }
            }

            return bestCandidate;
        }

        // Helper method to capitalize words
        private static string Capitalize(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return "";
            return char.ToUpper(word[0]) + word.Substring(1).ToLower();
        }



    }

}
