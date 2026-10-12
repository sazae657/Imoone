using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Niraction.Internal;

/// <summary>
/// ﾆﾗｸｼｮﾝ互換圧縮&伸張 (niraction_ctl_datum.cppから移植)
/// </summary>
internal sealed class RleBSV
{
    const int WindowSize = 4096;
    const int WindowMask = WindowSize - 1;
    const int MinMatchLength = 3;
    const int MaxMatchLength = 17;
    const int ChunkSize = 64 * 1024;
    const int BufferCapacity = WindowSize + ChunkSize;
    const int HashBits = 15;
    const int HashSize = 1 << HashBits;
    const int PacketSize = 64 * 1024;

    /// <summary>
    /// 圧縮
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Compress(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));
        ArgumentNullException.ThrowIfNull(output, nameof(output));

        var writer = new BeitWriter(output);
        var buffer = new byte[BufferCapacity];
        int bss = 0;
        int cursor = 0;
        long basePos = 0; 
        bool ending = false;

        var head = new long[HashSize];
        var prev = new long[WindowSize];
        Array.Fill(head, -1L);

        while (true) {
            while (!ending && (bss - cursor < MaxMatchLength + MinMatchLength)) {
                // ｳｨﾝﾄﾞｳｻｲｽﾞを超えて進んだら、未処理領域と直前4KBを残してﾊﾞｯﾌｧ先頭へｼﾌﾄ
                if (cursor >= WindowSize) {
                    int wps = cursor - WindowSize;
                    int bytesToKeep = bss - wps;
                    Buffer.BlockCopy(buffer, wps, buffer, 0, bytesToKeep);
                    cursor -= wps;
                    bss = bytesToKeep;
                    basePos += wps;
                }

                int bytesToRead = buffer.Length - bss;
                int read = input.Read(buffer, bss, bytesToRead);
                if (read == 0) {
                    ending = true;
                }
                else {
                    bss += read;
                }
            }

            if (cursor >= bss) {
                break; // 全ﾃﾞーﾀ処理おわり
            }

            int lookaheadLen = Math.Min(MaxMatchLength, bss - cursor);
            int bestMatchLen = 0;
            int bestDistance = 0;

            if (lookaheadLen >= MinMatchLength) {
                long absCursor = basePos + cursor;
                long windowStart = absCursor - WindowSize;
                long candidate = head[Hash(buffer, cursor)];

                // 同一ﾊｯｼｭの位置を新しい順に辿り、ｳｨﾝﾄﾞｳ内の最長一致を探索
                while (candidate >= 0 && candidate >= windowStart) {
                    int i = (int)(candidate - basePos);

                    // 現在の最長一致の末尾が一致しなければ更新できないので先に判定
                    if (buffer[i + bestMatchLen] == buffer[cursor + bestMatchLen]) {
                        int match = 0;
                        while (match < lookaheadLen && buffer[i + match] == buffer[cursor + match]) {
                            match++;
                        }

                        if (match > bestMatchLen) {
                            bestMatchLen = match;
                            bestDistance = cursor - i;
                            if (bestMatchLen == lookaheadLen) {
                                break; // 最大一致長に達したため探索終了
                            }
                        }
                    }

                    candidate = prev[(int)(candidate & WindowMask)];
                }
            }

            int advance;
            if (bestMatchLen >= MinMatchLength) {
                writer.WriteMatch(bestDistance, bestMatchLen);
                advance = bestMatchLen;
            }
            else {
                writer.WriteLiteral(buffer[cursor]);
                advance = 1;
            }

            // 通過した位置をﾊｯｼｭﾁｪーﾝに登録
            int insertEnd = Math.Min(cursor + advance, bss - (MinMatchLength - 1));
            for (int p = cursor; p < insertEnd; p++) {
                int h = Hash(buffer, p);
                long abs = basePos + p;
                prev[(int)(abs & WindowMask)] = head[h];
                head[h] = abs;
            }

            cursor += advance;
        }

        writer.WriteEof();
        writer.Flush();
    }


    /// <summary>
    /// 先頭3ﾊﾞｲﾄからﾊｯｼｭ計算
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="pos"></param>
    /// <returns></returns>
    private static int Hash(byte[] buffer, int pos)
    {
        uint v = (uint)(buffer[pos] | (buffer[pos + 1] << 8) | (buffer[pos + 2] << 16));
        return (int)((v * 2654435761u) >> (32 - HashBits));
    }


    /// <summary>
    /// 伸張
    /// </summary>
    /// <param name="input"></param>
    /// <param name="output"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidDataException"></exception>
    public static void Decompress(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));
        ArgumentNullException.ThrowIfNull(output, nameof(output));

        var reader = new BeitReader(input);
        var ringBuffer = new byte[WindowSize];
        int ringPos = 0;
        var outBuf = new byte[PacketSize];
        int outZise = 0;

        while (true) {
            int flagRead = reader.ReadByte();
            if (flagRead == -1) {
                break;
            }

            byte flag = (byte)flagRead;

            for (int bit = 0; bit < 8; bit++) {
                bool isLiteral = (flag & (1 << bit)) != 0;

                if (isLiteral) {
                    int b = reader.ReadByte();
                    if (b == -1) {
                        output.Write(outBuf, 0, outZise);
                        throw new InvalidDataException(Properties.Resources.NIR_E_LiteralCorrupted);
                    }

                    if (outZise == outBuf.Length) {
                        output.Write(outBuf, 0, outZise);
                        outZise = 0;
                    }
                    outBuf[outZise++] = (byte)b;
                    ringBuffer[ringPos] = (byte)b;
                    ringPos = (ringPos + 1) & WindowMask;
                }
                else {
                    int b0 = reader.ReadByte();
                    int b1 = reader.ReadByte();
                    if (b0 == -1 || b1 == -1) {
                        output.Write(outBuf, 0, outZise);
                        throw new InvalidDataException(Properties.Resources.NIR_E_TokenCorrupted);
                    }

                    int offset = b0 | ((b1 & 0x0F) << 8);
                    int lenCode = (b1 >> 4) & 0x0F;

                    // lenCode == 0 は終端ﾏーｶー (EOF)
                    if (lenCode == 0) {
                        output.Write(outBuf, 0, outZise);
                        reader.Rewind();
                        return;
                    }

                    int distance = offset + 1;
                    int length = lenCode + 2;

                    if (outBuf.Length - outZise < length) {
                        output.Write(outBuf, 0, outZise);
                        outZise = 0;
                    }

                    int readPos = (ringPos - distance) & WindowMask;
                    for (int k = 0; k < length; k++) {
                        byte b = ringBuffer[readPos];
                        outBuf[outZise++] = b;
                        ringBuffer[ringPos] = b;
                        ringPos = (ringPos + 1) & WindowMask;
                        readPos = (readPos + 1) & WindowMask;
                    }
                }
            }
        }

        output.Write(outBuf, 0, outZise);
    }

    /// <summary>
    /// ﾊﾞｯﾌｧー付き読み込みﾏﾝ
    /// </summary>
    private sealed class BeitReader
    {
        readonly Stream input;
        readonly byte[] buffer = new byte[PacketSize];
        int position;
        int lengthOf;

        public BeitReader(Stream input) => this.input = input;

        /// <summary>
        /// 読み込み
        /// </summary>
        /// <returns></returns>
        public int ReadByte()
        {
            if (position == lengthOf) {
                lengthOf = input.Read(buffer, 0, buffer.Length);
                position = 0;
                if (lengthOf == 0) {
                    return -1;
                }
            }
            return buffer[position++];
        }

        /// <summary>
        /// 先読みしすぎた分だけｽﾄﾘーﾑ位置を戻す
        /// </summary>
        public void Rewind()
        {
            int unread = lengthOf - position;
            if (unread > 0 && input.CanSeek) {
                input.Seek(-unread, SeekOrigin.Current);
            }
            position = lengthOf = 0;
        }
    }

    /// <summary>
    /// 書き込みﾏﾝ
    /// </summary>
    private sealed class BeitWriter
    {
        private readonly Stream outlet;
        private readonly byte[] buffer = new byte[PacketSize];
        private int lengthOf;
        private int flagPos = -1;
        private int bitCount;

        public BeitWriter(Stream output) => outlet = output;

        /// <summary>
        /// 新しいﾌﾗｸﾞﾌﾞﾛｯｸを開始
        /// </summary>
        private void BeginBlock()
        {
            // ﾌﾗｸﾞ1ﾊﾞｲﾄ+ﾄーｸﾝ最大16ﾊﾞｲﾄ分の空きを確保)
            if (buffer.Length - lengthOf < 1 + 16) {
                outlet.Write(buffer, 0, lengthOf);
                lengthOf = 0;
            }
            flagPos = lengthOf;
            buffer[lengthOf++] = 0;
        }

        /// <summary>
        /// かきこみ
        /// </summary>
        /// <param name="b"></param>
        public void WriteLiteral(byte b)
        {
            if (bitCount == 0) BeginBlock();
            buffer[flagPos] |= (byte)(1 << bitCount);
            buffer[lengthOf++] = b;
            if (++bitCount == 8) bitCount = 0;
        }

        /// <summary>
        /// かきこみ
        /// </summary>
        /// <param name="distance"></param>
        /// <param name="length"></param>
        public void WriteMatch(int distance, int length)
        {
            if (bitCount == 0) BeginBlock();
            int offset = distance - 1;
            int lenCode = (length == 0) ? 0 : (length - 2);

            // 16bit = 12bit(offset) + 4bit(lenCode)
            buffer[lengthOf++] = (byte)(offset & 0xFF);
            buffer[lengthOf++] = (byte)(((offset >> 8) & 0x0F) | (lenCode << 4));
            if (++bitCount == 8) bitCount = 0;
        }

        /// <summary>
        /// EOF書き込み
        /// </summary>
        public void WriteEof()
        {
            WriteMatch(1, 0);
        }

        /// <summary>
        /// ﾌﾗｯｼｭ！
        /// </summary>
        public void Flush()
        {
            if (lengthOf > 0) {
                outlet.Write(buffer, 0, lengthOf);
                lengthOf = 0;
            }
            bitCount = 0;
        }
    }
}
