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
        
        static string GetBorrowedDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "BorrowedDB.json"));
            return candidate;
        }
        //-------------------------
        // Sorting Books (new)
        //-------------------------
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
                            ["Category"] = "Fiction",
                            ["Genre"] = "One of: Fantasy, ScienceFiction, Mystery, Romance, Horror, Historical, Dystopian, Adventure",
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
                sw.WriteLine("ID,Username,Password,Fullname,Age,Email");
                foreach (var u in users)
                {
                    var safeFullname = (u.Fullname ?? string.Empty).Replace(Environment.NewLine, " ").Replace(",", " ");
                    var safeEmail = (u.Email ?? string.Empty).Replace(",", "");
                    sw.WriteLine($"{u.ID},{u.Username},{u.Password},{safeFullname},{u.Age},{safeEmail}");
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
            var path = GetBorrowedDbPath();
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
            var path = GetBorrowedDbPath();
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
                var borrowed = LoadBorrowedRecords();
                var userRecords = borrowed.Where(b => b.UserId == selected.ID).ToList();
                var totalBorrowed = userRecords.Count;
                var currentBorrowed = userRecords.Count(b => !b.ReturnedUtc.HasValue);
                var weekStart = StartOfWeek(DateTime.UtcNow.ToLocalTime(), DayOfWeek.Monday).ToUniversalTime();
                var weeklyBorrowed = userRecords.Count(b => b.BorrowedUtc >= weekStart);

                Console.WriteLine($"Total borrowed (ever): {totalBorrowed}");
                Console.WriteLine($"Current borrowed: {currentBorrowed}");
                Console.WriteLine($"Borrowed this week: {weeklyBorrowed}");

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

                // Genre or SubCategory depending on category
                string newGenre = curGenre;
                string newSubCategory = curSubCategory;
                if (newCategory == "Fiction")
                {
                    // Offer Genre
                    var current = !string.IsNullOrEmpty(curGenre) ? curGenre : curSubCategory;
                    Console.WriteLine($"Current Genre: {current}");
                    Console.Write("New Genre (Enter = keep): ");
                    var g = (Console.ReadLine() ?? "").Trim();
                    if (!string.IsNullOrEmpty(g)) newGenre = g;
                    // clear subcategory
                    newSubCategory = null;
                }
                else
                {
                    // Offer SubCategory
                    var current = !string.IsNullOrEmpty(curSubCategory) ? curSubCategory : curGenre;
                    Console.WriteLine($"Current SubCategory: {current}");
                    Console.Write("New SubCategory (Enter = keep): ");
                    var s = (Console.ReadLine() ?? "").Trim();
                    if (!string.IsNullOrEmpty(s)) newSubCategory = s;
                    // clear genre
                    newGenre = null;
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

                if (newCategory == "Fiction")
                {
                    outDict["Genre"] = newGenre ?? "";
                    // ensure SubCategory is not present
                }
                else
                {
                    outDict["SubCategory"] = newSubCategory ?? "";
                    // ensure Genre is not present
                }

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadBookForCheacking: {ex.Message}");
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

        public void CheckoutBook()
        {
            SelectingTheBookMainCategory();
        }
                
        static (bool canceled, string input) ReadInputWithCancelForUser()
        {
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine();
                    return (true, string.Empty);
                }
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
                if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write(key.KeyChar);
                }
            }
        }

        public void SelectingTheBookMainCategory()
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
                        SelectingBookGenre();
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        SelectingBookSubCategory();
                        break;
                    case ConsoleKey.Escape:
                        return;

                    default:
                        break;
                }
            }
        }
        public void SelectingBookSubCategory()
        {
            while (true)
            {
                Console.Clear();
                BooksDisplayMenu.SelectBookSubCategoryMenu();
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        //display the History SubCategory
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        //display the Politics SubCategory
                        break;
                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        //display the Philosophy SubCategory
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        //display the Math SubCategory
                        break;
                    case ConsoleKey.D5:
                    case ConsoleKey.NumPad5:
                        //display the Science SubCategory
                        break;
                    case ConsoleKey.Escape:
                        return;

                    default:
                        break;
                }
            }
        }
        public void SelectingBookGenre()
        {
            while (true)
            {
                Console.Clear();
                BooksDisplayMenu.SelectBookGenreMenu();
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        //display the Fantasy Genre
                        break;
                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        //display the Science Fiction Genre
                        break;
                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        //display the Mystery Genre
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        //display the Romance Genre
                        break;
                    case ConsoleKey.D5:
                    case ConsoleKey.NumPad5:
                        //display the Horror Genre
                        break;
                    case ConsoleKey.D6:
                    case ConsoleKey.NumPad6:
                        //display the Historical Genre
                        break;
                    case ConsoleKey.D7:
                    case ConsoleKey.NumPad7:
                        //display the Dystopian Genre
                        break;
                    case ConsoleKey.D8:
                    case ConsoleKey.NumPad8:
                        //display the Adventure Genre
                        break;
                    case ConsoleKey.Escape:
                        return;

                    default:
                        break;
                }
            }
        }

        public void GoToBookMenu()
        {
            // Code to go to book menu
        }
        public void ReadBook()
        {
            // Code to read book
        }
        public void ReturnBook()
        {
            // Not implemented here — you can implement UI to mark a borrowed record as returned (set ReturnedUtc).
            // This is required to decrease the current borrowed count.
        }
        public void ViewBorrowedBooks()
        {
            // Code to view borrowed books
        }
        public void ViewBoookInfo()
        {
            // Code to view book information
        }
        public void ShowTopBorrowers()
        {
            // Code to show top borrowers
        }
        public void ShowTopBookCompleationists()
        {
            // Code to show top book completions
        }
        public void addCredit(float amount)
        {
            Credit += amount;
        }
    }
}
