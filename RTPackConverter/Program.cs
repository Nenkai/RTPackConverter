using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Linq;

using Console = Colorful.Console;

namespace RTPackConverter
{
    class Program
    {
        static bool isBatchConvert = false;
        static int processedFiles = 0;
        static string currentFileName;

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
                            Console.WriteLine($@"[!] Could not convert {currentFileName}", Color.OrangeRed);
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
                        Console.WriteLine($@"[!] Could not convert {currentFileName}", Color.OrangeRed);
                        continue;
                    }
                }
            }
            sw.Stop();
            Console.WriteLine($"Done. Processed {processedFiles} files in {sw.Elapsed.Minutes}mins {sw.Elapsed.Seconds}seconds.", Color.Green);
            Console.WriteLine("\n(Press any key to exit)", Color.Yellow);
            Console.ReadKey();
        }

        private static void Log(string text)
        {
            if (!isBatchConvert)
            {
                Console.WriteLine(text);
            }
        }

        private static void Log(string text, Color color)
        {
            if (!isBatchConvert)
            {
                Console.WriteLine(text, color);
            }
        }

        static void ConvertRTPACKFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                BinaryReader br = new BinaryReader(fs);

                //RTFile Header (8 bytes)
                string rtpack_magic = new string(br.ReadChars(6));
                if (!String.Equals(rtpack_magic, "RTPACK"))
                {
                    Log("Not a valid rtfont file. Press any key to exit...", Color.OrangeRed);
                    Console.ReadKey();
                    return;
                }
                Log("File has valid RTPACK magic.", Color.LimeGreen);
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
                        HandleRTTEX(bdr).Bitmap.Save($@"{Path.GetDirectoryName(filename)}\{Path.GetFileNameWithoutExtension(filename)}.png");
                    }
                    else if (String.Equals(decomp_magic, "RTFONT"))
                    {
                        Log("Font detected. (RTFONT/rtfont)", Color.LimeGreen);
                        HandleRTFONT(bdr).Bitmap.Save($@"{Path.GetDirectoryName(filename)}\{Path.GetFileNameWithoutExtension(filename)}.png");
                    }

                }
            }
        }

        private static DirectBitmap HandleRTFONT(BinaryReader bdr)
        {
            byte rtfont_version = bdr.ReadByte();
            byte rtfont_reserved = bdr.ReadByte();

            short charSpacing = bdr.ReadInt16();
            short lineHeight = bdr.ReadInt16();
            short lineSpacing = bdr.ReadInt16();
            short shadowXOffset = bdr.ReadInt16();
            short shadowYOffset = bdr.ReadInt16();
            short firstChar = bdr.ReadInt16();
            short lastChar = bdr.ReadInt16();
            short blankCharWidth = bdr.ReadInt16();
            short fontStateCount = bdr.ReadInt16();
            short kerningPairCount = bdr.ReadInt16();
            bdr.BaseStream.Seek(124, SeekOrigin.Current); //Reserved

            //Character definitions (not really used for anything here)
            for (int i = 0; i < lastChar - 1; i++)
            {
                short bmpPosX = bdr.ReadInt16();
                short bmpPosY = bdr.ReadInt16();
                short charSizeX = bdr.ReadInt16();
                short charSizeY = bdr.ReadInt16();
                short charBmpOffsetX = bdr.ReadInt16();
                short charBmpOffsetY = bdr.ReadInt16();
                float charBmpPosU = bdr.ReadSingle();
                float charBmpPosV = bdr.ReadSingle();
                float charBmpPosU2 = bdr.ReadSingle();
                float charBmpPosV2 = bdr.ReadSingle();
                short xadvance = bdr.ReadInt16();
            }

            //No other choice to do that, wasn't able to successfully find out where it ends
            //But we don't use that data yet so...
            bdr.BaseStream.Seek(28, SeekOrigin.Current);

            //Font color definitions
            List<FontState> fontStates = new List<FontState>();
            Log("Getting font states...", Color.Yellow);
            for (int i = 0; i < fontStateCount; i++)
            {
                FontState state = new FontState();
                state.Color = Color.FromArgb((bdr.ReadByte() << 16 | bdr.ReadByte() << 8 | bdr.ReadByte() | bdr.ReadByte() << 24));
                byte[] charRaw = { bdr.ReadByte() };
                state.CharTrigger = Encoding.ASCII.GetString(charRaw)[0];
                fontStates.Add(state);
                bdr.BaseStream.Seek(3, SeekOrigin.Current); //blank reserved
                Log($"{state.CharTrigger}|A:{state.Color.A},R:{state.Color.R},G:{state.Color.G},B:{state.Color.B}", Color.LightBlue);
            }

            //Skip the magic and prepare
            bdr.BaseStream.Seek(6, SeekOrigin.Current);

            //RTTEX Image begins here (magic and others) so we convert it the regular way
            return HandleRTTEX(bdr);
        }

        private static DirectBitmap HandleRTTEX(BinaryReader bdr)
        {
            bdr.BaseStream.Seek(2, SeekOrigin.Current);

            //RTTEXHeader
            Log("Reading image data...", Color.Yellow);

            int Height = bdr.ReadInt32();
            int Width = bdr.ReadInt32();

            Log($"-> Size {Height}x{Width}", Color.Orange);
            GL_FORMATS Format = (GL_FORMATS)bdr.ReadInt32();

            Log($"-> Format {Format}", Color.Orange);
            //Support for other types could be added, we only use the mainstream one for now
            if (Format != GL_FORMATS.OGL_RGBA_8888)
            {
                throw new NotImplementedException("Only OGL_RGBA_8888 (4 bytes per pixel) formats are supported yet.");
            }

            int OriginalHeight = bdr.ReadInt32();
            int OriginalWidth = bdr.ReadInt32();
            Log($"-> Original Size {OriginalHeight}x{OriginalWidth}", Color.Orange);

            bool UsesAlpha = bdr.ReadByte() == 1;
            bool IsCompressed = bdr.ReadByte() == 1;
            Log($"-> Alpha? {UsesAlpha} - Compressed? {IsCompressed}", Color.Orange);

            short ReservedFlags = bdr.ReadInt16();
            int MipmapCount = bdr.ReadInt32();
            int[] rttex_reserved = new int[16];
            for (int i = 0; i < 16; i++)
            {
                rttex_reserved[i] = bdr.ReadInt32();
            }

            bdr.BaseStream.Seek(24, SeekOrigin.Current);

            Log($"{currentFileName} --> {Path.GetFileNameWithoutExtension(currentFileName)}.png", Color.Green);
            DirectBitmap texture = new DirectBitmap(Width, Height);
            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (UsesAlpha)
                    {
                        texture.Bits[x + y * Width] = (bdr.ReadByte() << 16 | bdr.ReadByte() << 8 | bdr.ReadByte() | bdr.ReadByte() << 24);
                    }
                    else
                    {
                        texture.Bits[x + y * Width] = (bdr.ReadByte() << 16 | bdr.ReadByte() << 8 | bdr.ReadByte() | -16777216);
                    }
                }

            }
            bdr.Dispose();
            return texture;
        }

        static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        enum eCompressionType
        {
            C_COMPRESSION_NONE = 0,
            C_COMPRESSION_ZLIB = 1
        };

        public struct FontState
        {
            public Color Color { get; set; }
            public char CharTrigger { get; set; }
        }

        enum GL_FORMATS
        {
            OGL_PVRTC2 = 0x8C00,
            OGL_PVRTC2_2 = 0x8C01,
            OGL_PVRTC4 = 0x8C02,
            OGL_PVRTC4_2 = 0x8C03,
            OGL_RGBA_4444 = 0x8033,

            OGL_RGBA_8888 = 0x1401,
            OGL_RGB_565 = 0x8363
        }
    }
}



