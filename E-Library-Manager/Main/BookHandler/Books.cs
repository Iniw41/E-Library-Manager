using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using E_Library_Manager.Styles;

namespace E_Library_Manager.Main.BookHandler
{
    internal class Books
    {
        // the book data
        public string Title { get; set; }
        public string Author { get; set; }
        public string Category { get; set; } // "Fiction" or "NonFiction"
        public float BuyPrice { get; set; }
        public float RentPrice { get; set; }

        // full text or sections of the book; optional
        public List<string> Content { get; set; } = new();

        // parameterless ctor for serializers and factories
        public Books() { }

        public Books(string title, string author, string category, float buyprice, float rentprice)
        {
            Title = title;
            Author = author;
            Category = category;
            BuyPrice = buyprice;
            RentPrice = rentprice;
        }

        // Serialize this book (or derived type) to JSON
        public virtual string ToJson()
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(this, this.GetType(), opts);
        }

        // Save JSON to a file (overwrites)
        public virtual void SaveToJsonFile(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, ToJson());
        }

        // Load from JSON file and return Books/Fiction/NonFiction instance
        public static Books LoadFromJsonFile(string path)
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return FromJson(json);
        }

        // Parse JSON produced by the LLM pipeline (flexible: Content can be string or array)
        public static Books FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string category = ReadString(root, "Category", "category")?.Trim();
                string title = ReadString(root, "Title", "title") ?? string.Empty;
                string author = ReadString(root, "Author", "author") ?? "unknown";
                string buyStr = ReadString(root, "BuyPrice", "buyprice", "BuyPrice");
                string rentStr = ReadString(root, "RentPrice", "rentprice", "RentPrice");

                float buy = ParseFloatInvariant(buyStr);
                float rent = ParseFloatInvariant(rentStr);

                // read content: array or single string
                var contentList = new List<string>();
                if (root.TryGetProperty("Content", out var contentProp))
                {
                    if (contentProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var it in contentProp.EnumerateArray())
                        {
                            if (it.ValueKind == JsonValueKind.String)
                                contentList.Add(it.GetString());
                            else
                                contentList.Add(it.GetRawText());
                        }
                    }
                    else if (contentProp.ValueKind == JsonValueKind.String)
                    {
                        contentList.Add(contentProp.GetString());
                    }
                    else
                    {
                        contentList.Add(contentProp.GetRawText());
                    }
                }

                // decide derived type by Category or presence of Genre/SubCategory
                string genre = ReadString(root, "Genre", "genre");
                string subCategory = ReadString(root, "SubCategory", "subCategory", "Subcategory", "subcategory");

                if (!string.IsNullOrWhiteSpace(category) && category.Equals("Fiction", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(genre))
                {
                    var f = new Fiction(title, author, "Fiction", buy, rent, genre ?? string.Empty)
                    {
                        Content = contentList
                    };
                    return f;
                }
                else
                {
                    var nf = new NonFiction(title, author, "NonFiction", buy, rent, subCategory ?? string.Empty)
                    {
                        Content = contentList
                    };
                    return nf;
                }
            }
            catch
            {
                // parsing failed
                return null;
            }
        }

        public override string ToString()
        {
            return $"{Title} — {Author} [{Category}]";
        }

        // Helpers
        private static string ReadString(JsonElement el, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (el.TryGetProperty(k, out var p))
                {
                    if (p.ValueKind == JsonValueKind.String) return p.GetString();
                    if (p.ValueKind == JsonValueKind.Number) return p.GetRawText();
                    // fallback to raw
                    return p.GetRawText();
                }
            }
            return null;
        }

        private static float ParseFloatInvariant(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0f;
            if (float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                return v;
            // try to strip currency characters
            var numeric = new string(s.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == ',').ToArray()).Replace(",", ".");
            if (float.TryParse(numeric, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
                return v;
            return 0f;
        }
    }

    internal class NonFiction : Books
    {
        public string SubCategory { get; set; }

        public NonFiction() : base() { }

        public NonFiction(string title, string author, string category, float buyprice, float rentprice, string subCategory)
            : base(title, author, category, buyprice, rentprice)
        {
            SubCategory = subCategory;
        }

        public override string ToString()
        {
            return $"{Title} — {Author} [NonFiction/{SubCategory}]";
        }
    }

    internal class Fiction : Books
    {
        public string Genre { get; set; }

        public Fiction() : base() { }

        public Fiction(string title, string author, string category, float buyprice, float rentprice, string genre)
            : base(title, author, category, buyprice, rentprice)
        {
            Genre = genre;
        }

        public override string ToString()
        {
            return $"{Title} — {Author} [Fiction/{Genre}]";
        }
    }

    public enum BookCategory
    {
        Fiction,
        NonFiction
    }
    public enum FictionGenre
    {
        Fantasy,
        ScienceFiction,
        Mystery,
        Romance,
        Horror,
        Historical,
        Dystopian,
        Adventure,
    }
    public enum NonFictionSubCategory
    {
        Philosophy,
        Politics,
        History,
        Math,
        Science,
    }
}
