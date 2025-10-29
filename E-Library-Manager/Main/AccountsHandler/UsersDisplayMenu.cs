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
        public static void AdminMenu()
        {
            StyleConsPrint.WriteCentered("Admin Menu:");
            StyleConsPrint.WriteCentered("1. Add User");
            StyleConsPrint.WriteCentered("2. Remove User");
            StyleConsPrint.WriteCentered("3. View All Users");
            StyleConsPrint.WriteCentered("4. Logout");
        }

        public static void UserMenu()
        {
            StyleConsPrint.WriteCentered("User Menu:");
            StyleConsPrint.WriteCentered("1. View Available Books");
            StyleConsPrint.WriteCentered("2. Borrow Book");
            StyleConsPrint.WriteCentered("3. Return Book");
            StyleConsPrint.WriteCentered("4. Logout");
        }

        public static void BookMenu()
        {
            StyleConsPrint.WriteCentered("Book Menu:");
            StyleConsPrint.WriteCentered("1. View Book Details");
            StyleConsPrint.WriteCentered("2. Search Books");
            StyleConsPrint.WriteCentered("3. Back to User Menu");
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
