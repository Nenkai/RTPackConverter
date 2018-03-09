using System;
using System.Drawing;

using Console = Colorful.Console;

namespace RTPackConverter
{
    public static class Utils
    {
        public static void Log(string text)
        {
            if (!Program.isBatchConvert)
            {
                Console.WriteLine(text);
            }
        }

        public static void Log(string text, Color color)
        {
            if (!Program.isBatchConvert)
            {
                Console.WriteLine(text, color);
            }
        }

        public static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }
}
