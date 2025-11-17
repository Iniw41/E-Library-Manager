using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

                if ((!string.IsNullOrWhiteSpace(category) && category.Equals("Fiction", StringComparison.OrdinalIgnoreCase))
                    || !string.IsNullOrWhiteSpace(genre))
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

    // ---------------------------
    // BookInfo + BookService
    // ---------------------------
    internal class BookInfo
    {
        public string FilePath { get; init; } = string.Empty;
        public Books Book { get; init; }

        public string Title => Book?.Title ?? Path.GetFileNameWithoutExtension(FilePath) ?? string.Empty;
        public string Author => Book?.Author ?? "unknown";
        public string Category => !string.IsNullOrEmpty(Book?.Category) ? Book.Category : InferCategoryFromPath(FilePath);

        public string GenreOrSub
        {
            get
            {
                if (Book is Fiction f && !string.IsNullOrEmpty(f.Genre)) return f.Genre;
                if (Book is NonFiction nf && !string.IsNullOrEmpty(nf.SubCategory)) return nf.SubCategory;
                return string.Empty;
            }
        }

        public string BuyPriceString => Book != null ? Book.BuyPrice.ToString("0.00", CultureInfo.InvariantCulture) : "0.00";
        public string RentPriceString => Book != null ? Book.RentPrice.ToString("0.##", CultureInfo.InvariantCulture) : "0";

        private static string InferCategoryFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => p.Equals("Fiction", StringComparison.OrdinalIgnoreCase))) return "Fiction";
            if (parts.Any(p => p.Equals("NonFiction", StringComparison.OrdinalIgnoreCase) || p.Equals("Non-Fiction", StringComparison.OrdinalIgnoreCase))) return "NonFiction";
            return string.Empty;
        }
    }

    internal class TransactionRecord
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Action { get; set; } // "Purchase" or "Rent"
        public float Price { get; set; }
        public DateTime TimeUtc { get; set; }
    }

    internal static class BookService
    {
        public static string GetBooksRootPath()
        {
            var baseDir = AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "BooksDB"));
        }

        // path for transactions
        public static string GetPurchasedAndRentedDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "PurchasedAndRentedDB.json"));
        }

        public static List<BookInfo> LoadAllBooks()
        {
            var root = GetBooksRootPath();
            var list = new List<BookInfo>();
            if (!Directory.Exists(root)) return list;

            // include root and known subfolders
            var dirs = new[] { root, Path.Combine(root, "Fiction"), Path.Combine(root, "NonFiction") };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var full = Path.GetFullPath(file);
                    if (!seen.Add(full)) continue;

                    try
                    {
                        var book = Books.LoadFromJsonFile(full);
                        if (book == null) continue;
                        list.Add(new BookInfo { FilePath = full, Book = book });
                    }
                    catch
                    {
                        // skip unreadable files
                    }
                }
            }

            return list.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<BookInfo> FilterByTitlePrefix(IEnumerable<BookInfo> items, string prefix)
        {
            var list = items?.ToList() ?? new List<BookInfo>();
            if (string.IsNullOrWhiteSpace(prefix)) return new List<BookInfo>(list);
            return list.Where(b => !string.IsNullOrEmpty(b.Title) && b.Title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static List<BookInfo> FilterByAuthorPrefix(IEnumerable<BookInfo> items, string prefix)
        {
            var list = items?.ToList() ?? new List<BookInfo>();
            if (string.IsNullOrWhiteSpace(prefix)) return new List<BookInfo>(list);
            return list.Where(b => !string.IsNullOrEmpty(b.Author) && b.Author.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static List<BookInfo> FilterByGenre(IEnumerable<BookInfo> items, string genre, string titlePrefix = null)
        {
            var list = items?.ToList() ?? new List<BookInfo>();
            var q = list.Where(b => !string.IsNullOrEmpty(b.GenreOrSub) && b.GenreOrSub.Equals(genre, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(titlePrefix))
                q = q.Where(b => !string.IsNullOrEmpty(b.Title) && b.Title.StartsWith(titlePrefix, StringComparison.OrdinalIgnoreCase));
            return q.ToList();
        }

        public static List<BookInfo> FilterBySubCategory(IEnumerable<BookInfo> items, string subCategory, string titlePrefix = null)
        {
            // same behavior as FilterByGenre (Genre/SubCategory share the same property)
            return FilterByGenre(items, subCategory, titlePrefix);
        }

        public static void PrintBooksTable(IList<BookInfo> rows, string heading = null)
        {
            if (rows == null) rows = new List<BookInfo>();

            var headers = new[] { "Sel", "Title", "Author", "Genre/SubCategory", "BuyPrice", "RentPrice" };
            var colCount = headers.Length;
            var widths = new int[colCount];
            for (int i = 0; i < colCount; i++) widths[i] = headers[i].Length;

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                widths[0] = Math.Max(widths[0], (i + 1).ToString().Length);
                widths[1] = Math.Max(widths[1], (r.Title ?? "").Length);
                widths[2] = Math.Max(widths[2], (r.Author ?? "").Length);
                widths[3] = Math.Max(widths[3], (r.GenreOrSub ?? "").Length);
                widths[4] = Math.Max(widths[4], (r.BuyPriceString ?? "").Length);
                widths[5] = Math.Max(widths[5], (r.RentPriceString ?? "").Length);
            }

            string MakeSeparator()
            {
                var sb = new StringBuilder();
                sb.Append('+');
                for (int c = 0; c < colCount; c++)
                {
                    sb.Append(new string('-', widths[c] + 2));
                    sb.Append('+');
                }
                return sb.ToString();
            }

            var sep = MakeSeparator();

            if (!string.IsNullOrEmpty(heading))
            {
                Console.Clear();
                StyleConsPrint.WriteCentered(heading);
                Console.WriteLine();
            }

            Console.WriteLine(sep);

            // header
            {
                var sb = new StringBuilder();
                sb.Append("|");
                for (int c = 0; c < colCount; c++)
                {
                    sb.Append(" ");
                    sb.Append(headers[c].PadRight(widths[c]));
                    sb.Append(" |");
                }
                Console.WriteLine(sb.ToString());
            }

            Console.WriteLine(sep);

            // rows
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var sb = new StringBuilder();
                sb.Append("| ");
                sb.Append((i + 1).ToString().PadLeft(widths[0]));
                sb.Append(" | ");
                sb.Append((r.Title ?? "").PadRight(widths[1]));
                sb.Append(" | ");
                sb.Append((r.Author ?? "").PadRight(widths[2]));
                sb.Append(" | ");
                sb.Append((r.GenreOrSub ?? "").PadRight(widths[3]));
                sb.Append(" | ");
                sb.Append((r.BuyPriceString ?? "").PadLeft(widths[4]));
                sb.Append(" | ");
                sb.Append((r.RentPriceString ?? "").PadLeft(widths[5]));
                sb.Append(" |");
                Console.WriteLine(sb.ToString());
            }

            Console.WriteLine(sep);
            Console.WriteLine();
            Console.WriteLine($"Total books: {rows.Count}");
        }

        // ---------------------------
        // Interactive helpers (UI)
        // ---------------------------

        public static void InteractiveViewAllBooks()
        {
            Console.Clear();
            StyleConsPrint.WriteCentered("View All Books - Prefix Search");
            Console.Write("Enter title prefix (press Enter to list all): ");
            var prefix = (Console.ReadLine() ?? "").Trim();

            var all = LoadAllBooks();
            var filtered = FilterByTitlePrefix(all, prefix);

            Console.Clear();
            StyleConsPrint.WriteCentered($"Books matching '{prefix}'");
            Console.WriteLine();
            PrintBooksTable(filtered);
            Console.WriteLine();
            Console.WriteLine("Press any key to return...");
            Console.ReadKey(true);
        }

        public static BookInfo InteractiveSelectBookByTitle()
        {
            Console.Clear();
            StyleConsPrint.WriteCentered("Select Book By Title (prefix search)");
            Console.Write("Enter title prefix (press Enter to list all): ");
            var prefix = (Console.ReadLine() ?? "").Trim();

            var all = LoadAllBooks();
            var filtered = FilterByTitlePrefix(all, prefix);

            if (filtered.Count == 0)
            {
                StyleConsPrint.WriteCentered("No books found for that prefix.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered($"Books matching '{prefix}'");
            Console.WriteLine();
            PrintBooksTable(filtered);

            Console.Write("Select number (0 = cancel): ");
            var input = Console.ReadLine() ?? "";
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > filtered.Count)
                return null;
            if (sel == 0) return null;
            return filtered[sel - 1];
        }

        public static BookInfo InteractiveSelectBookByAuthor()
        {
            Console.Clear();
            StyleConsPrint.WriteCentered("Select Book By Author (prefix)");
            Console.Write("Enter author prefix (press Enter to list all): ");
            var prefix = (Console.ReadLine() ?? "").Trim();

            var all = LoadAllBooks();
            var filtered = FilterByAuthorPrefix(all, prefix);

            if (filtered.Count == 0)
            {
                StyleConsPrint.WriteCentered("No books found for that author prefix.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered($"Books by authors matching '{prefix}'");
            Console.WriteLine();
            PrintBooksTable(filtered);

            Console.Write("Select number (0 = cancel): ");
            var input = Console.ReadLine() ?? "";
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > filtered.Count)
                return null;
            if (sel == 0) return null;
            return filtered[sel - 1];
        }

        public static BookInfo InteractiveSelectBookByGenre()
        {
            Console.Clear();
            BooksDisplayMenu.SelectBookGenreMenu();
            Console.WriteLine();
            Console.Write("Choose genre (press corresponding number): ");
            var key = Console.ReadKey(true);

            string chosen = key.Key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => "Fantasy",
                ConsoleKey.D2 or ConsoleKey.NumPad2 => "ScienceFiction",
                ConsoleKey.D3 or ConsoleKey.NumPad3 => "Mystery",
                ConsoleKey.D4 or ConsoleKey.NumPad4 => "Romance",
                ConsoleKey.D5 or ConsoleKey.NumPad5 => "Horror",
                ConsoleKey.D6 or ConsoleKey.NumPad6 => "Historical",
                ConsoleKey.D7 or ConsoleKey.NumPad7 => "Dystopian",
                ConsoleKey.D8 or ConsoleKey.NumPad8 => "Adventure",
                _ => null
            };

            if (string.IsNullOrEmpty(chosen))
            {
                Console.WriteLine("Invalid selection or cancelled. Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.WriteLine();
            Console.Write("Enter title prefix to further filter (press Enter to skip): ");
            var prefix = (Console.ReadLine() ?? "").Trim();

            var all = LoadAllBooks();
            var filtered = FilterByGenre(all, chosen, prefix);

            if (filtered.Count == 0)
            {
                StyleConsPrint.WriteCentered("No books found.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered($"Genre: {chosen}  |  Title prefix: '{prefix}'");
            Console.WriteLine();
            PrintBooksTable(filtered);

            Console.Write("Select number (0 = cancel): ");
            var input = Console.ReadLine() ?? "";
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > filtered.Count)
                return null;
            if (sel == 0) return null;
            return filtered[sel - 1];
        }

        public static BookInfo InteractiveSelectBookBySubCategory()
        {
            Console.Clear();
            BooksDisplayMenu.SelectBookSubCategoryMenu();
            Console.WriteLine();
            Console.Write("Choose subcategory (press corresponding number): ");
            var key = Console.ReadKey(true);

            string chosen = key.Key switch
            {
                ConsoleKey.D1 or ConsoleKey.NumPad1 => "History",
                ConsoleKey.D2 or ConsoleKey.NumPad2 => "Politics",
                ConsoleKey.D3 or ConsoleKey.NumPad3 => "Philosophy",
                ConsoleKey.D4 or ConsoleKey.NumPad4 => "Engineering",
                ConsoleKey.D5 or ConsoleKey.NumPad5 => "Medical",
                ConsoleKey.D6 or ConsoleKey.NumPad6 => "Biography",
                ConsoleKey.D7 or ConsoleKey.NumPad7 => "Science",
                _ => null
            };

            if (string.IsNullOrEmpty(chosen))
            {
                Console.WriteLine("Invalid selection or cancelled. Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.WriteLine();
            Console.Write("Enter title prefix to further filter (press Enter to skip): ");
            var prefix = (Console.ReadLine() ?? "").Trim();

            var all = LoadAllBooks();
            var filtered = FilterBySubCategory(all, chosen, prefix);

            if (filtered.Count == 0)
            {
                StyleConsPrint.WriteCentered("No books found.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered($"SubCategory: {chosen}  |  Title prefix: '{prefix}'");
            Console.WriteLine();
            PrintBooksTable(filtered);

            Console.Write("Select number (0 = cancel): ");
            var input = Console.ReadLine() ?? "";
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > filtered.Count)
                return null;
            if (sel == 0) return null;
            return filtered[sel - 1];
        }

        // ---------------
        // Transactions persistence
        // ---------------
        public static List<TransactionRecord> LoadTransactions()
        {
            var path = GetPurchasedAndRentedDbPath();
            if (!File.Exists(path)) return new List<TransactionRecord>();

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return new List<TransactionRecord>();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var list = JsonSerializer.Deserialize<List<TransactionRecord>>(json, opts);
                return list ?? new List<TransactionRecord>();
            }
            catch
            {
                return new List<TransactionRecord>();
            }
        }

        public static void SaveTransactions(List<TransactionRecord> records)
        {
            var path = GetPurchasedAndRentedDbPath();
            var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var opts = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(records ?? new List<TransactionRecord>(), opts);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        public static void AddTransactionRecord(TransactionRecord rec)
        {
            var all = LoadTransactions();
            all.Add(rec);
            SaveTransactions(all);
        }
    }
}
