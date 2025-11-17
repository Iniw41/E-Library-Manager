using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using E_Library_Manager.LLM_Support;
using E_Library_Manager.Styles;
using E_Library_Manager.Main.BookHandler;

// Backend Logic

namespace E_Library_Manager.Main.AccountsHandler
{
    interface Ilogin
    {
        bool Login(string username, string password);
    }
    interface IAccountInfo
    {
        void DisplayInfo();
    }

    internal class AllUsers
    {
        // this class holds the basic info like id, username, password, age, fullname
        public string ID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Fullname { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }

        public AllUsers(string id, string username, string password, string fullname, int age, string email)
        {
            ID = id;
            Username = username;
            Password = password;
            Fullname = fullname;
            Age = age;
            Email = email;
        }
    }

    internal class Admin : AllUsers, Ilogin, IAccountInfo
    {
        public Admin(string id, string username, string password, string fullname, int age, string email)
            : base(id, username, password, fullname, age, email)
        { }

        public bool Login(string username, string password)
        {
            return Username == username && Password == password;
        }

        static string GetUserDBPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "UsersDB.txt"));
            return candidate;
        }

        static string GetBanDBPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "BansDB.txt"));
            return candidate;
        }

        // Books and borrowed records paths
        static string GetBooksPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "BooksDB"));
            return candidate;
        }
        
        static string GetPurchesedAndRentedDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "PurchasedAndRentedDB.json"));
            return candidate;
        }

        // Helper to update user's credit in UsersDB.txt
        internal static void UpdateUserCredit(string userId, float newCredit)
        {
            try
            {
                var path = GetUserDBPath();
                if (!File.Exists(path)) return;

                var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
                if (lines.Count == 0) return;

                // Ensure header has Credit column
                var header = lines[0];
                if (header.IndexOf("ID", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!header.Contains("Credit", StringComparison.OrdinalIgnoreCase))
                    {
                        header = header.TrimEnd() + ",Credit";
                        lines[0] = header;
                    }
                }

                // update matching user line
                for (int i = 1; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var tokens = line.Split(',').Select(t => t.Trim()).ToList();
                    if (tokens.Count == 0) continue;
                    if (tokens[0].Trim('"') == userId)
                    {
                        // make sure there are at least 7 tokens
                        while (tokens.Count < 7) tokens.Add(string.Empty);
                        tokens[6] = newCredit.ToString("0.##", CultureInfo.InvariantCulture);
                        lines[i] = string.Join(",", tokens);
                        File.WriteAllLines(path, lines, Encoding.UTF8);
                        return;
                    }
                }

                // If not found, append new user entry line with minimal fields (not ideal but safe)
                var newLine = $"{userId},Unknown, , ,0,,{newCredit.ToString("0.##", CultureInfo.InvariantCulture)}";
                lines.Add(newLine);
                File.WriteAllLines(path, lines, Encoding.UTF8);
            }
            catch
            {
                // ignore errors here to avoid breaking purchase flow
            }
        }

        //-------------------------
        // Sorting Books (new)
        //-------------------------
        public void SortBooksManually()
        {
            while (true)
            {
                Console.Clear();
                BooksDisplayMenu.SelectBookCategoryMenu();
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        // Fiction
                        // Show list then allow selection & genre assignment
                        ViewUnsortedBooks();
                        AddGenreToBook();
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        // Non-Fiction
                        ViewUnsortedBooks();
                        AddSubCategoryToBook();
                        break;

                    case ConsoleKey.Escape:
                        return;
                    
                    default:
                        break;
                }

            }
        }

        public void AddSubCategoryToBook()
        {
            try
            {
                // Let user select a file
                var filePath = SelectUnsortedBook();
                if (string.IsNullOrEmpty(filePath)) return;

                // show subcategory choices (reuse display helper if desired)
                Console.Clear();
                BooksDisplayMenu.SelectBookSubCategoryMenu();
                Console.WriteLine();
                Console.Write("Choose SubCategory (press corresponding number): ");
                var key = Console.ReadKey(true);

                string chosen = null;
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        chosen = "History";
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        chosen = "Politics";
                        break;
                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        chosen = "Philosophy";
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        chosen = "Engineering";
                        break;
                    case ConsoleKey.D5:
                    case ConsoleKey.NumPad5:
                        chosen = "Medical";
                        break;
                    case ConsoleKey.D6:
                    case ConsoleKey.NumPad6:
                        chosen = "Biography";
                        break;
                    case ConsoleKey.D7:
                    case ConsoleKey.NumPad7:
                        chosen = "Science";
                        break;
                    case ConsoleKey.Escape:
                        return;
                    default:
                        Console.WriteLine("Invalid selection. Operation cancelled.");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey(true);
                        return;
                }

                // Read original JSON and preserve content and other fields
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string title = root.TryGetProperty("Title", out var pTitle) ? pTitle.GetString() ?? Path.GetFileNameWithoutExtension(filePath) : Path.GetFileNameWithoutExtension(filePath);
                string author = root.TryGetProperty("Author", out var pAuthor) ? pAuthor.GetString() ?? "unknown" : "unknown";
                string buy = root.TryGetProperty("BuyPrice", out var pBuy) ? pBuy.GetString() ?? "0.00" : "0.00";
                string rent = root.TryGetProperty("RentPrice", out var pRent) ? pRent.GetString() ?? "0" : "0";

                string[] contentLines = Array.Empty<string>();
                if (root.TryGetProperty("Content", out var pContent) && pContent.ValueKind == JsonValueKind.Array)
                {
                    contentLines = pContent.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                }
                else if (root.TryGetProperty("Content", out pContent) && pContent.ValueKind == JsonValueKind.String)
                {
                    var text = pContent.GetString() ?? string.Empty;
                    var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
                    contentLines = normalized.Split('\n');
                }

                // Build new JSON with SubCategory and Category = NonFiction
                var outDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Title"] = title,
                    ["Author"] = author,
                    ["Category"] = "NonFiction",
                    ["SubCategory"] = chosen,
                    ["BuyPrice"] = buy,
                    ["RentPrice"] = rent,
                    ["Content"] = contentLines
                };

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var outJson = JsonSerializer.Serialize(outDict, opts);

                // Destination folder: Database/BooksDB/NonFiction
                var booksRoot = GetBooksPath();
                var destDir = Path.Combine(booksRoot, "NonFiction");
                Directory.CreateDirectory(destDir);

                var destPath = GetUniqueDestinationFile(destDir, Path.GetFileName(filePath));
                File.WriteAllText(destPath, outJson, Encoding.UTF8);

                // remove original file if different
                if (!string.Equals(Path.GetFullPath(destPath), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(filePath);
                }

                StyleConsPrint.WriteCentered($"Assigned SubCategory '{chosen}' and moved to NonFiction.");
                Console.WriteLine($"File: {Path.GetFileName(destPath)}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddSubCategoryToBook: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        public void AddGenreToBook()
        {
            try
            {
                // Let user select a file
                var filePath = SelectUnsortedBook();
                if (string.IsNullOrEmpty(filePath)) return;

                // show genre choices
                Console.Clear();
                BooksDisplayMenu.SelectBookGenreMenu();
                Console.WriteLine();
                Console.Write("Choose Genre (press corresponding number): ");
                var key = Console.ReadKey(true);

                string chosen = null;
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        chosen = "Fantasy";
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        chosen = "ScienceFiction";
                        break;
                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        chosen = "Mystery";
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        chosen = "Romance";
                        break;
                    case ConsoleKey.D5:
                    case ConsoleKey.NumPad5:
                        chosen = "Horror";
                        break;
                    case ConsoleKey.D6:
                    case ConsoleKey.NumPad6:
                        chosen = "Historical";
                        break;
                    case ConsoleKey.D7:
                    case ConsoleKey.NumPad7:
                        chosen = "Dystopian";
                        break;
                    case ConsoleKey.D8:
                    case ConsoleKey.NumPad8:
                        chosen = "Adventure";
                        break;
                    case ConsoleKey.Escape:
                        return;
                    default:
                        Console.WriteLine("Invalid selection. Operation cancelled.");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey(true);
                        return;
                }

                // Read original JSON and preserve content and other fields
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string title = root.TryGetProperty("Title", out var pTitle) ? pTitle.GetString() ?? Path.GetFileNameWithoutExtension(filePath) : Path.GetFileNameWithoutExtension(filePath);
                string author = root.TryGetProperty("Author", out var pAuthor) ? pAuthor.GetString() ?? "unknown" : "unknown";
                string buy = root.TryGetProperty("BuyPrice", out var pBuy) ? pBuy.GetString() ?? "0.00" : "0.00";
                string rent = root.TryGetProperty("RentPrice", out var pRent) ? pRent.GetString() ?? "0" : "0";

                string[] contentLines = Array.Empty<string>();
                if (root.TryGetProperty("Content", out var pContent) && pContent.ValueKind == JsonValueKind.Array)
                {
                    contentLines = pContent.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                }
                else if (root.TryGetProperty("Content", out pContent) && pContent.ValueKind == JsonValueKind.String)
                {
                    var text = pContent.GetString() ?? string.Empty;
                    var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
                    contentLines = normalized.Split('\n');
                }

                // Build new JSON with Genre and Category = Fiction
                var outDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Title"] = title,
                    ["Author"] = author,
                    ["Category"] = "Fiction",
                    ["Genre"] = chosen,
                    ["BuyPrice"] = buy,
                    ["RentPrice"] = rent,
                    ["Content"] = contentLines
                };

                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var outJson = JsonSerializer.Serialize(outDict, opts);

                // Destination folder: Database/BooksDB/Fiction
                var booksRoot = GetBooksPath();
                var destDir = Path.Combine(booksRoot, "Fiction");
                Directory.CreateDirectory(destDir);

                var destPath = GetUniqueDestinationFile(destDir, Path.GetFileName(filePath));
                File.WriteAllText(destPath, outJson, Encoding.UTF8);

                // remove original file if different
                if (!string.Equals(Path.GetFullPath(destPath), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(filePath);
                }

                StyleConsPrint.WriteCentered($"Assigned Genre '{chosen}' and moved to Fiction.");
                Console.WriteLine($"File: {Path.GetFileName(destPath)}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddGenreToBook: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }
        // Synchronously triggers the LLMSupport conversion/sorting process.
        // This blocks until the background conversion completes or throws.
        public void SortBooksAutomatically(int timeoutMinutes = 10)
        {
            try
            {
                StyleConsPrint.WriteCentered("Starting automatic book sorter...");
                Console.WriteLine("This may take a while depending on model and number of files.");
                Console.WriteLine();

                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                // Call the LLMSupport conversion method. It is async so wait on it here.
                LLMSupport.ConvertUnsortedBooksToJsonAsync(cts.Token).GetAwaiter().GetResult();

                StyleConsPrint.WriteCentered("Book sorting finished.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (OperationCanceledException)
            {
                StyleConsPrint.WriteCentered("Sorting cancelled (timeout or user cancellation).");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while sorting books: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        // Shows list of files currently in the project's UnsortedBooks folder.
        public void BookMenuChecking()
        {
            while (true)
            {
                Console.Clear();
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
                Console.SetCursorPosition(0, 0);

                UsersDisplayMenu.ViewUnsortedBooksMenu();
                
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        ViewUnsortedBooks();
                        ChangeBooksInfo();
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        ViewUnsortedBooks();
                        ReadBookForCheacking();
                        break;
                    case ConsoleKey.Escape:
                        return;
                    default:
                        break;
                }
            }
        }
        public void ViewUnsortedBooks()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var unsortedDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "UnsortedBooks"));
                Directory.CreateDirectory(unsortedDir);

                Console.Clear();
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
                Console.SetCursorPosition(0, 0);
                StyleConsPrint.WriteCentered("Unsorted Books");

                var files = Directory.EnumerateFiles(unsortedDir, "*.json", SearchOption.TopDirectoryOnly)
                                     .Select(Path.GetFileName)
                                     .ToList();

                if (files.Count == 0)
                {
                    StyleConsPrint.WriteCentered("No files found in UnsortedBooks.");
                    Console.WriteLine("Press any key to return...");
                    Console.ReadKey(true);
                    return;
                }

                for (int i = 0; i < files.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {files[i]}");
                }

                Console.WriteLine();
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing UnsortedBooks: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        // Converts all .txt files in the UnloadedBooks folder into JSON documents
        // using the required structure and writes them to the UnsortedBooks folder.
        // Each .txt file becomes a .json file with the same base name; the original .txt is removed.
        public void ConvertFilesTOJson()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var inputDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "UnloadedBooks"));
                var outputDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "UnsortedBooks"));

                Directory.CreateDirectory(inputDir);
                Directory.CreateDirectory(outputDir);

                Console.Clear();
                StyleConsPrint.WriteCentered("Convert UnloadedBooks -> JSON");
                var files = Directory.EnumerateFiles(inputDir, "*.txt", SearchOption.TopDirectoryOnly).ToList();

                if (files.Count == 0)
                {
                    StyleConsPrint.WriteCentered("No .txt files found in UnloadedBooks.");
                    Console.WriteLine("Press any key to return...");
                    Console.ReadKey(true);
                    return;
                }

                foreach (var file in files)
                {
                    try
                    {
                        // read raw text
                        var raw = File.ReadAllText(file, Encoding.UTF8);

                        // normalize line endings to '\n' and split into lines.
                        // This ensures the JSON "Content" array contains each text line as its own element
                        // (so serialized JSON does not embed \r\n sequences inside a single string).
                        var normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n");
                        var lines = normalized.Split('\n'); // preserve empty lines as empty strings

                        // Build JSON object according to requested schema
                        var jsonObj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Title"] = Path.GetFileNameWithoutExtension(file), // infer from filename
                            ["Author"] = "unknown",
                            ["Category"] = "",
                            ["BuyPrice"] = "1200.00",
                            ["RentPrice"] = "200",
                            ["Content"] = lines
                        };

                        // Use UnsafeRelaxedJsonEscaping so unicode characters (’ etc.) are not escaped as \uXXXX.
                        var opts = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };
                        var json = JsonSerializer.Serialize(jsonObj, opts);

                        var destFileName = Path.GetFileNameWithoutExtension(file) + ".json";
                        var destPath = GetUniqueDestinationFile(outputDir, destFileName);

                        File.WriteAllText(destPath, json, Encoding.UTF8);

                        // remove original text file
                        File.Delete(file);

                        Console.WriteLine($"Converted: {Path.GetFileName(file)} -> {Path.GetFileName(destPath)}");
                    }
                    catch (Exception exFile)
                    {
                        Console.WriteLine($"Failed converting '{Path.GetFileName(file)}': {exFile.Message}");
                    }
                }

                StyleConsPrint.WriteCentered("Conversion complete.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting files: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        // helper that ensures unique filename in a directory
        private static string GetUniqueDestinationFile(string directory, string filename)
        {
            var dest = Path.Combine(directory, filename);
            if (!File.Exists(dest)) return dest;

            var name = Path.GetFileNameWithoutExtension(filename);
            var ext = Path.GetExtension(filename);
            for (int i = 1; ; i++)
            {
                var candidate = Path.Combine(directory, $"{name} ({i}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
        }

        // -------------------------
        // AddUser (kept; unchanged in behavior)
        // -------------------------
        public void AddUser()
        {
            try
            {
                var path = GetUserDBPath();
                var dir = Path.GetDirectoryName(path) ?? Path.GetDirectoryName(AppContext.BaseDirectory);

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Ensure file exists and has a header (optional)
                if (!File.Exists(path))
                {
                    using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                    {
                        sw.WriteLine("ID,Username,Password,Fullname,Age,Email");
                    }
                }

                Console.Clear();
                StyleConsPrint.WriteCentered("Create New User");

                // Determine next ID (tries to parse numeric IDs; otherwise starts at 1)
                int nextId = 1;
                var lines = File.ReadAllLines(path)
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .ToArray();
                foreach (var line in lines)
                {
                    var tokens = line.Split(',');
                    if (tokens.Length == 0) continue;
                    var rawId = tokens[0].Trim().Trim('"');
                    if (rawId.Equals("ID", StringComparison.OrdinalIgnoreCase)) continue;
                    if (int.TryParse(rawId, out int idVal))
                        nextId = Math.Max(nextId, idVal + 1);
                }
                string idInput = nextId.ToString();

                // Read username
                Console.SetCursorPosition(Math.Max(Console.WindowWidth / 2 - 15, 0), Console.CursorTop);
                Console.Write("Username: ");
                string newUsername = Console.ReadLine()?.Trim() ?? string.Empty;

                // Read password (plain text here; consider masking centrally)
                Console.SetCursorPosition(Math.Max(Console.WindowWidth / 2 - 15, 0), Console.CursorTop);
                Console.Write("Password: ");
                string newPassword = Console.ReadLine() ?? string.Empty;

                // Read fullname
                Console.SetCursorPosition(Math.Max(Console.WindowWidth / 2 - 15, 0), Console.CursorTop);
                Console.Write("Fullname: ");
                string newFullname = Console.ReadLine() ?? string.Empty;

                // Read and validate age
                int newAge;
                while (true)
                {
                    Console.SetCursorPosition(Math.Max(Console.WindowWidth / 2 - 15, 0), Console.CursorTop);
                    Console.Write("Age: ");
                    string ageInput = Console.ReadLine() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(ageInput))
                    {
                        StyleConsPrint.WriteBottom("Age cannot be empty. Please enter a valid number.");
                        continue;
                    }
                    if (ageInput.StartsWith("0"))
                    {
                        StyleConsPrint.WriteBottom("Age cannot start with zero. Please enter a valid age.");
                        continue;
                    }
                    if (ageInput.Length > 3)
                    {
                        StyleConsPrint.WriteBottom("Age is too long. Please enter a valid age.");
                        continue;
                    }
                    if (int.TryParse(ageInput, out newAge) && newAge > 0)
                    {
                        break;
                    }
                    StyleConsPrint.WriteBottom("Invalid age. Please enter a valid number.");
                }

                // Read email
                Console.SetCursorPosition(Math.Max(Console.WindowWidth / 2 - 15, 0), Console.CursorTop);
                Console.Write("Email: ");
                string newEmail = Console.ReadLine() ?? string.Empty;
                Console.Clear();

                // Append new user
                using (var sw = new StreamWriter(path, true, Encoding.UTF8))
                {
                    sw.WriteLine($"{idInput},{newUsername},{newPassword},{newFullname},{newAge},{newEmail}");
                }

                StyleConsPrint.WriteBottom("User added successfully.");
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while adding a new user: " + ex.Message);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        // -------------------------
        // File helpers: users & bans
        // -------------------------
        static List<AllUsers> LoadAllUsers()
        {
            var path = GetUserDBPath();
            var list = new List<AllUsers>();
            if (!File.Exists(path))
                return list;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                // Basic CSV parsing: split and trim quotes (sufficient for current simple format)
                var tokens = line.Split(',').Select(t => t.Trim().Trim('"')).ToArray();
                if (tokens.Length == 0) continue;

                // Skip header rows
                if (tokens[0].Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                    (tokens.Length > 1 && tokens[1].Equals("Username", StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (tokens.Length < 6)
                    continue;

                var id = tokens[0];
                var username = tokens[1];
                var password = tokens[2];
                var fullname = tokens[3];
                int age = 0;
                int.TryParse(tokens[4], out age);
                var email = tokens[5];

                list.Add(new AllUsers(id, username, password, fullname, age, email));
            }

            return list;
        }

        static void SaveAllUsers(IEnumerable<AllUsers> users)
        {
            var path = GetUserDBPath();
            var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                // include Credit column in header (backwards compatible)
                sw.WriteLine("ID,Username,Password,Fullname,Age,Email,Credit");
                foreach (var u in users)
                {
                    var safeFullname = (u.Fullname ?? string.Empty).Replace(Environment.NewLine, " ").Replace(",", " ");
                    var safeEmail = (u.Email ?? string.Empty).Replace(",", "");
                    string credit = "0";
                    if (u is StandardUser su) credit = su.Credit.ToString("0.##", CultureInfo.InvariantCulture);
                    sw.WriteLine($"{u.ID},{u.Username},{u.Password},{safeFullname},{u.Age},{safeEmail},{credit}");
                }
            }
        }

        static Dictionary<string, DateTime> LoadBans()
        {
            var path = GetBanDBPath();
            var dict = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return dict;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                var parts = line.Split(new[] { ',' }, 2);
                if (parts.Length < 2)
                    continue;

                var id = parts[0].Trim().Trim('"');
                var datePart = parts[1].Trim().Trim('"' );

                if (DateTime.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                    dict[id] = dt.ToUniversalTime();
            }

            return dict;
        }

        static void SaveBans(Dictionary<string, DateTime> bans)
        {
            var path = GetBanDBPath();
            var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                foreach (var kv in bans)
                {
                    sw.WriteLine($"{kv.Key},{kv.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)}");
                }
            }
        }

        static void RemoveExpiredBans()
        {
            var bans = LoadBans();
            var now = DateTime.UtcNow;
            var expired = bans.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToArray();
            if (expired.Length == 0) return;
            foreach (var id in expired)
                bans.Remove(id);
            SaveBans(bans);
        }

        internal static bool IsUserBanned(string userId, out DateTime untilUtc)
        {
            RemoveExpiredBans();
            var bans = LoadBans();
            if (bans.TryGetValue(userId, out var dt))
            {
                untilUtc = dt;
                if (dt <= DateTime.UtcNow)
                {
                    bans.Remove(userId);
                    SaveBans(bans);
                    untilUtc = DateTime.MinValue;
                    return false;
                }
                return true;
            }
            untilUtc = DateTime.MinValue;
            return false;
        }

        // -------------------------
        // Search & selection helpers for users
        // -------------------------
        static List<AllUsers> SearchUsersByPrefix(string prefix)
        {
            var all = LoadAllUsers();
            if (string.IsNullOrEmpty(prefix))
                return all;
            return all.Where(u => !string.IsNullOrEmpty(u.Username) && u.Username.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        static AllUsers PromptSelectUserFromList(List<AllUsers> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            Console.Clear();
            StyleConsPrint.WriteCentered("Select a User:");
            for (int i = 0; i < candidates.Count; i++)
            {
                var u = candidates[i];
                if (IsUserBanned(u.ID, out var until))
                {
                    Console.WriteLine($"{i + 1}. {u.Username} (ID: {u.ID}) - BANNED until {until.ToLocalTime():f}");
                }
                else
                {
                    Console.WriteLine($"{i + 1}. {u.Username} (ID: {u.ID})");
                }
            }
            Console.WriteLine();
            Console.Write("Select number (0 = cancel): ");
            var input = Console.ReadLine() ?? string.Empty;
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > candidates.Count)
                return null;
            if (sel == 0) return null;
            return candidates[sel - 1];
        }

        // -------------------------
        // Book search & selection helpers
        // -------------------------
        internal static (bool canceled, string input) ReadInputWithCancel()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape)
                    return (true, string.Empty);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return (false, sb.ToString());
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                        Console.Write("\b \b");
                    }
                    continue;
                }
                // printable characters
                if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }

        internal static List<string> SearchBooksByPrefix(string prefix)
        {
            var path = GetBooksPath();
            var list = new List<string>();
            if (!Directory.Exists(path))
                return list;

            var files = Directory.EnumerateFiles(path, "*.txt", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
            {
                var title = Path.GetFileNameWithoutExtension(f);
                if (string.IsNullOrEmpty(prefix) || title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    list.Add(title);
            }

            return list.OrderBy(t => t).ToList();
        }

        internal static string PromptSelectBookFromList(List<string> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            Console.Clear();
            StyleConsPrint.WriteCentered("Select a Book:");
            for (int i = 0; i < candidates.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {candidates[i]}");
            }
            Console.WriteLine();
            Console.WriteLine("Select number (0 = cancel). Press Escape to cancel.");
            Console.Write("Selection: ");

            // read selection with cancel support
            var (canceled, input) = ReadInputWithCancel();
            if (canceled) return null;
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > candidates.Count)
                return null;
            if (sel == 0) return null;
            return candidates[sel - 1];
        }

        // -------------------------
        // Borrowed records helpers (JSON-backed)
        // -------------------------
        internal class BorrowRecord
        {
            public string UserId { get; set; }
            public string Title { get; set; }
            public DateTime BorrowedUtc { get; set; }
            public DateTime? ReturnedUtc { get; set; }
        }

        // JSON model for storage
        internal class BorrowedBookEntry
        {
            public string Title { get; set; }
            public DateTime BorrowedAt { get; set; }
            public DateTime? DueDate { get; set; }
            public DateTime? ReturnedAt { get; set; }
        }

        internal class UserBorrowJson
        {
            public string UserId { get; set; }
            public string Username { get; set; }
            public List<BorrowedBookEntry> BorrowedBooks { get; set; } = new();
        }

        internal static List<BorrowRecord> LoadBorrowedRecords()
        {
            var path = GetPurchesedAndRentedDbPath();
            var list = new List<BorrowRecord>();
            if (!File.Exists(path))
                return list;

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return list;

                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var users = JsonSerializer.Deserialize<List<UserBorrowJson>>(json, opts);
                if (users == null)
                    return list;

                foreach (var u in users)
                {
                    if (u.BorrowedBooks == null) continue;
                    foreach (var be in u.BorrowedBooks)
                    {
                        // map to flat BorrowRecord used by the rest of the code
                        list.Add(new BorrowRecord
                        {
                            UserId = u.UserId ?? string.Empty,
                            Title = be.Title ?? string.Empty,
                            BorrowedUtc = be.BorrowedAt.ToUniversalTime(),
                            ReturnedUtc = be.ReturnedAt?.ToUniversalTime()
                        });
                    }
                }
            }
            catch
            {
                // on parse error return empty list to avoid crashing the app
                return new List<BorrowRecord>();
            }

            return list;
        }

        internal static void SaveBorrowedRecords(IEnumerable<BorrowRecord> records)
        {
            var path = GetPurchesedAndRentedDbPath();
            var dir = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // group flat records into per-user JSON model
            var grouped = records
                .GroupBy(r => r.UserId ?? string.Empty)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // load users to get username where available
            var allUsers = LoadAllUsers().ToDictionary(u => u.ID, u => u.Username, StringComparer.OrdinalIgnoreCase);

            var usersJson = new List<UserBorrowJson>();
            foreach (var kv in grouped)
            {
                var userId = kv.Key;
                var recs = kv.Value;
                var uj = new UserBorrowJson
                {
                    UserId = userId,
                    Username = allUsers.TryGetValue(userId, out var uname) ? uname : string.Empty,
                    BorrowedBooks = new List<BorrowedBookEntry>()
                };

                foreach (var r in recs)
                {
                    var borrowedAtUtc = r.BorrowedUtc.ToUniversalTime();
                    var due = borrowedAtUtc.AddDays(7); // example: 7-day due period
                    uj.BorrowedBooks.Add(new BorrowedBookEntry
                    {
                        Title = r.Title ?? string.Empty,
                        BorrowedAt = borrowedAtUtc,
                        DueDate = due,
                        ReturnedAt = r.ReturnedUtc?.ToUniversalTime()
                    });
                }

                usersJson.Add(uj);
            }

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var outJson = JsonSerializer.Serialize(usersJson, opts);
            File.WriteAllText(path, outJson, Encoding.UTF8);
        }

        // -------------------------
        // Admin actions (implemented earlier)
        // -------------------------
        public void RemoveUser()
        {
            try
            {
                Console.Clear();
                StyleConsPrint.WriteCentered("Remove User");
                Console.Write("Type username (prefix) to search: ");
                var prefix = (Console.ReadLine() ?? string.Empty).Trim();

                var matches = SearchUsersByPrefix(prefix);
                if (matches.Count == 0)
                {
                    StyleConsPrint.WriteCentered("No users found matching that prefix.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return;
                }

                var selected = PromptSelectUserFromList(matches);
                if (selected == null)
                    return;

                Console.WriteLine();
                Console.Write($"Are you sure you want to permanently remove '{selected.Username}' (ID: {selected.ID})? (Y/N): ");
                var k = Console.ReadKey(true).Key;
                if (k != ConsoleKey.Y)
                {
                    StyleConsPrint.WriteCentered("Operation cancelled.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return;
                }

                var users = LoadAllUsers();
                var removedCount = users.RemoveAll(u => u.ID == selected.ID);
                SaveAllUsers(users);

                // also remove any ban record
                var bans = LoadBans();
                if (bans.Remove(selected.ID))
                    SaveBans(bans);

                // and remove current borrowed records for that user
                var borrowed = LoadBorrowedRecords();
                var remaining = borrowed.Where(b => b.UserId != selected.ID).ToList();
                SaveBorrowedRecords(remaining);

                StyleConsPrint.WriteCentered($"User '{selected.Username}' removed ({removedCount} record(s)).");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error removing user: " + ex.Message);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        public void BanUser()
        {
            try
            {
                Console.Clear();
                StyleConsPrint.WriteCentered("Ban / Unban User");
                Console.Write("Type username (prefix) to search: ");
                var prefix = (Console.ReadLine() ?? string.Empty).Trim();

                var matches = SearchUsersByPrefix(prefix);
                if (matches.Count == 0)
                {
                    StyleConsPrint.WriteCentered("No users found matching that prefix.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return;
                }

                var selected = PromptSelectUserFromList(matches);
                if (selected == null)
                    return;

                if (IsUserBanned(selected.ID, out var untilUtc))
                {
                    // Already banned -> offer manual unban
                    Console.WriteLine();
                    StyleConsPrint.WriteCentered($"User '{selected.Username}' is banned until {untilUtc.ToLocalTime():f}.");
                    Console.WriteLine("1. Unban now");
                    Console.WriteLine("2. Cancel");
                    Console.WriteLine();
                    Console.Write("Select option: ");
                    var opt = Console.ReadLine() ?? string.Empty;
                    if (opt.Trim() == "1")
                    {
                        var bans = LoadBans();
                        if (bans.Remove(selected.ID))
                            SaveBans(bans);
                        StyleConsPrint.WriteCentered($"User '{selected.Username}' has been unbanned.");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey(true);
                        return;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    // Ban for 3 days
                    var bans = LoadBans();
                    var expiry = DateTime.UtcNow.AddDays(3);
                    bans[selected.ID] = expiry;
                    SaveBans(bans);
                    StyleConsPrint.WriteCentered($"User '{selected.Username}' banned until {expiry.ToLocalTime():f} (3 days).");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while banning/unbanning: " + ex.Message);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        public void DisplayInfo()
        {
            try
            {
                Console.Clear();
                StyleConsPrint.WriteCentered("Display User Info");
                Console.Write("Type username (prefix) to search: ");
                var prefix = (Console.ReadLine() ?? string.Empty).Trim();

                var matches = SearchUsersByPrefix(prefix);
                if (matches.Count == 0)
                {
                    StyleConsPrint.WriteCentered("No users found matching that prefix.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return;
                }

                var selected = PromptSelectUserFromList(matches);
                if (selected == null)
                    return;

                Console.Clear();
                StyleConsPrint.WriteCentered($"User Info - {selected.Username}");
                Console.WriteLine($"ID      : {selected.ID}");
                Console.WriteLine($"Username: {selected.Username}");
                Console.WriteLine($"Fullname: {selected.Fullname}");
                Console.WriteLine($"Age     : {selected.Age}");
                Console.WriteLine($"Email   : {selected.Email}");

                // borrow stats
                var Purchased = LoadBorrowedRecords();
                var userRecords = Purchased.Where(b => b.UserId == selected.ID).ToList();
                var totalBorrowed = userRecords.Count;
                var currentBorrowed = userRecords.Count(b => !b.ReturnedUtc.HasValue);
                var weekStart = StartOfWeek(DateTime.UtcNow.ToLocalTime(), DayOfWeek.Monday).ToUniversalTime();
                var weeklyBorrowed = userRecords.Count(b => b.BorrowedUtc >= weekStart);

                Console.WriteLine($"Total Purchased Books (ever): {totalBorrowed}");
                Console.WriteLine($"Total Rented Books(ever): ");

                if (IsUserBanned(selected.ID, out var untilUtc))
                {
                    Console.WriteLine($"Status  : BANNED until {untilUtc.ToLocalTime():f}");
                    Console.WriteLine();
                    Console.WriteLine("Options: 1 = Unban user, 2 = Back");
                    Console.Write("Select option: ");
                    var opt = Console.ReadLine() ?? string.Empty;
                    if (opt.Trim() == "1")
                    {
                        var bans = LoadBans();
                        if (bans.Remove(selected.ID))
                            SaveBans(bans);
                        StyleConsPrint.WriteCentered($"User '{selected.Username}' has been unbanned.");
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey(true);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Status  : Active (not banned)");
                    Console.WriteLine();
                    Console.WriteLine("Press any key to return...");
                    Console.ReadKey(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error displaying user info: " + ex.Message);
                Console.WriteLine ("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        internal static DateTime StartOfWeek(DateTime dt, DayOfWeek start)
        {
            int diff = (7 + (dt.DayOfWeek - start)) % 7;
            return dt.Date.AddDays(-1 * diff);
        }

        private static string GetUnsortedBooksPath()
        {
            var baseDir = AppContext.BaseDirectory;
            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "UnsortedBooks"));
        }

        private static string SelectUnsortedBook()
        {
            var unsortedPath = GetUnsortedBooksPath();
            Directory.CreateDirectory(unsortedPath);

            var files = Directory.EnumerateFiles(unsortedPath, "*.json", SearchOption.TopDirectoryOnly)
                                 .OrderBy(Path.GetFileName)
                                 .ToList();

            if (files.Count == 0)
            {
                StyleConsPrint.WriteCentered("No .json files found in UnsortedBooks.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return null;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered("Select a file from UnsortedBooks");
            for (int i = 0; i < files.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
            }
            Console.WriteLine();
            Console.Write("Select number (0 = cancel): ");
            var input = Console.ReadLine() ?? string.Empty;
            if (!int.TryParse(input, out var sel) || sel < 0 || sel > files.Count)
                return null;
            if (sel == 0) return null;
            return files[sel - 1];
        }

        public void ChangeBooksInfo()
        {
            try
            {
                var filePath = SelectUnsortedBook();
                if (string.IsNullOrEmpty(filePath)) return;

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Read existing values (if missing use defaults)
                string curTitle = root.TryGetProperty("Title", out var pTitle) ? pTitle.GetString() ?? "" : Path.GetFileNameWithoutExtension(filePath);
                string curAuthor = root.TryGetProperty("Author", out var pAuthor) ? pAuthor.GetString() ?? "" : "unknown";
                string curCategory = root.TryGetProperty("Category", out var pCat) ? pCat.GetString() ?? "Fiction" : "Fiction";

                string curGenre = root.TryGetProperty("Genre", out var pGenre) ? pGenre.GetString() ?? "" : "";
                string curSubCategory = root.TryGetProperty("SubCategory", out var pSub) ? pSub.GetString() ?? "" : "";

                string curBuy = root.TryGetProperty("BuyPrice", out var pBuy) ? pBuy.GetString() ?? "0.00" : "0.00";
                string curRent = root.TryGetProperty("RentPrice", out var pRent) ? pRent.GetString() ?? "0" : "0";

                Console.Clear();
                StyleConsPrint.WriteCentered("Change Book Info");
                Console.WriteLine($"File: {Path.GetFileName(filePath)}");
                Console.WriteLine();

                // Title
                Console.WriteLine($"Current Title : {curTitle}");
                Console.Write("New Title (Enter = keep): ");
                var newTitle = (Console.ReadLine() ?? "").Trim();
                if (string.IsNullOrEmpty(newTitle)) newTitle = curTitle;

                // Author
                Console.WriteLine($"Current Author: {curAuthor}");
                Console.Write("New Author (Enter = keep): ");
                var newAuthor = (Console.ReadLine() ?? "").Trim();
                if (string.IsNullOrEmpty(newAuthor)) newAuthor = curAuthor;

                // Category (Fiction / NonFiction)
                Console.WriteLine($"Current Category: {curCategory}");
                string newCategory = null;
                while (true)
                {
                    Console.Write("New Category (Fiction / NonFiction) (Enter = keep): ");
                    var inCat = (Console.ReadLine() ?? "").Trim();
                    if (string.IsNullOrEmpty(inCat))
                    {
                        newCategory = curCategory;
                        break;
                    }
                    if (inCat.Equals("Fiction", StringComparison.OrdinalIgnoreCase))
                    {
                        newCategory = "Fiction";
                        break;
                    }
                    if (inCat.Equals("NonFiction", StringComparison.OrdinalIgnoreCase) || inCat.Equals("Non-Fiction", StringComparison.OrdinalIgnoreCase))
                    {
                        newCategory = "NonFiction";
                        break;
                    }
                    Console.WriteLine("Invalid category. Please type Fiction or NonFiction.");
                }

                // BuyPrice
                Console.WriteLine($"Current BuyPrice: {curBuy}");
                string newBuy;
                while (true)
                {
                    Console.Write("New BuyPrice (Enter = keep): ");
                    var inBuy = (Console.ReadLine() ?? "").Trim();
                    if (string.IsNullOrEmpty(inBuy)) { newBuy = curBuy; break; }
                    if (decimal.TryParse(inBuy, NumberStyles.Number, CultureInfo.InvariantCulture, out var dBuy))
                    {
                        newBuy = dBuy.ToString("0.00", CultureInfo.InvariantCulture);
                        break;
                    }
                    Console.WriteLine("Invalid price format. Use digits, optionally with decimals (e.g. 1200 or 1200.00).");
                }

                // RentPrice
                Console.WriteLine($"Current RentPrice: {curRent}");
                string newRent;
                while (true)
                {
                    Console.Write("New RentPrice (Enter = keep): ");
                    var inRent = (Console.ReadLine() ?? "").Trim();
                    if (string.IsNullOrEmpty(inRent)) { newRent = curRent; break; }
                    if (decimal.TryParse(inRent, NumberStyles.Number, CultureInfo.InvariantCulture, out var dRent))
                    {
                        // keep no trailing decimals if whole number originally was integer style, but user likely prefers 2 decimals
                        newRent = dRent.ToString("0.##", CultureInfo.InvariantCulture);
                        break;
                    }
                    Console.WriteLine("Invalid price format. Use digits, optionally with decimals (e.g. 200 or 200.00).");
                }

                // Preserve Content
                string[] contentLines = Array.Empty<string>();
                if (root.TryGetProperty("Content", out var pContent) && pContent.ValueKind == JsonValueKind.Array)
                {
                    contentLines = pContent.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                }

                // Build output dictionary
                var outDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Title"] = newTitle,
                    ["Author"] = newAuthor,
                    ["Category"] = newCategory,
                    ["BuyPrice"] = newBuy,
                    ["RentPrice"] = newRent,
                    ["Content"] = contentLines
                };
                var opts = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var outJson = JsonSerializer.Serialize(outDict, opts);
                File.WriteAllText(filePath, outJson, Encoding.UTF8);

                StyleConsPrint.WriteCentered("Book JSON updated successfully.");
                Console.WriteLine($"Updated file: {Path.GetFileName(filePath)}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ChangeBooksInfo: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        public void ReadBookForCheacking()
        {
            try
            {
                var filePath = SelectUnsortedBook();
                if (string.IsNullOrEmpty(filePath)) return;

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Console.Clear();
                StyleConsPrint.WriteCentered($"Content - {Path.GetFileName(filePath)}");
                Console.WriteLine();

                if (root.TryGetProperty("Content", out var pContent) && pContent.ValueKind == JsonValueKind.Array)
                {
                    var lines = pContent.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        Console.WriteLine($"{i + 1,3}: {lines[i]}");
                    }
                }
                else if (root.TryGetProperty("Content", out pContent) && pContent.ValueKind == JsonValueKind.String)
                {
                    // fallback: single string -> print split lines
                    var text = pContent.GetString() ?? string.Empty;
                    var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
                    var split = normalized.Split('\n');
                    for (int i = 0; i < split.Length; i++)
                        Console.WriteLine($"{i + 1,3}: {split[i]}");
                }
                else
                {
                    Console.WriteLine("[No Content found in JSON]");
                }

                Console.WriteLine();
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                Console.Clear();
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
                Console.SetCursorPosition(0, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadBookForCheacking: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        public void DisplayAllUsersTable()
        {
            try
            {
                var path = GetUserDBPath();
                if (!File.Exists(path))
                {
                    StyleConsPrint.WriteCentered("No users database found.");
                    Console.WriteLine("Press any key to return...");
                    Console.ReadKey(true);
                    return;
                }

                var rawLines = File.ReadAllLines(path, Encoding.UTF8)
                                   .Where(l => !string.IsNullOrWhiteSpace(l))
                                   .ToList();

                // remove header if present
                if (rawLines.Count > 0 &&
                    (rawLines[0].TrimStart().StartsWith("ID", StringComparison.OrdinalIgnoreCase) ||
                     rawLines[0].IndexOf("Username", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    rawLines.RemoveAt(0);
                }

                var rows = new List<string[]>();
                foreach (var line in rawLines)
                {
                    // Basic CSV split - handles the project's simple CSV format
                    var tokens = line.Split(',');
                    if (tokens.Length < 2) continue;

                    var id = tokens.Length > 0 ? tokens[0].Trim() : "";
                    var username = tokens.Length > 1 ? tokens[1].Trim() : "";
                    // password is tokens[2] - ignored here
                    var fullname = tokens.Length > 3 ? tokens[3].Trim() : "";
                    var age = tokens.Length > 4 ? tokens[4].Trim() : "";
                    var email = tokens.Length > 5 ? tokens[5].Trim() : "";
                    var credit = tokens.Length > 6 ? tokens[6].Trim() : "";

                    string status;
                    if (IsUserBanned(id, out var untilUtc))
                    {
                        status = $"BANNED until {untilUtc.ToLocalTime():g}";
                    }
                    else
                    {
                        status = "Active";
                    }

                    rows.Add(new[] { id, username, fullname, age, email, credit, status });
                }

                // Table headers
                var headers = new[] { "ID", "Username", "Full Name", "Age", "Email", "Credit", "Status" };

                // compute column widths
                var colCount = headers.Length;
                var widths = new int[colCount];
                for (int c = 0; c < colCount; c++)
                {
                    widths[c] = headers[c].Length;
                }
                foreach (var r in rows)
                {
                    for (int c = 0; c < colCount; c++)
                    {
                        var v = c < r.Length ? (r[c] ?? "") : "";
                        widths[c] = Math.Max(widths[c], v.Length);
                    }
                }

                // build separators
                string MakeSeparator(char left, char fill, char mid, char right)
                {
                    var sb = new StringBuilder();
                    sb.Append(left);
                    for (int i = 0; i < colCount; i++)
                    {
                        sb.Append(new string(fill, widths[i] + 2));
                        sb.Append(i == colCount - 1 ? right : mid);
                    }
                    return sb.ToString();
                }

                var top = MakeSeparator('+', '-', '+', '+');
                var mid = MakeSeparator('+', '-', '+', '+');
                var bottom = MakeSeparator('+', '-', '+', '+');

                Console.Clear();
                StyleConsPrint.WriteCentered("Users");
                Console.WriteLine();

                // print top border
                Console.WriteLine(top);

                // print header row
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

                // header separator
                Console.WriteLine(mid);

                // print rows
                foreach (var r in rows)
                {
                    var sb = new StringBuilder();
                    sb.Append("|");
                    // ID (right), Username (left), Full Name (left), Age (right), Email (left), Credit (right), Status (left)
                    sb.Append(" ");
                    sb.Append((r[0] ?? "").PadLeft(widths[0]));
                    sb.Append(" | ");

                    sb.Append((r[1] ?? "").PadRight(widths[1]));
                    sb.Append(" | ");

                    sb.Append((r[2] ?? "").PadRight(widths[2]));
                    sb.Append(" | ");

                    sb.Append((r[3] ?? "").PadLeft(widths[3]));
                    sb.Append(" | ");

                    sb.Append((r[4] ?? "").PadRight(widths[4]));
                    sb.Append(" | ");

                    sb.Append((r[5] ?? "").PadLeft(widths[5]));
                    sb.Append(" | ");

                    sb.Append((r[6] ?? "").PadRight(widths[6]));
                    sb.Append(" |");

                    Console.WriteLine(sb.ToString());
                }

                // bottom border
                Console.WriteLine(bottom);

                Console.WriteLine();
                Console.WriteLine($"Total users: {rows.Count}");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying users: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }
    }

    internal class StandardUser : AllUsers, Ilogin, IAccountInfo
    {
        public float Credit { get; set; }
        private const int MaxConcurrentBorrowed = 5;

        public StandardUser(string id, string username, string password, string fullname, int age, string email, float credit)
            : base(id, username, password, fullname, age, email)
        {
            Credit = credit;
        }
        public bool Login(string username, string password)
        {
            return Username == username && Password == password;
        }
        public void DisplayInfo()
        {
            Console.Clear();
            StyleConsPrint.WriteCentered($"User Info - {Username}");
            Console.WriteLine($"ID      : {ID}");
            Console.WriteLine($"Username: {Username}");
            Console.WriteLine($"Fullname: {Fullname}");
            Console.WriteLine($"Age     : {Age}");
            Console.WriteLine($"Email   : {Email}");
            Console.WriteLine();
            Console.WriteLine("Press any key to return...");
            Console.ReadKey(true);
        }

        public void PurchaseBook()
        {
            Console.Clear();
            BooksDisplayMenu.ViewBookMenu();
            var key = Console.ReadKey(true);

            BookHandler.BookInfo selected = null;

            switch (key.Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    // View all - use title selector (empty prefix lists all)
                    selected = BookHandler.BookService.InteractiveSelectBookByTitle();
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    // search book by title
                    selected = BookHandler.BookService.InteractiveSelectBookByTitle();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    // search book by author
                    selected = BookHandler.BookService.InteractiveSelectBookByAuthor();
                    break;
                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    // filter books by subcategory
                    selected = BookHandler.BookService.InteractiveSelectBookBySubCategory();
                    break;
                case ConsoleKey.D5:
                case ConsoleKey.NumPad5:
                    // filter books by genre
                    selected = BookHandler.BookService.InteractiveSelectBookByGenre();
                    break;
                case ConsoleKey.Escape:
                    return;
                default:
                    return;
            }

            if (selected == null) return;

            // Price and confirmation
            float price = selected.Book?.BuyPrice ?? 0f;
            if (!float.TryParse(selected.BuyPriceString, NumberStyles.Any, CultureInfo.InvariantCulture, out var tmp)) price = selected.Book?.BuyPrice ?? 0f;

            if (price <= 0)
            {
                StyleConsPrint.WriteCentered("Selected book has invalid price. Cannot purchase.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered("Purchase Confirmation");
            Console.WriteLine($"Title : {selected.Title}");
            Console.WriteLine($"Author: {selected.Author}");
            Console.WriteLine($"Price : {price.ToString("0.00", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Your Credits: {Credit.ToString("0.##", CultureInfo.InvariantCulture)}");
            Console.WriteLine();
            Console.Write("Confirm purchase? (Y/N): ");
            var confirm = Console.ReadKey(true).Key;
            if (confirm != ConsoleKey.Y)
            {
                StyleConsPrint.WriteCentered("Purchase cancelled.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            if (Credit < price)
            {
                StyleConsPrint.WriteCentered("Insufficient credits for this purchase.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            var before = Credit;
            Credit -= price;

            // persist credit to UsersDB
            Admin.UpdateUserCredit(this.ID, Credit);

            // record transaction
            var rec = new BookHandler.TransactionRecord
            {
                UserId = this.ID,
                Username = this.Username,
                Title = selected.Title,
                Author = selected.Author,
                Action = "Purchase",
                Price = price,
                TimeUtc = DateTime.UtcNow
            };
            BookHandler.BookService.AddTransactionRecord(rec);

            // print centered receipt
            PrintCenteredReceipt("Purchase Receipt", selected.Title, selected.Author, price, before, Credit);

            // done
        }

        public void RentBook()
        {
            Console.Clear();
            BooksDisplayMenu.ViewBookMenu();
            var key = Console.ReadKey(true);

            BookHandler.BookInfo selected = null;

            switch (key.Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    // View all - use title selector (empty prefix lists all)
                    selected = BookHandler.BookService.InteractiveSelectBookByTitle();
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    // search book by title
                    selected = BookHandler.BookService.InteractiveSelectBookByTitle();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    // search book by author
                    selected = BookHandler.BookService.InteractiveSelectBookByAuthor();
                    break;
                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    // filter books by subcategory
                    selected = BookHandler.BookService.InteractiveSelectBookBySubCategory();
                    break;
                case ConsoleKey.D5:
                case ConsoleKey.NumPad5:
                    // filter books by genre
                    selected = BookHandler.BookService.InteractiveSelectBookByGenre();
                    break;
                case ConsoleKey.Escape:
                    return;
                default:
                    return;
            }

            if (selected == null) return;

            float price = selected.Book?.RentPrice ?? 0f;
            if (!float.TryParse(selected.RentPriceString, NumberStyles.Any, CultureInfo.InvariantCulture, out var tmp)) price = selected.Book?.RentPrice ?? 0f;

            if (price <= 0)
            {
                StyleConsPrint.WriteCentered("Selected book has invalid rent price. Cannot rent.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            Console.Clear();
            StyleConsPrint.WriteCentered("Rent Confirmation");
            Console.WriteLine($"Title : {selected.Title}");
            Console.WriteLine($"Author: {selected.Author}");
            Console.WriteLine($"Rent Price : {price.ToString("0.00", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"Your Credits: {Credit.ToString("0.##", CultureInfo.InvariantCulture)}");
            Console.WriteLine();
            Console.Write("Confirm rent? (Y/N): ");
            var confirm = Console.ReadKey(true).Key;
            if (confirm != ConsoleKey.Y)
            {
                StyleConsPrint.WriteCentered("Rent cancelled.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            if (Credit < price)
            {
                StyleConsPrint.WriteCentered("Insufficient credits for this rent.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey(true);
                return;
            }

            var before = Credit;
            Credit -= price;

            // persist credit to UsersDB
            Admin.UpdateUserCredit(this.ID, Credit);

            // record transaction
            var rec = new BookHandler.TransactionRecord
            {
                UserId = this.ID,
                Username = this.Username,
                Title = selected.Title,
                Author = selected.Author,
                Action = "Rent",
                Price = price,
                TimeUtc = DateTime.UtcNow
            };
            BookHandler.BookService.AddTransactionRecord(rec);

            // print centered receipt
            PrintCenteredReceipt("Rent Receipt", selected.Title, selected.Author, price, before, Credit);

            // done
        }

        // centers a small receipt table in the console
        private void PrintCenteredReceipt(string heading, string title, string author, float price, float before, float after)
        {
            var lines = new List<string>();
            lines.Add(heading);
            lines.Add("");
            lines.Add($"Title : {title}");
            lines.Add($"Author: {author}");
            lines.Add("");
            lines.Add($"Price           : {price.ToString("0.00", CultureInfo.InvariantCulture)}");
            lines.Add($"Credits (Before): {before.ToString("0.##", CultureInfo.InvariantCulture)}");
            lines.Add($"Credits (After) : {after.ToString("0.##", CultureInfo.InvariantCulture)}");
            lines.Add("");
            lines.Add("Thank you!");

            int width = lines.Max(l => l.Length) + 4; // padding
            int height = lines.Count + 2; // top/bottom border

            int consoleWidth = Math.Max(Console.WindowWidth, width + 2);
            int consoleHeight = Math.Max(Console.WindowHeight, height + 2);

            int left = Math.Max((consoleWidth - width) / 2, 0);
            int top = Math.Max((consoleHeight - height) / 2, 0);

            Console.Clear();
            // build box lines
            string topBorder = "+" + new string('-', width - 2) + "+";
            string bottomBorder = topBorder;

            // print top border
            Console.SetCursorPosition(left, top);
            Console.Write(topBorder);

            // print content
            for (int i = 0; i < lines.Count; i++)
            {
                Console.SetCursorPosition(left, top + 1 + i);
                var content = lines[i].PadRight(width - 4);
                Console.Write("| " + content + " |");
            }

            // print bottom border
            Console.SetCursorPosition(left, top + 1 + lines.Count);
            Console.Write(bottomBorder);

            Console.SetCursorPosition(0, top + height + 1);
            Console.WriteLine();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        public void SelectingBookGenre()
        {
            // convenience: open genre filter then show results (non-purchasing)
            BookHandler.BookService.InteractiveViewAllBooks();
        }

        public void SelectBookSubCategory()
        {
            BookHandler.BookService.InteractiveViewAllBooks();
        }

        public void ReadBook()
        {
            // Code to read book
        }
        public void addCredit(float amount)
        {
            Credit += amount;
        }
    }
}
