using System;
using System.Text;
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
                        Console.Clear();
                        if (PromptLogin(isAdmin: true))
                        {
                            StyleConsPrint.WriteCentered("Admin login successful.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            if (PromptLogin(isAdmin: true))
                            {
                                AdminSession();
                            }
                        }
                        else
                        {
                            StyleConsPrint.WriteCentered("Admin login failed. Press any key to return.");
                            Console.ReadKey(true);
                        }
                        break;

                    case ConsoleKey.D2:
                        Console.Clear();
                        if (PromptLogin(isAdmin: false))
                        {
                            StyleConsPrint.WriteCentered("User login successful.");
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadKey(true);
                            if (PromptLogin(isAdmin: false))
                            {
                                UserSession();
                            }
                        }
                        else
                        {
                            StyleConsPrint.WriteCentered("User login failed. Press any key to return.");
                            Console.ReadKey(true);
                        }
                        break;

                    case ConsoleKey.Escape:
                        // Allow exit with Escape
                        return;

                    default:
                        // ignore other keys, re-display menu
                        break;
                }
            }
        }
        // Simple admin session loop — displays the AdminMenu and handles choices.
        static void AdminSession()
        {
            while (true)
            {
                Console.Clear();
                StyleConsPrint.WriteCentered("E-Library Manager - Admin");
                UsersDisplayMenu.AdminMenu();

                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                    case ConsoleKey.NumPad1:
                        Console.Clear();
                        StyleConsPrint.WriteCentered("Add User selected. (Not implemented)");
                        Console.WriteLine("Press any key to return to Admin Menu...");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D2:
                    case ConsoleKey.NumPad2:
                        Console.Clear();
                        StyleConsPrint.WriteCentered("Remove User selected. (Not implemented)");
                        Console.WriteLine("Press any key to return to Admin Menu...");
                        Console.ReadKey(true);
                        break;

                    case ConsoleKey.D3:
                    case ConsoleKey.NumPad3:
                        Console.Clear();
                        StyleConsPrint.WriteCentered("View All Users selected. (Not implemented)");
                        Console.WriteLine("Press any key to return to Admin Menu...");
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

        static void UserSession()
        {
            while (true)
            {
                Console.Clear();
                StyleConsPrint.WriteCentered("E-Library Manager - User");
                // User menu display and handling would go here
                Console.WriteLine("Press Escape to logout...");
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                {
                    // Logout -> return to main menu
                    return;
                }
            }
        }

        // Prompts for username and password, validates against simple in-memory defaults.
        // Returns true when credentials match.
        static bool PromptLogin(bool isAdmin)
        {
            // Sample/default users (mirror what exists in Users.cs defualtusers)
            var admin = new Admin(1, "admin", "123", "Francis Rainier Cutamora", 20, "lemonsour41@gmail.com");
            var standard = new StandardUser(2, "user", "123", "Jane Doe", 21, "user@example.com");

            StyleConsPrint.WriteCentered(isAdmin ? "Admin Login" : "User Login");

            Console.Write("Username: ");
            string username = Console.ReadLine() ?? string.Empty;

            Console.Write("Password: ");
            string password = ReadPasswordMasked();

            

            // Validate
            if (isAdmin)
            {
                return admin.Login(username, password);
            }
            else
            {
                return standard.Login(username, password);
            }

        }

        
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