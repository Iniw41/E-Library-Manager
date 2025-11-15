using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E_Library_Manager.LLM_Support
{
    internal static class LLMSupport
    {
        private static readonly HttpClient _http = new HttpClient();

        // Call this method to convert all .txt files in the project UnsortedBooks folder
        // into structured JSON files written into Database/BooksDB/Fiction or /NonFiction.
        // After successful conversion the original .txt is moved into the same target folder.
        public static async Task ConvertUnsortedBooksToJsonAsync(CancellationToken cancellationToken = default)
        {
            var baseDir = AppContext.BaseDirectory;
            var inputFolder = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "UnsortedBooks"));
            var booksRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "BooksDB"));
            var fictionRoot = Path.Combine(booksRoot, "Fiction");
            var nonFictionRoot = Path.Combine(booksRoot, "NonFiction");

            Directory.CreateDirectory(inputFolder);
            Directory.CreateDirectory(fictionRoot);
            Directory.CreateDirectory(nonFictionRoot);

            var files = Directory.GetFiles(inputFolder, "*.txt", SearchOption.TopDirectoryOnly);
            if (files.Length == 0)
            {
                Console.WriteLine("No .txt files found in UnsortedBooks.");
                return;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string rawContent;
                try
                {
                    rawContent = File.ReadAllText(file, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to read '{file}': {ex.Message}");
                    continue;
                }

                BookInfo bookInfo = null;
                try
                {
                    bookInfo = await AskOllamaForBookInfoAsync(rawContent, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Console.WriteLine($"LLM request failed for '{Path.GetFileName(file)}': {ex.Message}");
                }

                if (bookInfo == null)
                {
                    Console.WriteLine($"Skipping '{Path.GetFileName(file)}' — no structured info returned.");
                    continue;
                }

                // choose target directory (use subfolder if Genre/SubCategory provided)
                string destDir;
                if (string.Equals(bookInfo.Category, "Fiction", StringComparison.OrdinalIgnoreCase))
                {
                    var sub = string.IsNullOrWhiteSpace(bookInfo.Genre) ? "Unsorted" : SanitizeFolderName(bookInfo.Genre);
                    destDir = Path.Combine(fictionRoot, sub);
                }
                else // NonFiction (treat any non-"Fiction" as NonFiction)
                {
                    var sub = string.IsNullOrWhiteSpace(bookInfo.SubCategory) ? "Unsorted" : SanitizeFolderName(bookInfo.SubCategory);
                    destDir = Path.Combine(nonFictionRoot, sub);
                }

                Directory.CreateDirectory(destDir);

                // prepare filenames
                var titleForFile = string.IsNullOrWhiteSpace(bookInfo.Title)
                    ? Path.GetFileNameWithoutExtension(file)
                    : SanitizeFileName(bookInfo.Title);

                var jsonPath = GetUniqueDestinationPath(destDir, titleForFile + ".json");
                var textDest = GetUniqueDestinationPath(destDir, Path.GetFileName(file));

                try
                {
                    var jsonOutput = JsonConvert.SerializeObject(bookInfo, Formatting.Indented);
                    await File.WriteAllTextAsync(jsonPath, jsonOutput, Encoding.UTF8, cancellationToken);

                    // move original text file to target folder (so UnsortedBooks will eventually be empty)
                    File.Move(file, textDest);

                    Console.WriteLine($"✔ Converted: {Path.GetFileName(jsonPath)}  (moved text -> {Path.GetFileName(textDest)})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write output for '{Path.GetFileName(file)}': {ex.Message}");
                }
            }
        }

        // Ask the local Ollama endpoint to analyze the text and return structured BookInfo.
        // The model is expected to return a JSON object (only JSON). The method will try to extract JSON
        // from the response body and deserialize it into BookInfo.
        private static async Task<BookInfo> AskOllamaForBookInfoAsync(string text, CancellationToken ct)
        {
            var url = "http://localhost:11434/api/generate";
            var prompt = new StringBuilder();
            prompt.AppendLine("Analyze the following ebook text and return ONLY valid JSON (no explanation).");
            prompt.AppendLine("If Fiction return JSON with fields: Title, Author, Category=\"Fiction\", Genre, BuyPrice, RentPrice, Content (array or string).");
            prompt.AppendLine("If NonFiction return JSON with fields: Title, Author, Category=\"NonFiction\", SubCategory, BuyPrice, RentPrice, Content (array or string).");
            prompt.AppendLine();
            prompt.AppendLine("Text:");
            prompt.AppendLine(text);

            var request = new
            {
                model = "SrEgg",
                prompt = prompt.ToString(),
                max_tokens = 1500,
                temperature = 0.2,
                top_p = 1.0,
                frequency_penalty = 0,
                presence_penalty = 0,
                stream = false
            };

            var reqJson = JsonConvert.SerializeObject(request);
            using var content = new StringContent(reqJson, Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);

            // extract candidate text (the model output) from the response body
            var modelText = ExtractModelTextFromApiResponse(body);

            // if the model wrapped JSON inside markdown/code fences, strip them
            modelText = StripCodeFence(modelText).Trim();

            // Attempt to find the first JSON object inside the model text and deserialize it
            var jsonCandidate = ExtractFirstJsonObject(modelText) ?? modelText;

            // try direct deserialization
            try
            {
                var info = JsonConvert.DeserializeObject<BookInfo>(jsonCandidate);
                if (info != null)
                    return NormalizeBookInfo(info);
            }
            catch
            {
                // fall through to try a more tolerant parse (search for JSON substring)
            }

            // second attempt: extract any JSON substring and try again
            var altJson = ExtractFirstJsonObject(modelText);
            if (!string.IsNullOrWhiteSpace(altJson))
            {
                try
                {
                    var info2 = JsonConvert.DeserializeObject<BookInfo>(altJson);
                    if (info2 != null)
                        return NormalizeBookInfo(info2);
                }
                catch { }
            }

            // If parsing failed, return null so caller can skip the file
            return null;
        }

        // Helper: try to extract a plausible model output string from various API response shapes.
        private static string ExtractModelTextFromApiResponse(string apiResponse)
        {
            if (string.IsNullOrWhiteSpace(apiResponse))
                return string.Empty;

            // try parse JSON
            try
            {
                var token = JToken.Parse(apiResponse);

                // common keys to search for
                var searchKeys = new[] { "response", "text", "content", "output", "result", "answer", "generated_text", "generation", "generations", "choices" };

                // search for direct string properties with these names
                foreach (var key in searchKeys)
                {
                    var found = token.SelectToken($"..{key}", errorWhenNoMatch: false);
                    if (found != null)
                    {
                        // if found is a string, return it; otherwise attempt to find first string inside
                        if (found.Type == JTokenType.String)
                            return found.ToString();
                        var first = FindFirstStringJToken(found);
                        if (!string.IsNullOrWhiteSpace(first))
                            return first;
                    }
                }

                // fallback: find the first string anywhere in the JSON
                var fallback = FindFirstStringJToken(token);
                if (!string.IsNullOrWhiteSpace(fallback))
                    return fallback;
            }
            catch
            {
                // not JSON, fall back to raw body
            }

            // raw response might already be the JSON text/contents
            return apiResponse;
        }

        private static string FindFirstStringJToken(JToken token)
        {
            if (token == null) return string.Empty;
            if (token.Type == JTokenType.String) return token.ToString();

            if (token.Type == JTokenType.Object)
            {
                foreach (var prop in token.Children<JProperty>())
                {
                    var s = FindFirstStringJToken(prop.Value);
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in token.Children())
                {
                    var s = FindFirstStringJToken(item);
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }

            return string.Empty;
        }

        // Try to extract the first balanced JSON object from a larger string
        private static string ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            int start = text.IndexOf('{');
            if (start < 0) return null;

            int depth = 0;
            for (int i = start; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(start, i - start + 1);
                    }
                }
            }
            return null;
        }

        private static string StripCodeFence(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            // remove triple-backtick blocks if present
            if (s.Contains("```"))
            {
                var first = s.IndexOf("```", StringComparison.Ordinal);
                var last = s.LastIndexOf("```", StringComparison.Ordinal);
                if (first != last)
                {
                    return s.Substring(first + 3, last - first - 3).Trim();
                }
                // if only one fence, remove it
                return s.Replace("```", "").Trim();
            }
            return s;
        }

        private static BookInfo NormalizeBookInfo(BookInfo info)
        {
            if (info == null) return null;
            // normalize category strings
            if (!string.IsNullOrWhiteSpace(info.Category))
            {
                info.Category = info.Category.Trim();
                if (!string.Equals(info.Category, "Fiction", StringComparison.OrdinalIgnoreCase))
                    info.Category = "NonFiction";
            }
            else
            {
                // fallback: try infer by presence of Genre vs SubCategory
                info.Category = string.IsNullOrWhiteSpace(info.Genre) ? "NonFiction" : "Fiction";
            }

            if (info.BuyPrice == null) info.BuyPrice = "0";
            if (info.RentPrice == null) info.RentPrice = "0";
            // ensure content list exists
            if (info.Content == null)
                info.Content = new List<string>();

            return info;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(clean)) clean = "book";
            return clean;
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).Distinct().ToArray();
            var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(clean)) clean = "Unsorted";
            return clean;
        }

        private static string GetUniqueDestinationPath(string directory, string fileName)
        {
            var dest = Path.Combine(directory, fileName);
            if (!File.Exists(dest)) return dest;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            for (int i = 1; ; i++)
            {
                var candidate = Path.Combine(directory, $"{name} ({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
        }

        // Simple model for the expected JSON structure returned by the LLM
        private class BookInfo
        {
            public string Title { get; set; }
            public string Author { get; set; }
            public string Category { get; set; } // "Fiction" or "NonFiction"
            public string Genre { get; set; } // for Fiction
            public string SubCategory { get; set; } // for NonFiction
            public string BuyPrice { get; set; }
            public string RentPrice { get; set; }
            public List<string> Content { get; set; } = new List<string>();
        }
    }
}
