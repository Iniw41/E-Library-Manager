using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using E_Library_Manager.Main.AccountsHandler;
using E_Library_Manager.Styles;

namespace E_Library_Manager.Main
{
    internal class Program
    {
        static void Main(string[] args)
        {
            StyleConsPrint.WriteCentered("E-Library Manager");
            mainmenu_selection();
        }

        static void mainmenu_display_login()
        {
            StyleConsPrint.WriteCentered("Select User type: ");
            StyleConsPrint.WriteCentered("1. Admin  2. User");
        }

        static void mainmenu_selection()
        {
            while (true)
            {
                Console.Clear();
                StyleConsPrint.WriteCentered("E-Library Manager");
                mainmenu_display_login();

                var keypress = Console.ReadKey(true);
                switch (keypress.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        Console.Clear();
                        // Authenticate and get Admin instance from DB
                        var admin = AuthenticateAdmin();
                        if (admin != null)
                        {
                            StyleConsPrint.WriteCentered("Admin login successful.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            AdminSession(admin);
                        }
                        else
                        {
                            StyleConsPrint.WriteCentered("Admin login failed. Press any key to return.");
                            Console.ReadKey(true);
                        }
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        Console.Clear();
                        // Authenticate and get StandardUser instance from DB
                        var user = AuthenticateUser();
                        if (user != null)
                        {
                            StyleConsPrint.WriteCentered("User login successful.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            UserSession(user);
                        }
                        else
                        {
                            StyleConsPrint.WriteCentered("User login failed. Press any key to return.");
                            Console.ReadKey(true);
                        }
                        break;

                    case ConsoleKey.Escape:
                        return;

                    default:
                        // ignore other keys, re-display menu
                        break;
                }
            }
        }

        // Authenticate admin and return Admin instance when credentials match, otherwise null.
        static Admin? AuthenticateAdmin()
        {
            StyleConsPrint.WriteCentered("Admin Login");

            Console.Write("Username: ");
            string username = Console.ReadLine() ?? string.Empty;

            Console.Write("Password: ");
            string password = ReadPasswordMasked();

            return GetAdminFromDb(username, password);
        }

        // Reads the AdminDB and returns a populated Admin object when a match is found.
        static Admin? GetAdminFromDb(string username, string password)
        {
            try
            {
                var path = GetAdminDbPath();
                if (!File.Exists(path))
                    return null;

                foreach (var rawLine in File.ReadLines(path))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // Extract quoted strings and numbers. Tokens order: id, username, password, fullname, age, email
                    var matches = Regex.Matches(line, "\"([^\"]*)\"|(\\d+)");
                    var tokens = matches.Cast<Match>()
                                        .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                                        .ToArray();

                    if (tokens.Length < 3)
                        continue;

                    var fileUsername = tokens[1];
                    var filePassword = tokens[2];

                    if (fileUsername == username && filePassword == password)
                    {
                        string id = tokens.Length > 0 ? tokens[0] : "0";
                        string fullname = tokens.Length > 3 ? tokens[3] : string.Empty;
                        int age = 0;
                        if (tokens.Length > 4) int.TryParse(tokens[4], out age);
                        string email = tokens.Length > 5 ? tokens[5] : string.Empty;

                        return new Admin(id, fileUsername, filePassword, fullname, age, email);
                    }
                }
            }
            catch
            {
                Console.WriteLine("Error reading Admin database.");
            }

            return null;
        }

        // Attempt to locate the AdminDB.txt relative to the running assembly.
        static string GetAdminDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "AdminDB.txt"));
            return candidate;
        }

        // -----------------------
        // User authentication (new)
        // -----------------------

        static StandardUser? AuthenticateUser()
        {
            StyleConsPrint.WriteCentered("User Login");

            Console.Write("Username: ");
            string username = Console.ReadLine() ?? string.Empty;

            Console.Write("Password: ");
            string password = ReadPasswordMasked();

            return GetUserFromDb(username, password);
        }

        static StandardUser? GetUserFromDb(string username, string password)
        {
            try
            {
                var path = GetUserDbPath();
                if (!File.Exists(path))
                    return null;

                foreach (var rawLine in File.ReadLines(path))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // Support both quoted and plain CSV formats.
                    if (line.Contains('"'))
                    {
                        var matches = Regex.Matches(line, "\"([^\"]*)\"|(\\d+)");
                        var tokens = matches.Cast<Match>()
                                            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                                            .ToArray();

                        if (tokens.Length < 3)
                            continue;

                        var fileUsername = tokens.Length > 1 ? tokens[1] : string.Empty;
                        var filePassword = tokens.Length > 2 ? tokens[2] : string.Empty;

                        if (fileUsername == username && filePassword == password)
                        {
                            string id = tokens.Length > 0 ? tokens[0] : "0";
                            string fullname = tokens.Length > 3 ? tokens[3] : string.Empty;
                            int age = 0;
                            if (tokens.Length > 4) int.TryParse(tokens[4], out age);
                            string email = tokens.Length > 5 ? tokens[5] : string.Empty;
                            float credit = 0.0f;

                            return new StandardUser(id, fileUsername, filePassword, fullname, age, email, credit);
                        }
                    }
                    else
                    {
                        var tokens = line.Split(',')
                                         .Select(t => t.Trim().Trim('"'))
                                         .ToArray();

                        if (tokens.Length < 3)
                            continue;

                        // Skip header rows if present
                        if (tokens[0].Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                            tokens[1].Equals("Username", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var fileId = tokens.Length > 0 ? tokens[0] : "0";
                        var fileUsername = tokens.Length > 1 ? tokens[1] : string.Empty;
                        var filePassword = tokens.Length > 2 ? tokens[2] : string.Empty;

                        if (fileUsername == username && filePassword == password)
                        {
                            string fullname = tokens.Length > 3 ? tokens[3] : string.Empty;
                            int age = 0;
                            if (tokens.Length > 4) int.TryParse(tokens[4], out age);
                            string email = tokens.Length > 5 ? tokens[5] : string.Empty;
                            float credit = 0.0f;

                            return new StandardUser(fileId, fileUsername, filePassword, fullname, age, email, credit);
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine("Error reading Users database.");
            }

            return null;
        }

        static string GetUserDbPath()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Database", "usersDB", "UsersDB.txt"));
            return candidate;
        }

        // Admin session now receives the authenticated Admin instance and delegates AddUser to it.
        static void AdminSession(Admin admin)
        {
            while (true)
            {
                Console.Clear();
                StyleConsPrint.WriteCentered($"E-Library Manager - Admin ({admin.Username})");
                UsersDisplayMenu.AdminMenu();

                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        Console.Clear();
                        // Call AddUser from the Admin instance
                        admin.AddUser();
                        StyleConsPrint.WriteCentered("Operation complete. Press any key to return to Admin Menu...");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        Console.Clear();
                        admin.RemoveUser();
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        Console.Clear();
                        admin.BanUser();
                        Console.ReadKey(true);
                        break;
                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                        Console.Clear();
                        admin.DisplayInfo();
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D5:
                    case ConsoleKey.NumPad5:
                    case ConsoleKey.Escape:
                        // Logout -> return to main menu
                        return;

                    default:
                        // ignore and refresh menu
                        break;
                }
            }
        }

        // User session now receives the authenticated StandardUser instance.
        static void UserSession(StandardUser user)
        {
            while (true)
            {
                Console.Clear();
                StyleConsPrint.WriteCentered($"E-Library Manager - User ({user.Username})");
                UsersDisplayMenu.UserMenu();

                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        Console.Clear();
                        StyleConsPrint.WriteCentered("View Available Books selected. (Not implemented)");
                        Console.WriteLine("Press any key to return to User Menu...");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        Console.Clear();
                        user.CheckoutBook();
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        Console.Clear();
                        StyleConsPrint.WriteCentered("Return Book selected. (Not implemented)");
                        Console.WriteLine("Press any key to return to User Menu...");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D4:
                    case ConsoleKey.NumPad4:
                    case ConsoleKey.Escape:
                        // Logout -> return to main menu
                        return;

                    default:
                        // ignore and refresh menu
                        break;
                }
            }
        }
        // Reads password from console while masking input with '*'.

        static string ReadPasswordMasked()
        {
            var sb = new StringBuilder();
            ConsoleKeyInfo key;

            while (true)
            {
                key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (sb.Length > 0)
                    {
                        sb.Length--;
                        // move cursor back, overwrite with space, move back again
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write('*');
                }
            }

            return sb.ToString();
        }
    }
}