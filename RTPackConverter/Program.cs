using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.IO;
using System.IO.Compression;

using Console = Colorful.Console;

using static RTPackConverter.Utils;
using static RTPackConverter.Constants;

namespace RTPackConverter
{
    class Program
    {
        public static bool isBatchConvert = false;
        private static int processedFiles = 0;
        public static string currentFileName;

        static void Main(string[] args)
        {
            Console.WriteLine("================ RTPack Converter =================", Color.White);
            if (args.Length == 0)
            {
                Console.WriteLine("Drop a RTPACK File (rtfont/rttex) or a folder into this executable to convert it.", Color.OrangeRed);
                Console.WriteLine("(Press any key to exit...)", Color.OrangeRed);
                Console.ReadKey();
                return;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();

            foreach (var i in args)
            {
                FileAttributes attr = File.GetAttributes(i);
                if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    string[] files = Directory.GetFiles(i, "*.*", SearchOption.TopDirectoryOnly).Where(s => s.EndsWith(".rttex") || s.EndsWith(".rtfont")).ToArray();
                    if (files.Length > 1) isBatchConvert = true;
                    foreach (string f in files)
                    {
                        currentFileName = Path.GetFileName(f);
                        try
                        {
                            ConvertRTPACKFile(f);
                            Console.WriteLine($@"Converted {currentFileName} to png.", Color.LightGreen);
                            processedFiles++;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}", Color.OrangeRed);
                            continue;
                        }
                    }
                }
                else
                {
                    if (args.Length > 1) isBatchConvert = true;
                    currentFileName = Path.GetFileName(i);
                    try
                    {
                        ConvertRTPACKFile(i);
                        Console.WriteLine("Converted " + currentFileName + " to png.", Color.LightGreen);
                        processedFiles++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($@"[!] Could not convert {currentFileName} : {e.Message}", Color.OrangeRed);
                        continue;
                    }
                }
            }
            sw.Stop();
            Console.WriteLine($"Done. Processed {processedFiles} files in {sw.Elapsed.Minutes}mins {sw.Elapsed.Seconds}seconds.", Color.Green);
            Console.WriteLine("\n(Press any key to exit)", Color.Yellow);
            Console.ReadKey();
        }

        /// <summary>
        /// Converts a RTPack File to .png by file name. Returns false if invalid file.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        static void ConvertRTPACKFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                BinaryReader br = new BinaryReader(fs);

                /*
                 * Main File Header.
                 * - RTTXTR -> Texture File.
                 * - RTFONT -> Font File.
                 * - RTPACK -> One of them, recompressed after output. (RTPack.exe ".rtfont/.rttex")
                 * 
                 * */
                string rtpack_magic = new string(br.ReadChars(6));
                if (String.Equals(rtpack_magic, "RTTXTR"))
                {
                    Log("Uncompressed texture file detected.", Color.LimeGreen);
                    new RTTEX(br).texture.Save($@"{Path.GetDirectoryName(filename)}\{Path.GetFileNameWithoutExtension(filename)}.png");
                }
                else if (String.Equals(rtpack_magic, "RTFONT"))
                {
                    Log("Uncompressed font file detected.", Color.LimeGreen);
                    RTFONT font = new RTFONT(br);
                    font.fontBitmap.texture.Save($@"{Path.GetDirectoryName(filename)}\{Path.GetFileNameWithoutExtension(filename)}.png");
                    Log("Extracting all characters.", Color.LimeGreen);
                    font.ExtractCharacters(filename);
                }
                else if (String.Equals(rtpack_magic, "RTPACK"))
                {
                    byte version = br.ReadByte();
                    byte reserved = br.ReadByte();

                    //RTPACK Header (24 bytes)
                    uint compressedSize = br.ReadUInt32();
                    Log($"-> Compressed Size : {BytesToString(compressedSize)}", Color.Orange);
                    uint decompressedSize = br.ReadUInt32();
                    Log($"-> Decompressed Size : {BytesToString(decompressedSize)}", Color.Orange);

                    eCompressionType compressionType = (eCompressionType)br.ReadByte();
                    Log($"-> Compression Type : {compressionType}", Color.Orange);

                    fs.Seek(15, SeekOrigin.Current);

                    //Zlib Magic header (78 9C), IO.Compression doesn't want it for deflate so just skip it
                    fs.ReadByte();
                    fs.ReadByte();

                    //RTFONT Header
                    using (MemoryStream ms = new MemoryStream())
                    {
                        if (compressionType == eCompressionType.C_COMPRESSION_ZLIB)
                        {
                            Log("Decompressing..", Color.Yellow);
                            using (DeflateStream zs = new DeflateStream(fs, CompressionMode.Decompress))
                            {

                                zs.CopyTo(ms);
                            }
                            ms.Position = 0;

                            //Decompress and save file
#if DEBUG
                            using (FileStream file = new FileStream(Path.GetFileNameWithoutExtension(filename) + ".rtpack", FileMode.Create))
                            {
                                ms.CopyTo(file);
                                ms.Position = 0;
                            }
#endif

                        }
                        else
                        {
                            fs.CopyTo(ms);
                            ms.Position = 0;
                        }

                        Log("Loaded onto memory.");
                        BinaryReader bdr = new BinaryReader(ms);

                        //RTFile Header again (8 bytes)
                        string decomp_magic = new string(bdr.ReadChars(6));
                        if (String.Equals(decomp_magic, "RTTXTR"))
                        {
                            Log("Texture file detected. (RTTXTR/rttex)", Color.LimeGreen);
                            new RTTEX(bdr).texture.Save($@"{Path.GetDirectoryName(filename)}\{Path.GetFileNameWithoutExtension(filename)}.png");
                        }
                        else if (String.Equals(decomp_magic, "RTFONT"))
                        {
                            Log("Font detected. (RTFONT/rtfont)", Color.LimeGreen);
                            RTFONT font = new RTFONT(bdr);
                            font.fontBitmap.texture.Save($@"{Path.GetDirectoryName(filename)}\{Path.GetFileNameWithoutExtension(filename)}.png");
                            Log("Extracting all characters.", Color.LimeGreen);
                            font.ExtractCharacters(filename);
                        }
                    }
                }
                else
                {
                    throw new FileFormatException("Not a RTPACK file.");
                }
            }
        }
    }
}



