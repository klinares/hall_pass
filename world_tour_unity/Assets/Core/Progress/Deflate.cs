using System;
using System.Collections.Generic;
using System.IO;

namespace WorldTour.Core.Progress
{
    /// <summary>
    /// Raw DEFLATE (RFC 1951) in plain C#, for the export code. The web player can't be relied on to have
    /// System.IO.Compression's native zlib, so this needs nothing from the platform. Compress writes one block
    /// with the fixed Huffman codes (LZ77, 32 KB window); Inflate reads any raw DEFLATE stream (stored, fixed
    /// and dynamic blocks), so any zlib can read and write codes too. Decoding is bounded and throws
    /// <see cref="InvalidDataException"/> on a damaged stream.
    /// </summary>
    public static class Deflate
    {
        private static readonly int[] LenBase = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };
        private static readonly int[] LenExtra = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
        private static readonly int[] DistBase = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };
        private static readonly int[] DistExtra = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };
        private static readonly int[] ClOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };

        private const int Window = 32768;
        private const int MinMatch = 3;
        private const int MaxMatch = 258;
        private const int MaxChain = 128;

        // ---------- compress ----------

        /// <summary>Compresses to a raw DEFLATE stream (no zlib or gzip header).</summary>
        public static byte[] Compress(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var w = new BitWriter();
            w.Bits(1, 1);          // BFINAL
            w.Bits(1, 2);          // BTYPE = 01, fixed Huffman
            var head = new int[1 << 15];
            var prev = new int[Window];
            for (int i = 0; i < head.Length; i++) head[i] = -1;
            int pos = 0, n = data.Length;
            while (pos < n)
            {
                int bestLen = 0, bestDist = 0;
                if (pos + MinMatch <= n)
                {
                    int h = Hash(data, pos);
                    int cand = head[h];
                    int chain = 0;
                    while (cand >= 0 && pos - cand <= Window && chain++ < MaxChain)
                    {
                        int len = 0, max = Math.Min(MaxMatch, n - pos);
                        while (len < max && data[cand + len] == data[pos + len]) len++;
                        if (len > bestLen) { bestLen = len; bestDist = pos - cand; if (len == max) break; }
                        cand = prev[cand % Window];
                    }
                }
                if (bestLen >= MinMatch)
                {
                    WriteLength(w, bestLen);
                    WriteDistance(w, bestDist);
                    for (int k = 0; k < bestLen; k++) Insert(data, pos + k, n, head, prev);
                    pos += bestLen;
                }
                else
                {
                    WriteLit(w, data[pos]);
                    Insert(data, pos, n, head, prev);
                    pos++;
                }
            }
            WriteLit(w, 256);      // end of block
            return w.ToArray();
        }

        private static int Hash(byte[] d, int p)
        {
            return ((d[p] << 10) ^ (d[p + 1] << 5) ^ d[p + 2]) & 0x7fff;
        }

        private static void Insert(byte[] d, int p, int n, int[] head, int[] prev)
        {
            if (p + MinMatch > n) return;
            int h = Hash(d, p);
            prev[p % Window] = head[h];
            head[h] = p;
        }

        /// <summary>A literal/length symbol in the fixed code (RFC 1951 3.2.6).</summary>
        private static void WriteLit(BitWriter w, int sym)
        {
            if (sym < 144) w.Code(0x30 + sym, 8);
            else if (sym < 256) w.Code(0x190 + sym - 144, 9);
            else if (sym < 280) w.Code(sym - 256, 7);
            else w.Code(0xc0 + sym - 280, 8);
        }

        private static void WriteLength(BitWriter w, int len)
        {
            int i = LenBase.Length - 1;
            while (LenBase[i] > len) i--;
            WriteLit(w, 257 + i);
            if (LenExtra[i] > 0) w.Bits(len - LenBase[i], LenExtra[i]);
        }

        private static void WriteDistance(BitWriter w, int dist)
        {
            int i = DistBase.Length - 1;
            while (DistBase[i] > dist) i--;
            w.Code(i, 5);
            if (DistExtra[i] > 0) w.Bits(dist - DistBase[i], DistExtra[i]);
        }

        private sealed class BitWriter
        {
            private readonly List<byte> _out = new List<byte>();
            private int _acc, _n;

            /// <summary>Value bits, least significant first (header fields and extra bits).</summary>
            public void Bits(int value, int count)
            {
                for (int i = 0; i < count; i++) Bit((value >> i) & 1);
            }

            /// <summary>A Huffman code, most significant bit first.</summary>
            public void Code(int code, int len)
            {
                for (int i = len - 1; i >= 0; i--) Bit((code >> i) & 1);
            }

            private void Bit(int b)
            {
                _acc |= b << _n;
                if (++_n == 8) { _out.Add((byte)_acc); _acc = 0; _n = 0; }
            }

            public byte[] ToArray()
            {
                if (_n > 0) { _out.Add((byte)_acc); _acc = 0; _n = 0; }
                return _out.ToArray();
            }
        }

        // ---------- inflate ----------

        /// <summary>Decompresses a raw DEFLATE stream. Throws InvalidDataException on damage or past <paramref name="maxOutput"/> bytes.</summary>
        public static byte[] Inflate(byte[] data, int maxOutput = 8 * 1024 * 1024)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var r = new BitReader(data);
            var outBuf = new List<byte>();
            bool last;
            do
            {
                last = r.Bits(1) == 1;
                int type = r.Bits(2);
                if (type == 0) Stored(r, outBuf, maxOutput);
                else if (type == 1) Codes(r, outBuf, FixedLit, FixedDist, maxOutput);
                else if (type == 2)
                {
                    Huffman lit, dist;
                    Dynamic(r, out lit, out dist);
                    Codes(r, outBuf, lit, dist, maxOutput);
                }
                else throw new InvalidDataException("bad block type");
            } while (!last);
            return outBuf.ToArray();
        }

        private static void Stored(BitReader r, List<byte> outBuf, int max)
        {
            r.Align();
            int len = r.Byte() | (r.Byte() << 8);
            int nlen = r.Byte() | (r.Byte() << 8);
            if ((len ^ 0xffff) != nlen) throw new InvalidDataException("bad stored block");
            if (outBuf.Count + len > max) throw new InvalidDataException("too large");
            for (int i = 0; i < len; i++) outBuf.Add((byte)r.Byte());
        }

        private static void Codes(BitReader r, List<byte> outBuf, Huffman lit, Huffman dist, int max)
        {
            while (true)
            {
                int sym = lit.Decode(r);
                if (sym < 256)
                {
                    if (outBuf.Count >= max) throw new InvalidDataException("too large");
                    outBuf.Add((byte)sym);
                }
                else if (sym == 256) return;
                else
                {
                    sym -= 257;
                    if (sym >= 29) throw new InvalidDataException("bad length code");
                    int len = LenBase[sym] + r.Bits(LenExtra[sym]);
                    int ds = dist.Decode(r);
                    if (ds >= 30) throw new InvalidDataException("bad distance code");
                    int d = DistBase[ds] + r.Bits(DistExtra[ds]);
                    if (d > outBuf.Count) throw new InvalidDataException("distance too far back");
                    if (outBuf.Count + len > max) throw new InvalidDataException("too large");
                    int from = outBuf.Count - d;
                    for (int i = 0; i < len; i++) outBuf.Add(outBuf[from + i]);
                }
            }
        }

        private static void Dynamic(BitReader r, out Huffman lit, out Huffman dist)
        {
            int nlen = r.Bits(5) + 257, ndist = r.Bits(5) + 1, ncode = r.Bits(4) + 4;
            if (nlen > 286 || ndist > 30) throw new InvalidDataException("bad counts");
            var lengths = new int[19];
            for (int i = 0; i < ncode; i++) lengths[ClOrder[i]] = r.Bits(3);
            var cl = new Huffman(lengths, 19);
            var all = new int[nlen + ndist];
            int idx = 0;
            while (idx < nlen + ndist)
            {
                int sym = cl.Decode(r);
                if (sym < 16) { all[idx++] = sym; continue; }
                int rep, val = 0;
                if (sym == 16)
                {
                    if (idx == 0) throw new InvalidDataException("repeat with no length");
                    val = all[idx - 1];
                    rep = 3 + r.Bits(2);
                }
                else if (sym == 17) rep = 3 + r.Bits(3);
                else rep = 11 + r.Bits(7);
                if (idx + rep > nlen + ndist) throw new InvalidDataException("too many lengths");
                while (rep-- > 0) all[idx++] = val;
            }
            if (all[256] == 0) throw new InvalidDataException("no end-of-block code");
            var litLens = new int[nlen];
            Array.Copy(all, litLens, nlen);
            var distLens = new int[ndist];
            Array.Copy(all, nlen, distLens, 0, ndist);
            lit = new Huffman(litLens, nlen);
            dist = new Huffman(distLens, ndist);
        }

        private static readonly Huffman FixedLit = MakeFixedLit();
        private static readonly Huffman FixedDist = new Huffman(Fill(30, 5), 30);

        private static Huffman MakeFixedLit()
        {
            var l = new int[288];
            for (int i = 0; i < 144; i++) l[i] = 8;
            for (int i = 144; i < 256; i++) l[i] = 9;
            for (int i = 256; i < 280; i++) l[i] = 7;
            for (int i = 280; i < 288; i++) l[i] = 8;
            return new Huffman(l, 288);
        }

        private static int[] Fill(int n, int v)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = v;
            return a;
        }

        /// <summary>A canonical Huffman decoder (counts per length plus symbols in code order), decoded bit by bit.</summary>
        private sealed class Huffman
        {
            private readonly int[] _count = new int[16];
            private readonly int[] _symbol;

            public Huffman(int[] lengths, int n)
            {
                _symbol = new int[n];
                for (int s = 0; s < n; s++) _count[lengths[s]]++;
                _count[0] = 0;
                int left = 1;
                for (int len = 1; len < 16; len++)
                {
                    left <<= 1;
                    left -= _count[len];
                    if (left < 0) throw new InvalidDataException("over-subscribed code");
                }
                var offs = new int[16];
                for (int len = 1; len < 15; len++) offs[len + 1] = offs[len] + _count[len];
                for (int s = 0; s < n; s++) if (lengths[s] != 0) _symbol[offs[lengths[s]]++] = s;
            }

            public int Decode(BitReader r)
            {
                int code = 0, first = 0, index = 0;
                for (int len = 1; len < 16; len++)
                {
                    code |= r.Bits(1);
                    int count = _count[len];
                    if (code - count < first) return _symbol[index + (code - first)];
                    index += count;
                    first += count;
                    first <<= 1;
                    code <<= 1;
                }
                throw new InvalidDataException("bad code");
            }
        }

        private sealed class BitReader
        {
            private readonly byte[] _d;
            private int _pos, _bit;

            public BitReader(byte[] d) { _d = d; }

            public int Bits(int count)
            {
                int v = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_pos >= _d.Length) throw new InvalidDataException("unexpected end of data");
                    v |= ((_d[_pos] >> _bit) & 1) << i;
                    if (++_bit == 8) { _bit = 0; _pos++; }
                }
                return v;
            }

            public void Align()
            {
                if (_bit != 0) { _bit = 0; _pos++; }
            }

            public int Byte()
            {
                if (_pos >= _d.Length) throw new InvalidDataException("unexpected end of data");
                return _d[_pos++];
            }
        }

        // ---------- checksum ----------

        private static readonly uint[] CrcTable = MakeCrc();

        private static uint[] MakeCrc()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        /// <summary>CRC-32 (the zlib and gzip one).</summary>
        public static uint Crc32(byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            foreach (byte b in data) c = CrcTable[(c ^ b) & 0xff] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
