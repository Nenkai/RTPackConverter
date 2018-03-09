using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System;

using static RTPackConverter.Utils;

namespace RTPackConverter
{
    class RTFONT
    {
        public short charSpacing;
        public short lineHeight;
        public short lineSpacing;
        public short shadowXOffset;
        public short shadowYOffset;
        public short firstChar;
        public short lastChar;
        public short blankCharWidth;
        public short fontStateCount;
        public short kerningPairCount;
        public List<CharData> charList = new List<CharData>();
        public List<KerningPair> KerningPairList = new List<KerningPair>();
        public List<FontState> fontStates = new List<FontState>();
        public RTTEX fontBitmap;

        public RTFONT(BinaryReader bdr)
        {
            byte rtfont_version = bdr.ReadByte();
            byte rtfont_reserved = bdr.ReadByte();

            charSpacing = bdr.ReadInt16();
            lineHeight = bdr.ReadInt16();
            lineSpacing = bdr.ReadInt16();
            shadowXOffset = bdr.ReadInt16();
            shadowYOffset = bdr.ReadInt16();
            firstChar = bdr.ReadInt16();
            lastChar = bdr.ReadInt16();
            blankCharWidth = bdr.ReadInt16();
            fontStateCount = bdr.ReadInt16();
            kerningPairCount = bdr.ReadInt16();
            bdr.BaseStream.Seek(124, SeekOrigin.Current); //Reserved

            //Character definitions
            for (int i = 0; i < lastChar - firstChar; i++)
            {
                CharData charDef = new CharData();
                charDef.bmpPosX = bdr.ReadInt16();
                charDef.bmpPosY = bdr.ReadInt16();
                charDef.charSizeX = bdr.ReadInt16();
                charDef.charSizeY = bdr.ReadInt16();
                charDef.charBmpOffsetX = bdr.ReadInt16();
                charDef.charBmpOffsetY = bdr.ReadInt16();
                charDef.charBmpPosU = bdr.ReadSingle();
                charDef.charBmpPosV = bdr.ReadSingle();
                charDef.charBmpPosU2 = bdr.ReadSingle();
                charDef.charBmpPosV2 = bdr.ReadSingle();
                charDef.xadvance = bdr.ReadInt16();
                charList.Add(charDef);

                //Struct alignment (2 bytes)
                bdr.BaseStream.Seek(2, SeekOrigin.Current);
            }
            Log($"Parsed {lastChar - firstChar} characters.", Color.Yellow);
            for (var i = 0; i < kerningPairCount; i++)
            {
                KerningPair kp = new KerningPair();
                kp.first = bdr.ReadInt16();
                kp.second = bdr.ReadInt16();
                kp.amount = (char)bdr.ReadByte();

                //Struct alignment (1 bytes)
                bdr.ReadByte();
            }

            //Font color definitions
            Log($"Getting font states ({fontStateCount})...", Color.Yellow);
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
            fontBitmap = new RTTEX(bdr);
        }

        public void ExtractCharacters(string fullPath)
        {
            string charsPath = Path.Combine(Path.GetDirectoryName(fullPath), Path.GetFileNameWithoutExtension(fullPath));
            Directory.CreateDirectory(charsPath);
            int i = 1;
            foreach (var character in charList)
            {
                if (character.charSizeX == 0 && character.charSizeY == 0) continue;

                i++;
                Rectangle charRect = new Rectangle(character.bmpPosX, character.bmpPosY,
                    character.charSizeX, character.charSizeY);

                var charBmp = fontBitmap.texture.Clone(charRect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                charBmp.Save(charsPath + "\\" + i + ".png");

                charBmp.Dispose();
            }
        }
    }


    // Physically 30 bytes, Padding = 32
    public struct CharData
    {
        public short bmpPosX, bmpPosY;
        public short charSizeX, charSizeY;
        public short charBmpOffsetX;
        public short charBmpOffsetY;
        public float charBmpPosU, charBmpPosV;
        public float charBmpPosU2, charBmpPosV2;
        public short xadvance;
    };

    public struct FontState
    {
        public Color Color { get; set; }
        public char CharTrigger { get; set; }
    }

    // Physically 5 bytes, Padding = 6
    public struct KerningPair
    {
        public short first, second;
        public char amount;
    };
}
