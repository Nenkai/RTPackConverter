using System;
using System.IO;
using System.Drawing;

using static RTPackConverter.Utils;
using static RTPackConverter.Constants;

namespace RTPackConverter
{
    class RTTEX
    {
        public Bitmap texture;
        public int Height;
        public int Width;
        public GL_FORMATS format;
        public int OriginalHeight;
        public int OriginalWidth;
        public bool UsesAlpha;
        public bool IsCompressed;
        public int MipmapCount;

        public RTTEX(BinaryReader bdr)
        {
            bdr.BaseStream.Seek(2, SeekOrigin.Current);

            //RTTEXHeader
            Log("Reading image data...", Color.Yellow);

            Height = bdr.ReadInt32();
            Width = bdr.ReadInt32();

            Utils.Log($"-> Size {Height}x{Width}", Color.Orange);
            GL_FORMATS Format = (GL_FORMATS)bdr.ReadInt32();

            Log($"-> Format {Format}", Color.Orange);
            //Support for other types could be added, we only use the mainstream one for now
            if (Format != GL_FORMATS.OGL_RGBA_8888)
            {
                throw new NotImplementedException("Only OGL_RGBA_8888 (4 bytes per pixel) formats are supported yet.");
            }

            OriginalHeight = bdr.ReadInt32();
            OriginalWidth = bdr.ReadInt32();
            Log($"-> Original Size {OriginalHeight}x{OriginalWidth}", Color.Orange);

            UsesAlpha = bdr.ReadByte() == 1;
            IsCompressed = bdr.ReadByte() == 1;
            Log($"-> Alpha? {UsesAlpha} - Compressed? {IsCompressed}", Color.Orange);

            short ReservedFlags = bdr.ReadInt16();
            MipmapCount = bdr.ReadInt32();
            int[] rttex_reserved = new int[16];
            for (int i = 0; i < 16; i++)
            {
                rttex_reserved[i] = bdr.ReadInt32();
            }

            bdr.BaseStream.Seek(24, SeekOrigin.Current);

            Log($"{Program.currentFileName} --> {Path.GetFileNameWithoutExtension(Program.currentFileName)}.png", Color.Green);
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
            this.texture = texture.Bitmap;
        }
    }
}
