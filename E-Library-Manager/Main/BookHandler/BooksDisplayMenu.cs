using E_Library_Manager.Styles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E_Library_Manager.Main.BookHandler
{
    internal class BooksDisplayMenu
    {
        public static void BookMenu()
        {
            StyleConsPrint.WriteCentered("Book Menu:");
            StyleConsPrint.WriteCentered("1. View Book");
            StyleConsPrint.WriteCentered("2. Read Book");
            StyleConsPrint.WriteCentered("Esc. Back to User Menu");
        }
        public static void ViewBookMenu()
        {
            StyleConsPrint.WriteCentered("View Book Menu:");
            StyleConsPrint.WriteCentered("1. View All Books");
            StyleConsPrint.WriteCentered("2. Search Book by Title");
            StyleConsPrint.WriteCentered("3. Search Book by Author");
            StyleConsPrint.WriteCentered("4. Filter Books by Category");
            StyleConsPrint.WriteCentered("Esc. Back to Book Menu");
        }
        public static void SelectBookCategoryMenu()
        {
            StyleConsPrint.WriteCentered("Select Book Category:");
            StyleConsPrint.WriteCentered("1. Fiction");
            StyleConsPrint.WriteCentered("2. Non-Fiction");
            StyleConsPrint.WriteCentered("Esc. Back to View Book Menu");
        }
        public static void SelectBookSubCategoryMenu()
        {
            StyleConsPrint.WriteCentered("Select SubCategory:");
            StyleConsPrint.WriteCentered("1. History");
            StyleConsPrint.WriteCentered("2. Politics");
            StyleConsPrint.WriteCentered("3. Philosophy");
            StyleConsPrint.WriteCentered("4. Math");
            StyleConsPrint.WriteCentered("5. Science");
            StyleConsPrint.WriteCentered("Esc. Back to Select Book Category Menu");
        }
        public static void SelectBookGenreMenu()
        {
            StyleConsPrint.WriteCentered("Select Genre:");
            StyleConsPrint.WriteCentered("1. Fantasy");
            StyleConsPrint.WriteCentered("2. Science Fiction");
            StyleConsPrint.WriteCentered("3. Mystery");
            StyleConsPrint.WriteCentered("4. Romance");
            StyleConsPrint.WriteCentered("5. Horror");
            StyleConsPrint.WriteCentered("6. Historical");
            StyleConsPrint.WriteCentered("7. Dystopian");
            StyleConsPrint.WriteCentered("8. Adventure");
            StyleConsPrint.WriteCentered("Esc. Back to Select Book Category Menu");
        }
    }
}
