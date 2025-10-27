using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;


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
        public Admin(int id, string username, string password, string fullname, int age, string email) 
            : base(id, username, password, fullname, age, email)
        { }
        
        public bool Login(string username, string password)
        {
            return Username == username && Password == password;
        }
        
        public void AddUser()
        {
            // Code to add a new user
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
