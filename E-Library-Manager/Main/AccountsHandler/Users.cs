using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using E_Library_Manager.Styles;

{
    
}


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
        //this class holds the basic info like id, username, password, age, fullname
        public int ID { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Fullname { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }

        public AllUsers(int id, string username, string password, string fullname, int age, string email) 
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
        public string filePath = "C:\\Users\\User 103\\source\\repos\\E-Library-Manager\\E-Library-Manager\\Database\\UsersDB.txt";
        public Admin(int id, string username, string password, string fullname, int age, string email) 
            : base(id, username, password, fullname, age, email)
        { }
        
        public bool Login(string username, string password)
        {
            return Username == username && Password == password;
        }
        
        public void AddUser()
        {
            // Code to add a new standard user
            try
            {
                if(!File.Exists(filePath))
                {
                    //someting
                    using (StreamWriter sw = new StreamWriter(filePath))
                    {
                        UsersDisplayMenu.CreateNewUserMenu();
                        while (true)
                        {
                            Console.SetCursorPosition(Console.WindowWidth / 2 - 10, Console.CursorTop);
                            string newUsername = Console.ReadLine();
                            Console.SetCursorPosition(Console.WindowWidth / 2 - 10, Console.CursorTop);
                            string newPassword = Console.ReadLine();
                            Console.SetCursorPosition(Console.WindowWidth / 2 - 10, Console.CursorTop);
                            string newFullname = Console.ReadLine();
                            Console.SetCursorPosition(Console.WindowWidth / 2 - 10, Console.CursorTop);
                            string ageInput = Console.ReadLine();
                            Console.SetCursorPosition(Console.WindowWidth / 2 - 10, Console.CursorTop);
                            string newEmail = Console.ReadLine();
                            if (int.TryParse(ageInput, out int newAge))
                            {
                                sw.WriteLine($"{newUsername}, {newPassword}, {newFullname}, {newAge}, {newEmail}");
                                break;
                            }
                            else
                            {
                                StyleConsPrint.WriteCentered("Invalid age. Please enter a valid number.");
                                Console.SetCursorPosition(Console.WindowWidth / 2 - 10, Console.CursorTop - 5);
                            }
                        }
                        sw.WriteLine("NewUser, password, Fullname, Age, Email");
                    }
                }
                else
                {
                    using (StreamWriter sw = new StreamWriter(filePath, true))
                    {
                        sw.WriteLine("NewUser, password, Fullname, Age, Email");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while adding a new user: " + ex.Message);
            }
        }

        public void RemoveUser()
        {
            // Code to remove an existing user
        }

        public void DisplayInfo()
        {
            // Code to display admin information
        }

    }

    internal class StandardUser : AllUsers, Ilogin, IAccountInfo
    {
        public StandardUser(int id, string username, string password, string fullname, int age, string email) 
            : base(id, username, password, fullname, age, email)
        { }
        public bool Login(string username, string password)
        {
            return Username == username && Password == password;
        }
        public void DisplayInfo()
        {
            // Code to display user information
        }

        public void BorrowBook()
        {
            // Code to borrow a book
        }
        public void ReturnBook()
        {
            // Code to return a book
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
            // Code to show top book compleations
        }
    }
}
