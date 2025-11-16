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
            StyleConsPrint.WriteCentered("4. View All Users");
            StyleConsPrint.WriteCentered("5. View Unsorted Books");
            StyleConsPrint.WriteCentered("6. Sort The Books Automatically");
            StyleConsPrint.WriteCentered("7. Convert Unloaded books into sortable Format Automatically");
            StyleConsPrint.WriteCentered("Esc. Logout");
        }
        public static void ViewUnsortedBooksMenu()
        {
            StyleConsPrint.WriteCentered("Unsorted Books Menu:");
            StyleConsPrint.WriteCentered("1. Change The Info");
            StyleConsPrint.WriteCentered("2. Read The Content");
            StyleConsPrint.WriteCentered("Esc. Go Back");
        }
        public static void ViewAllUsersMenu()
        {
            StyleConsPrint.WriteCentered("View All Users:");
            StyleConsPrint.WriteCentered("ID | Username | Full Name | Age | Email | Status");
            StyleConsPrint.WriteCentered("------------------------------------------------");
        }

        //For User Menu
        public static void UserMenu()
        {
            StyleConsPrint.WriteCentered("User Menu:");
            StyleConsPrint.WriteCentered("1. Go to Book Menu");
            StyleConsPrint.WriteCentered("2. Purchase a Book");
            StyleConsPrint.WriteCentered("3. Rent a Book");
            StyleConsPrint.WriteCentered("4. Display Info");
            StyleConsPrint.WriteCentered("Esc. Logout");
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
