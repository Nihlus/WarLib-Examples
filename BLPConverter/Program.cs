using System;
using Mono.Options;
using WarLib.BLP;
using System.IO;
using System.Collections.Generic;
using System.Drawing;

namespace BLPConverter
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			string filePath = "";
			string fileType = "";
			string outputPath = "";
			bool showHelp = false;

			OptionSet options = new OptionSet()
			{
				{ "if=|file=", "The path to the BLP to convert.", v =>
                    {
                        filePath = v.ToString();
                    }
                },
                { "f=|output-format=", "The format of the output image.", v =>
                    {
                        fileType = v.ToString();
                    } 
                },
                { "of=|output-file=", "The location of the output image.", v => outputPath = v },
                { "h|help", "Show this message and exit.", v => showHelp = v != null }
            };                       


            BLP blp = new BLP(File.ReadAllBytes(args[0]));
            Bitmap map = blp.GetMipMap(0);
            map.Save(args[1] + "/peaceflower.png", System.Drawing.Imaging.ImageFormat.Png);  

            List<string> extra;
            try
            {
                extra = options.Parse(args);
            }
            catch (OptionException oex)
            {
                Console.WriteLine(oex.Message.ToString());
                return;
            }

            if (showHelp)
            {
                ShowHelp(options);
                return;
            }

            if (extra.Count > 0)
            {                
                             
            }
            else
            {
                ShowHelp(options);
            }
        }

        static void ShowHelp(OptionSet options)
        {
            Console.WriteLine("Usage: blpconverter --file <path> --output-format <format>");
            Console.WriteLine("Converts a World of Warcraft BLP file into a normal image format.");
            Console.WriteLine("Supported formats are PNG, JPEG, TGA, and BMP.");
            Console.WriteLine();
            Console.WriteLine("Options: ");
            options.WriteOptionDescriptions(Console.Out);
        }
    }
}
