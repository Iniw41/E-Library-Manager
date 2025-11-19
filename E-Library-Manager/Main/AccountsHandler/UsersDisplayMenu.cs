using E_Library_Manager.Styles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//Frontend Lmao 
namespace E_Library_Manager.Main.AccountsHandler
{
    internal class UsersDisplayMenu
    {
        //For Admin Menu
        public static void AdminMenu()
        {
            StyleConsPrint.WriteCentered("Admin Menu:");
            StyleConsPrint.WriteCentered("1. Add User");
            StyleConsPrint.WriteCentered("2. Remove User");
            StyleConsPrint.WriteCentered("3. Ban User");
            StyleConsPrint.WriteCentered("4. View All Users(Idividually)");
            StyleConsPrint.WriteCentered("D. View All User(Table Format)");
            StyleConsPrint.WriteCentered("5. Verify Books Info");
            StyleConsPrint.WriteCentered("6. Sort The Books Automatically");
            StyleConsPrint.WriteCentered("7. Sort The Books Manually");
            StyleConsPrint.WriteCentered("8. Convert Unloaded books into sortable Format Automatically");
            StyleConsPrint.WriteCentered("H. Help info for all options");
            StyleConsPrint.WriteCentered("Esc. Logout");
        }

        public static void HelpAdminMenu()
        {
            StyleConsPrint.WriteCentered("Help - Admin Menu:");
            Console.WriteLine("1. Add User - Create a new user account.");
            Console.WriteLine("2. Remove User - Delete an existing user account.");
            Console.WriteLine("3. Ban User - Restrict a user from accessing their account.");
            Console.WriteLine("4. View All Users - Display a list of all registered users.");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Admin Book Management Options:");
            Console.WriteLine("This step is the must be the 2nd step you do after converting all the files into json, so  you can add and the author and change the pricing of the books");
            Console.WriteLine("5. Verify Books Info - Check and validate book information and change the book information you can also read the content of the books before sorting.");
            Console.WriteLine("6. Sort The Books Automatically - Organize books using an LLM model (SrEgg) Caution Not recomended the model still need more data");
            Console.WriteLine("7. Sort The Books Manually -  Organize the boook by you the Librarian");
            Console.WriteLine("   -In making the Books sortable You are required to do this step first.");
            Console.WriteLine("8. Convert Unloaded books into sortable Format Automatically - Converts the books into from txt to a json file so it can be sorted (requred to do this first)");
            Console.WriteLine("H. Help info for all options - Display this help information.");
            Console.WriteLine("Esc. Logout - Exit the admin menu and log out.");
        }
        public static void ViewUnsortedBooksMenu()
        {
            StyleConsPrint.WriteCentered("Unsorted Books Menu:");
            StyleConsPrint.WriteCentered("1. Change The Info");
            StyleConsPrint.WriteCentered("2. Read The Content");
            StyleConsPrint.WriteCentered("Esc. Go Back");
        }

        //For User Menu
        //Todo Features
        public static void UserMenu()
        {
            StyleConsPrint.WriteCentered("User Menu:");
            StyleConsPrint.WriteCentered("1. Read a Book");
            StyleConsPrint.WriteCentered("2. Purchase a Book");
            StyleConsPrint.WriteCentered("3. Rent a Book");
            StyleConsPrint.WriteCentered("4. Display User Info");
            StyleConsPrint.WriteCentered("5. Contact staff");
            StyleConsPrint.WriteCentered("6. Add Credits");
            StyleConsPrint.WriteCentered("H. Help info for all options");
            StyleConsPrint.WriteCentered("Esc. Logout");
        }
        public static void HelpUserMenu()
        {
            StyleConsPrint.WriteCentered("Help - User Menu:");
            Console.WriteLine("1. Go to Book Menu - Access the book browsing and reading section.");
            Console.WriteLine("2. Purchase a Book - Buy a book to own permanently.");
            Console.WriteLine("3. Rent a Book - Borrow a book for a limited time.");
            Console.WriteLine("4. Display Info - View your account details and status.");
            Console.WriteLine("5. Contact staff - Reach out to library staff for assistance.");
            Console.WriteLine("6. Add Credits - Top up your account balance for purchases and rentals.");
            Console.WriteLine("H. Help info for all options - Display this help information.");
            Console.WriteLine("Esc. Logout - Exit the user menu and log out.");
        }
        public static void DisplayBookMenu()
        {
            StyleConsPrint.WriteCentered("Book Menu:");
            StyleConsPrint.WriteCentered("1. View Purchased Books");
            StyleConsPrint.WriteCentered("2. View Rented Books");
            StyleConsPrint.WriteCentered("Esc. Go Back");
        }
        public static void CreateNewUserMenu()
        {
            StyleConsPrint.WriteCentered("Create New User:");
            StyleConsPrint.WriteCentered("Please enter the following details:");
            StyleConsPrint.WriteCentered("ID:");
            StyleConsPrint.WriteCentered("Username:");
            StyleConsPrint.WriteCentered("Password:");
            StyleConsPrint.WriteCentered("Full Name:");
            StyleConsPrint.WriteCentered("Age:");
            StyleConsPrint.WriteCentered("Email:");
        }

    }
}
