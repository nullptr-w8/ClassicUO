using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ClassicUO.IO;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    static class FontRenderer
    {
        private static FontAtlas _atlas;

        public static void Load(GraphicsDevice device, string path)
        {
            _atlas = new FontAtlas();
            _atlas.Load(device, path);
        }

        public static void DrawUnicode
        (
            UltimaBatcher2D batcher,
            byte fontIndex,
            string text,
            int x,
            int y,
            ref Vector3 hue
        )
        {
            hue.Y = ShaderHueTranslator.SHADER_TEXT_HUE_NO_BLACK;

            Draw(batcher, _atlas.Unicodes[fontIndex], text, x, y, ref hue);
        }

        public static void DrawASCII
        (
            UltimaBatcher2D batcher,
            byte fontIndex,
            string text,
            int x,
            int y,
            ref Vector3 hue
        )
        {
            hue.Y = fontIndex != 5 && fontIndex != 8 ? ShaderHueTranslator.SHADER_PARTIAL_HUED : ShaderHueTranslator.SHADER_HUED;

            Draw(batcher, _atlas.ASCIIs[fontIndex], text, x, y, ref hue);
        }

        public static void Draw(UltimaBatcher2D batcher, BaseUOFont font, string text, int x, int y, ref Vector3 hue)
        {
            int startX = x;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    x = startX;
                    y += font.MaxHeight;

                    continue;
                }

                Rectangle rect = font.GetCharBounds(c);
                Texture2D texture = font.GetTextureByChar(c);

                batcher.Draw2D
                (
                    texture,
                    x,
                    y,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height,
                    ref hue
                );

                x += rect.Width;
            }
        }
    }

    unsafe class FontAtlas
    {
        [StructLayout(LayoutKind.Sequential)]
        internal ref struct FontHeader
        {
            public byte Width, Height, Unknown;
        }




        // ASCII: https://github.com/polserver/UOFiddler/blob/master/Ultima/ASCIIFont.cs
        // UNICODE: https://github.com/polserver/UOFiddler/blob/master/Ultima/UnicodeFont.cs

        public List<AsciiFont> ASCIIs = new List<AsciiFont>();
        public List<UnicodeFont> Unicodes = new List<UnicodeFont>();

        public void Load(GraphicsDevice device, string uopath)
        {
            string asciiFontPath = Path.Combine(uopath, "fonts.mul");
            string[] unicodeFontPaths = new string[20];

            for (int i = 0; i < unicodeFontPaths.Length; ++i)
            {
                unicodeFontPaths[i] = Path.Combine(uopath, $"unifont{(i == 0 ? "" : i.ToString())}.mul");
            }

            using (UOFile asciiFontFile = new UOFile(asciiFontPath, true))
            {
                int fontCount = GetFontCount(asciiFontFile, 224);

                for (byte i = 0; i < fontCount; ++i)
                {
                    ASCIIs.Add(new AsciiFont(device, asciiFontFile, i, new FontSettings() { CharsCount = 224 }));
                }
            }


            for (byte i = 0; i < unicodeFontPaths.Length; ++i)
            {
                FileInfo file = new FileInfo(unicodeFontPaths[i]);

                if (file.Exists)
                {
                    using (UOFile uniFontFile = new UOFile(file.FullName, true))
                    {
                        Unicodes.Add(new UnicodeFont(device, uniFontFile, i));
                        Unicodes.Add(new UnicodeFont(device, uniFontFile, i, new FontSettings() { Border = true }));
                        Unicodes.Add(new UnicodeFont(device, uniFontFile, i, new FontSettings() { Italic = true }));
                        Unicodes.Add(new UnicodeFont(device, uniFontFile, i, new FontSettings() { Bold = true }));
                        Unicodes.Add(new UnicodeFont(device, uniFontFile, i, new FontSettings() { Underline = true }));
                    }
                }
            }
        }

        private int GetFontCount(UOFile file, int charactersCount)
        {
            file.Seek(0);

            bool done = false;
            int fontCount = 0;
            int headerSize = sizeof(FontHeader);

            while (!file.IsEOF)
            {
                file.Skip(1);

                for (int i = 0; i < charactersCount; ++i)
                {
                    FontHeader* fh = (FontHeader*)file.PositionAddress;

                    if (file.Position + headerSize >= file.Length)
                    {
                        continue;
                    }

                    file.Skip(headerSize);

                    int bcount = fh->Width * fh->Height * sizeof(ushort);

                    if (file.Position + bcount > file.Length)
                    {
                        done = true;
                        break;
                    }

                    file.Skip(bcount);
                }

                if (done)
                {
                    break;
                }

                ++fontCount;
            }

            file.Seek(0);
            return fontCount;
        }

    }

    class UnicodeFont : BaseUOFont
    {
        struct FontBakerCharacterRange
        {
            public static readonly FontBakerCharacterRange BasicLatin = new FontBakerCharacterRange((char)0x0020, (char)0x007F);
            public static readonly FontBakerCharacterRange Latin1Supplement = new FontBakerCharacterRange((char)0x00A0, (char)0x00FF);
            public static readonly FontBakerCharacterRange LatinExtendedA = new FontBakerCharacterRange((char)0x0100, (char)0x017F);
            public static readonly FontBakerCharacterRange LatinExtendedB = new FontBakerCharacterRange((char)0x0180, (char)0x024F);
            public static readonly FontBakerCharacterRange Cyrillic = new FontBakerCharacterRange((char)0x0400, (char)0x04FF);
            public static readonly FontBakerCharacterRange CyrillicSupplement = new FontBakerCharacterRange((char)0x0500, (char)0x052F);
            public static readonly FontBakerCharacterRange Hiragana = new FontBakerCharacterRange((char)0x3040, (char)0x309F);
            public static readonly FontBakerCharacterRange Katakana = new FontBakerCharacterRange((char)0x30A0, (char)0x30FF);

            public char Start { get; private set; }
            public char End { get; private set; }

            public FontBakerCharacterRange(char start, char end)
            {
                Start = start;
                End = end;
            }
        }

        private List<Texture2D> _backerTextures { get; } = new List<Texture2D>();


        public UnicodeFont(GraphicsDevice device, UOFile file, byte font, FontSettings settings = default)
            : base(device, file, font, settings)
        {
            HasItalic = settings.Italic;
            HasUnderline = settings.Underline;
            HasBorder = settings.Border;
            HasBold = settings.Bold;
        }


        public bool HasItalic { get; }
        public bool HasUnderline { get; }
        public bool HasBorder { get; }
        public bool HasBold { get; }



        public override Texture2D GetTextureByChar(char c)
        {
            if (c >= FontBakerCharacterRange.BasicLatin.Start && c < FontBakerCharacterRange.BasicLatin.End)
            {
                return _backerTextures[0];
            }

            if (c >= FontBakerCharacterRange.Latin1Supplement.Start && c < FontBakerCharacterRange.Latin1Supplement.End)
            {
                return _backerTextures[1];
            }

            if (c >= FontBakerCharacterRange.LatinExtendedA.Start && c < FontBakerCharacterRange.LatinExtendedA.End)
            {
                return _backerTextures[2];
            }

            if (c >= FontBakerCharacterRange.LatinExtendedB.Start && c < FontBakerCharacterRange.LatinExtendedB.End)
            {
                return _backerTextures[3];
            }

            if (c >= FontBakerCharacterRange.Cyrillic.Start && c < FontBakerCharacterRange.Cyrillic.End)
            {
                return _backerTextures[4];
            }

            if (c >= FontBakerCharacterRange.CyrillicSupplement.Start && c < FontBakerCharacterRange.CyrillicSupplement.End)
            {
                return _backerTextures[5];
            }

            if (c >= FontBakerCharacterRange.Hiragana.Start && c < FontBakerCharacterRange.Hiragana.End)
            {
                return _backerTextures[6];
            }

            if (c >= FontBakerCharacterRange.Katakana.Start && c < FontBakerCharacterRange.Katakana.End)
            {
                return _backerTextures[7];
            }

            throw new NotImplementedException("texture for this char is not implemented");
        }


        protected override unsafe void CreateTextureAtlas(GraphicsDevice device, UOFile file, FontSettings settings, byte fontIndex)
        {
            const int UNICODE_SPACE_WIDTH = 8;
            const float ITALIC_FONT_KOEFFICIENT = 3.3f;
            const uint UO_BLACK = 0xFF010101;
            const uint DEFAULT_HUE = 0xFF_FF_FF_FF;


            bool isItalic = settings.Italic;
            bool isSolid = settings.Bold;
            bool isUnderline = settings.Underline;
            bool isBlackBorder = settings.Border;
            uint* table = (uint*)file.StartAddress;

            List<FontBakerCharacterRange> allbakers = new List<FontBakerCharacterRange>();
            allbakers.Add(FontBakerCharacterRange.BasicLatin);
            allbakers.Add(FontBakerCharacterRange.Latin1Supplement);
            allbakers.Add(FontBakerCharacterRange.LatinExtendedA);
            allbakers.Add(FontBakerCharacterRange.LatinExtendedB);
            allbakers.Add(FontBakerCharacterRange.Cyrillic);
            allbakers.Add(FontBakerCharacterRange.CyrillicSupplement);
            allbakers.Add(FontBakerCharacterRange.Hiragana);
            allbakers.Add(FontBakerCharacterRange.Katakana);


            for (int f = 0; f < allbakers.Count; ++f)
            {
                int totalWidth = 0;
                int maxHeight = 0;
                int done = 0;

                FontBakerCharacterRange baker = allbakers[f];

                int charsStart = baker.Start;
                int charsCount = baker.End;

                for (int i = charsStart; i < charsCount; ++i)
                {
                    char c = (char)i;

                    if (c == '\r')
                    {
                        continue;
                    }

                    if ((table[c] == 0 || table[c] == 0xFF_FF_FF_FF) && c != ' ')
                    {
                        continue;
                    }

                    byte* data = (byte*)((IntPtr)table + (int)table[c]);

                    int offX = (sbyte)data[0] + 1;
                    int offY = (sbyte)data[1];
                    int dw = data[2] + offX + 4;
                    int dh = data[3] + offY;

                    // TODO: add offsets?
                    totalWidth += dw;

                    if (maxHeight < dh)
                    {
                        maxHeight = dh;
                    }
                }


                int textureWidth = totalWidth;
                int textureHeight = maxHeight;

                if (textureWidth == 0 || textureHeight == 0)
                {
                    _backerTextures.Add(null);

                    continue;
                }

                MaxHeight = maxHeight;

                uint[] buffer = new uint[textureWidth * textureHeight];

                int lineOffY = 0;
                int w = 0;

                Point offsetFromFlag = Point.Zero;

                if (isBlackBorder)
                {
                    ++offsetFromFlag.X;
                    ++offsetFromFlag.Y;
                }

                if (isItalic)
                {
                    offsetFromFlag.X += 3;
                }

                if (isSolid)
                {
                    ++offsetFromFlag.X;
                    ++offsetFromFlag.Y;
                }

                if (isUnderline)
                {
                    ++offsetFromFlag.Y;
                }

                for (int i = charsStart; i < charsCount; ++i)
                {
                    char c = (char)i;

                    if (c == '\r' || (table[c] == 0 || table[c] == 0xFF_FF_FF_FF) && c != ' ')
                    {
                        continue;
                    }

                    int tmpW = w;
                    byte* data = (byte*)((IntPtr)table + (int)table[c]);

                    int offX = 0;
                    int offY = 0;
                    int dw = 0;
                    int dh = 0;

                    if (c != ' ')
                    {
                        offX = (sbyte)data[0] + 1;
                        offY = (sbyte)data[1];
                        dw = data[2];
                        dh = data[3];

                        data += 4;

                        _sizes[c] = new Rectangle(w, 0, dw + offX + offsetFromFlag.X, dh + offY + offsetFromFlag.Y);

                        int scanlineCount = ((dw - 1) >> 3) + 1;

                        for (int y = 0; y < dh; ++y)
                        {
                            int testY = offY + lineOffY + y;

                            if (testY < 0)
                            {
                                testY = 0;
                            }

                            if (testY >= textureHeight)
                            {
                                break;
                            }

                            byte* scanlines = data;
                            data += scanlineCount;

                            int italicOffset = 0;

                            if (isItalic)
                            {
                                italicOffset = (int)((dh - y) / ITALIC_FONT_KOEFFICIENT);
                            }

                            int testX = w + offX + italicOffset + (isSolid ? 1 : 0);

                            for (int j = 0; j < scanlineCount; ++j)
                            {
                                int coeff = j << 3;

                                for (int z = 0; z < 8; ++z)
                                {
                                    int x = coeff + z;

                                    if (x >= dw)
                                    {
                                        break;
                                    }

                                    int nowX = testX + x;

                                    if (nowX >= textureWidth)
                                    {
                                        break;
                                    }

                                    byte cl = (byte)(scanlines[j] & (1 << (7 - z)));
                                    int block = testY * textureWidth + nowX;

                                    if (cl != 0)
                                    {
                                        buffer[block] = DEFAULT_HUE;
                                    }
                                }
                            }
                        }

                        if (isSolid)
                        {
                            uint solidColor = UO_BLACK;

                            if (solidColor == DEFAULT_HUE)
                            {
                                solidColor++;
                            }

                            int minXOk = w + offX > 0 ? -1 : 0;
                            int maxXOk = w + offX + dw < textureWidth ? 1 : 0;
                            maxXOk += dw;

                            for (int cy = 0; cy < dh; cy++)
                            {
                                int testY = offY + lineOffY + cy;

                                if (testY >= textureHeight)
                                {
                                    break;
                                }

                                if (testY < 0)
                                {
                                    testY = 0;
                                }

                                int italicOffset = 0;

                                if (isItalic && cy < dh)
                                {
                                    italicOffset = (int)((dh - cy) / ITALIC_FONT_KOEFFICIENT);
                                }

                                for (int cx = minXOk; cx < maxXOk; cx++)
                                {
                                    int testX = cx + w + offX + italicOffset;

                                    if (testX >= textureWidth)
                                    {
                                        break;
                                    }

                                    int block = testY * textureWidth + testX;

                                    if (buffer[block] == 0 && buffer[block] != solidColor)
                                    {
                                        int endX = cx < dw ? 2 : 1;

                                        if (endX == 2 && testX + 1 >= textureWidth)
                                        {
                                            endX--;
                                        }

                                        for (int x = 0; x < endX; x++)
                                        {
                                            int nowX = testX + x;
                                            int testBlock = testY * textureWidth + nowX;

                                            if (buffer[testBlock] != 0 && buffer[testBlock] != solidColor)
                                            {
                                                buffer[block] = solidColor;

                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            for (int cy = 0; cy < dh; cy++)
                            {
                                int testY = offY + lineOffY + cy;

                                if (testY >= textureHeight)
                                {
                                    break;
                                }

                                if (testY < 0)
                                {
                                    testY = 0;
                                }

                                int italicOffset = 0;

                                if (isItalic)
                                {
                                    italicOffset = (int)((dh - cy) / ITALIC_FONT_KOEFFICIENT);
                                }

                                for (int cx = 0; cx < dw; cx++)
                                {
                                    int testX = cx + w + offX + italicOffset;

                                    if (testX >= textureWidth)
                                    {
                                        break;
                                    }

                                    int block = testY * textureWidth + testX;

                                    if (buffer[block] == solidColor)
                                    {
                                        buffer[block] = DEFAULT_HUE;
                                    }
                                }
                            }
                        }

                        if (isBlackBorder)
                        {
                            int minXOk = w + offX > 0 ? -1 : 0;
                            int minYOk = offY > 0 ? -1 : 0;
                            int maxXOk = w + offX + dw < textureWidth ? 1 : 0;
                            int maxYOk = offY + lineOffY + dh < textureHeight ? 1 : 0;
                            maxXOk += dw;
                            maxYOk += dh;

                            for (int cy = minYOk; cy < maxYOk; cy++)
                            {
                                int testY = offY + cy;

                                if (testY < 0)
                                {
                                    testY = 0;
                                }

                                if (testY >= textureHeight)
                                {
                                    break;
                                }

                                int italicOffset = 0;

                                if (isItalic && cy >= 0 && cy < dh)
                                {
                                    italicOffset = (int)((dh - cy) / ITALIC_FONT_KOEFFICIENT);
                                }

                                for (int cx = minXOk; cx < maxXOk; cx++)
                                {
                                    int testX = cx + w + offX + italicOffset;

                                    if (testX >= textureWidth)
                                    {
                                        break;
                                    }

                                    int block = testY * textureWidth + testX;

                                    if (buffer[block] == 0 && buffer[block] != UO_BLACK)
                                    {
                                        int startX = cx > 0 ? -1 : 0;
                                        int startY = cy > 0 ? -1 : 0;
                                        int endX = cx < dw - 1 ? 2 : 1;
                                        int endY = cy < dh - 1 ? 2 : 1;

                                        if (endX == 2 && testX + 1 >= textureWidth)
                                        {
                                            endX--;
                                        }

                                        bool passed = false;

                                        for (int x = startX; x < endX; x++)
                                        {
                                            int nowX = testX + x;

                                            for (int y = startY; y < endY; y++)
                                            {
                                                int testBlock = (testY + y) * textureWidth + nowX;

                                                if (testBlock < 0)
                                                {
                                                    continue;
                                                }

                                                if (testBlock < buffer.Length && buffer[testBlock] != 0 && buffer[testBlock] != UO_BLACK)
                                                {
                                                    buffer[block] = UO_BLACK;
                                                    passed = true;

                                                    break;
                                                }
                                            }

                                            if (passed)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        w += dw + offX + (isSolid ? 1 : 0) + 4;
                    }
                    else
                    {
                        dw = UNICODE_SPACE_WIDTH;
                        dh = MaxHeight;
                        w += UNICODE_SPACE_WIDTH;
                    }

                    if (isUnderline)
                    {
                        int minXOk = tmpW + offX > 0 ? -1 : 0;
                        int maxXOk = w + offX + dw < textureWidth ? 1 : 0;
                        byte* aData = (byte*)((IntPtr)table + (int)table[(byte)'a']);
                        int testY = lineOffY + (sbyte)aData[1] + (sbyte)aData[3];

                        if (testY >= textureHeight)
                        {
                            break;
                        }

                        if (testY < 0)
                        {
                            testY = 0;
                        }

                        for (int cx = minXOk; cx < dw + maxXOk; cx++)
                        {
                            int testX = cx + tmpW + offX + (isSolid ? 1 : 0);

                            if (testX >= textureWidth)
                            {
                                break;
                            }

                            int block = testY * textureWidth + testX;
                            buffer[block] = DEFAULT_HUE;
                        }
                    }
                }

                Texture2D texture = new Texture2D(device, textureWidth, textureHeight);
                texture.SetData(buffer);

                _backerTextures.Add(texture);
            }


            if (!_sizes.ContainsKey(' '))
            {
                _sizes[' '] = new Rectangle(0, 0, UNICODE_SPACE_WIDTH, MaxHeight + (isUnderline ? 1 : 0));
            }
        }
    }

    unsafe class AsciiFont : BaseUOFont
    {
        public AsciiFont(GraphicsDevice device, UOFile file, byte font, FontSettings settings)
            : base(device, file, font, settings)
        {

        }

        protected override unsafe void CreateTextureAtlas(GraphicsDevice device, UOFile file, FontSettings settings, byte fontIndex)
        {
            int charsCount = settings.CharsCount;

            CharacterInfo* infos = stackalloc CharacterInfo[charsCount];
            int totalWidth = 0;
            int maxHeight = 0;

            byte header = file.ReadByte();

            for (int i = 0; i < charsCount; ++i)
            {
                if (file.Position + 3 >= file.Length)
                {
                    continue;
                }

                ref CharacterInfo info = ref infos[i];
                info.Width = file.ReadByte();
                info.Height = file.ReadByte();

                totalWidth += info.Width;

                if (maxHeight < info.Height)
                {
                    maxHeight = info.Height;
                }

                file.Skip(1);

                info.Data = (ushort*)file.PositionAddress;

                file.Skip(info.Width * info.Height * sizeof(ushort));
            }


            int textureWidth = totalWidth;
            int textureHeight = maxHeight;

            MaxHeight = maxHeight;

            //if (totalWidth > MAX_WIDTH)
            //{
            //    textureWidth = MAX_WIDTH;

            //    textureHeight = maxHeight * ((totalWidth / MAX_WIDTH) + 1);
            //}

            uint* buffer = stackalloc uint[textureWidth * textureHeight];

            int lineOffY = 0;
            int w = 0;

            for (int j = 0; j < charsCount; ++j)
            {
                char c = (char)(j);

                int offsY = GetFontOffsetY((byte)fontIndex, (byte)c);

                ref CharacterInfo info1 = ref infos[GetASCIIIndex(c)];

                int dw = info1.Width;
                int dh = info1.Height;

                _sizes[c] = new Rectangle(w, lineOffY, dw, dh);

                for (int y = 0; y < dh; ++y)
                {
                    int testY = y + lineOffY + offsY;

                    if (testY >= textureHeight)
                    {
                        break;
                    }

                    for (int x = 0; x < dw; ++x)
                    {
                        if (x + w >= textureWidth)
                        {
                            lineOffY += maxHeight;
                            w = 0;

                            break;
                        }

                        ushort uc = ((ushort*)info1.Data)[y * dw + x];

                        if (uc != 0)
                        {
                            var color = HuesHelper.Color16To32(uc) | 0xFF_00_00_00;

                            int block = testY * textureWidth + x + w;

                            if (block >= 0)
                            {
                                buffer[block] = color;
                            }
                        }
                    }
                }

                w += dw;
            }

            if (_atlasTexture != null && !_atlasTexture.IsDisposed)
            {
                _atlasTexture.Dispose();
            }

            _atlasTexture = new Texture2D(device, textureWidth, textureHeight);
            _atlasTexture.SetDataPointerEXT(0, null, (IntPtr)buffer, textureWidth * textureHeight * sizeof(uint));
        }




        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetASCIIIndex(char c)
        {
            const byte NOPRINT_CHARS = 32;

            byte ch = (byte)c; // ASCII fonts cover only 256 characters

            if (ch < NOPRINT_CHARS)
            {
                return 0;
            }

            return ch - NOPRINT_CHARS;
        }

        private static readonly int[] _offsetCharTable =
        {
            2, 0, 2, 2, 0, 0, 2, 2, 0, 0
        };
        private static readonly int[] _offsetSymbolTable =
        {
            1, 0, 1, 1, -1, 0, 1, 1, 0, 0
        };

        private static int GetFontOffsetY(byte font, byte index)
        {
            if (index == 0xB8)
            {
                return 1;
            }

            if (!(index >= 0x41 && index <= 0x5A) && !(index >= 0xC0 && index <= 0xDF) && index != 0xA8)
            {
                if (font < 10)
                {
                    if (index >= 0x61 && index <= 0x7A)
                    {
                        return _offsetCharTable[font];
                    }

                    return _offsetSymbolTable[font];
                }

                return 2;
            }

            return 0;
        }


    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe ref struct CharacterInfo
    {
        public sbyte OffsetX, OffsetY;
        public byte Width, Height;
        public void* Data;
    }

    struct FontSettings
    {
        public int CharsCount;
        public bool Bold;
        public bool Italic;
        public bool Underline;
        public bool Border;
    }

    abstract class BaseUOFont : IDisposable
    {
        protected readonly Dictionary<char, Rectangle> _sizes = new Dictionary<char, Rectangle>();
        protected Texture2D _atlasTexture;

        protected BaseUOFont(GraphicsDevice device, UOFile file, byte font, FontSettings settings)
        {
            Font = font;
            CharsCount = settings.CharsCount;

            CreateTextureAtlas(device, file, settings, font);
        }



        public byte Font { get; }

        public int CharsCount { get; }

        public int MaxHeight { get; protected set; }

        public bool IsDisposed => _atlasTexture?.IsDisposed == true;



        public virtual Texture2D GetTextureByChar(char c)
        {
            return _atlasTexture;
        }

        public Rectangle GetCharBounds(char c)
        {
            _sizes.TryGetValue(c, out Rectangle rect);

            return rect;
        }

        public Vector2 GetCharSize(char c)
        {
            Vector2 size = Vector2.Zero;

            if (_sizes.TryGetValue(c, out Rectangle rect))
            {
                size.X = rect.X;
                size.Y = rect.Y;
            }

            return size;
        }

        public Vector2 MeasureString(string text)
        {
            Vector2 size = Vector2.Zero;

            if (string.IsNullOrEmpty(text))
            {
                return size;
            }

            size.Y = MaxHeight;

            Rectangle rect = Rectangle.Empty;
            float lineWith = 0;

            if (!_sizes.TryGetValue('?', out Rectangle invalidRect))
            {
                invalidRect = _sizes[' '];
            }

            for (int i = 0; i < text.Length; ++i)
            {
                char c = text[i];

                if (c == '\r')
                {
                    continue;
                }

                if (c == '\n')
                {
                    size.Y += MaxHeight;

                    if (lineWith > size.X)
                    {
                        size.X = lineWith;
                    }

                    lineWith = 0;

                    continue;
                }

                if (!_sizes.TryGetValue(c, out rect))
                {
                    rect = invalidRect;
                }

                lineWith += rect.Width;
            }

            return size;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                _atlasTexture.Dispose();
                _atlasTexture = null;
            }
        }




        protected abstract void CreateTextureAtlas(GraphicsDevice device, UOFile file, FontSettings settings, byte fontIndex);
    }
}
