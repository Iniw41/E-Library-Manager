using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E_Library_Manager.Main.BookHandler
{
    internal class Books
    {
        //the book data
        public string Title { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public float Price { get; set; }

        public Books(string title, string author, string category, float price)
        {
            Title = title;
            Author = author;
            Category = category;
            Price = price;
        }
    }
    internal class NonFiction : Books
    {
        public string SubCategory { get; set; }
        public NonFiction(string title, string author, string category, float price, string subCategory) : base(title, author, category, price)
        {
            SubCategory = subCategory;


        }
    }
    internal class Fiction : Books
    {
        public string Genre { get; set; }
        public Fiction(string title, string author, string category, float price, string genre) : base(title, author, category, price)
        {
            Genre = genre;
        }
    }
    public enum BookCategory
    {
        Fiction,
        NonFiction
    }
    public enum FictionGenre
    {
        Fantasy,
        ScienceFiction,
        Mystery,
        Romance,
        Horror,
        Historical,
        Dystopian,
        Adventure,wha
    }
    public enum NonFictionSubCategory
    {
        Philosophy,
        Politics,
        History,
        Math,
        Science,
    }
}
