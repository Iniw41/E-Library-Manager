using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using E_Library_Manager.LLM_Support;
using E_Library_Manager.Styles;

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
        public void ViewUnsortedBooks()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var unsortedDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "UnsortedBooks"));
                Directory.CreateDirectory(unsortedDir);

                Console.Clear();
                StyleConsPrint.WriteCentered("Unsorted Books");

                var files = Directory.EnumerateFiles(unsortedDir, "*.txt", SearchOption.TopDirectoryOnly)
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
            try
            {
                // Check ban status first
                if (Admin.IsUserBanned(ID, out var untilUtc))
                {
                    StyleConsPrint.WriteCentered($"You are banned until {untilUtc.ToLocalTime():f}. Cannot borrow books.");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return;
                }

                Console.Clear();
                StyleConsPrint.WriteCentered("Borrow Book");
                Console.WriteLine("Type book title (prefix). Press Enter to list all. Press Escape to cancel.");
                Console.Write("Search: ");

                var (canceled, prefix) = ReadInputWithCancelForUser();
                if (canceled)
                    return;

                var books = Admin.SearchBooksByPrefix(prefix);
                if (books.Count == 0)
                {
                    StyleConsPrint.WriteCentered("No books found.");
                    Console.WriteLine("Press any key to return...");
                    Console.ReadKey(true);
                    return;
                }

                var selectedTitle = Admin.PromptSelectBookFromList(books);
                if (selectedTitle == null)
                    return;

                // Load borrowed records and compute counts
                var borrowed = Admin.LoadBorrowedRecords();
                var userRecords = borrowed.Where(b => b.UserId == ID).ToList();
                var currentBorrowed = userRecords.Count(b => !b.ReturnedUtc.HasValue);
                var weekStart = Admin.StartOfWeek(DateTime.UtcNow.ToLocalTime(), DayOfWeek.Monday).ToUniversalTime();
                var weeklyBorrowed = userRecords.Count(b => b.BorrowedUtc >= weekStart);

                if (currentBorrowed >= MaxConcurrentBorrowed)
                {
                    StyleConsPrint.WriteCentered($"You currently have {currentBorrowed} books borrowed. Return a book before borrowing more (max {MaxConcurrentBorrowed}).");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey(true);
                    return;
                }

                // Add borrow record
                var newRec = new Admin.BorrowRecord
                {
                    UserId = ID,
                    Title = selectedTitle,
                    BorrowedUtc = DateTime.UtcNow,
                    ReturnedUtc = null
                };

                borrowed.Add(newRec);
                Admin.SaveBorrowedRecords(borrowed);

                StyleConsPrint.WriteCentered($"Book '{selectedTitle}' borrowed successfully.");
                Console.WriteLine($"Current borrowed: {currentBorrowed + 1}");
                Console.WriteLine($"Borrowed this week: {weeklyBorrowed + 1}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error borrowing book: " + ex.Message);
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        // Helper for ReadInputWithCancel usable from StandardUser
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
