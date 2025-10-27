using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace E_Library_Manager.Styles
{
    internal class StyleConsPrint
    {
        public static void WriteCentered(string text)
        {
            int windowWidth = Console.WindowWidth;
            int textLength = text.Length;
            int leftPadding = Math.Max((windowWidth - textLength) / 2, 0);
            Console.SetCursorPosition(leftPadding, Console.CursorTop);
            Console.WriteLine(text);
            Console.WriteLine();
        }

        public static void WriteBottom(string text)
        {
            int windowWidth = Console.WindowWidth;
            int windowHeight = Console.WindowHeight;
            int textLength = text.Length;
            int leftPadding = Math.Max((windowWidth - textLength) / 2, 0);

            // Set cursor to the last line
            Console.SetCursorPosition(leftPadding, windowHeight - 1);
            Console.WriteLine(text);
        }
    }
}
