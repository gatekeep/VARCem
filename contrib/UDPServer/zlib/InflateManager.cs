/**
 * UDP-socket Network Server
 * INTERNAL/PROPRIETARY/CONFIDENTIAL. Use is subject to license terms.
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 * 
 * Author:	Bryan Biedenkapp, <gatekeep@gmail.com>
 * Copyright 2018 Bryan Biedenkapp
 */
// ------------------------------------------------------------------
//
// Copyright (c) 2009 Dino Chiesa and Microsoft Corporation.  
// All rights reserved.
//
// This code is derivied from DotNetZip, a zipfile class library.
//
// -----------------------------------------------------------------------
//
// Copyright (c) 2000,2001,2002,2003 ymnk, JCraft,Inc. All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in
// the documentation and/or other materials provided with the distribution.
//
// 3. The names of the authors may not be used to endorse or promote products
// derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED ``AS IS'' AND ANY EXPRESSED OR IMPLIED WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL JCRAFT,
// INC. OR ANY CONTRIBUTORS TO THIS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
// OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
// LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
// NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
// EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//
// -----------------------------------------------------------------------
//
// This program is based on zlib-1.1.3; credit to authors
// Jean-loup Gailly(jloup@gzip.org) and Mark Adler(madler@alumni.caltech.edu)
// and contributors of zlib.
//
// -----------------------------------------------------------------------
using System;
using System.IO;

namespace UDPServer.zlib
{
    /// <summary>
    /// Summary description for InternalInflateConstants.
    /// </summary>
    internal static class InternalInflateConstants
    {
        // Fields
        // And'ing with mask[n] masks the lower n bits
        internal static readonly int[] InflateMask = new int[] {
            0x00000000, 0x00000001, 0x00000003, 0x00000007,
            0x0000000f, 0x0000001f, 0x0000003f, 0x0000007f,
            0x000000ff, 0x000001ff, 0x000003ff, 0x000007ff,
            0x00000fff, 0x00001fff, 0x00003fff, 0x00007fff, 0x0000ffff };
    }

    /// <summary>
    /// Summary description for InflateManager.
    /// </summary>
    internal sealed class InflateManager
    {
        // Fields
        // preset dictionary flag in zlib header
        private const int PRESET_DICT = 0x20;

        private const int Z_DEFLATED = 8;

        private enum InflateManagerMode
        {
            METHOD = 0,  // waiting for method byte
            FLAG   = 1,  // waiting for flag byte
            DICT4  = 2,  // four dictionary check bytes to go
            DICT3  = 3,  // three dictionary check bytes to go
            DICT2  = 4,  // two dictionary check bytes to go
            DICT1  = 5,  // one dictionary check byte to go
            DICT0  = 6,  // waiting for inflateSetDictionary
            BLOCKS = 7,  // decompressing blocks
            CHECK4 = 8,  // four check bytes to go
            CHECK3 = 9,  // three check bytes to go
            CHECK2 = 10, // two check bytes to go
            CHECK1 = 11, // one check byte to go
            DONE   = 12, // finished check, done
            BAD    = 13, // got an error--stay here
        }

        private InflateManagerMode mode; // current inflate mode
        internal ZCodec _codec; // pointer back to this zlib stream

        // mode dependent information
        internal int method; // if FLAGS, method byte

        // if CHECK, check values to compare
        internal uint computedCheck; // computed check value
        internal uint expectedCheck; // stream check value

        // if BAD, inflateSync's marker bytes count
        internal int marker;

        // mode independent information
        //internal int nowrap; // flag for no wrapper
        private bool _handleRfc1950HeaderBytes = true;
        internal bool HandleRfc1950HeaderBytes
        {
            get { return _handleRfc1950HeaderBytes; }
            set { _handleRfc1950HeaderBytes = value; }
        }
        internal int wbits; // log2(window size)  (8..15, defaults to 15)

        internal InflateBlocks blocks; // current inflate_blocks state

        // Methods
        public InflateManager() { }

        public InflateManager(bool expectRfc1950HeaderBytes)
        {
            _handleRfc1950HeaderBytes = expectRfc1950HeaderBytes;
        }

        internal int Reset()
        {
            _codec.TotalBytesIn = _codec.TotalBytesOut = 0;
            _codec.Message = null;
            mode = HandleRfc1950HeaderBytes ? InflateManagerMode.METHOD : InflateManagerMode.BLOCKS;
            blocks.Reset();
            return ZConstants.Z_OK;
        }

        internal int End()
        {
            if (blocks != null)
                blocks.Free();
            blocks = null;
            return ZConstants.Z_OK;
        }

        internal int Initialize(ZCodec codec, int w)
        {
            _codec = codec;
            _codec.Message = null;
            blocks = null;

            // handle undocumented nowrap option (no zlib header or check)
            //nowrap = 0;
            //if (w < 0)
            //{
            //    w = - w;
            //    nowrap = 1;
            //}

            // set window size
            if (w < 8 || w > 15)
            {
                End();
                throw new IOException("Bad window size.");

                //return ZConstants.Z_STREAM_ERROR;
            }
            wbits = w;

            blocks = new InflateBlocks(codec,
                HandleRfc1950HeaderBytes ? this : null,
                1 << w);

            // reset state
            Reset();
            return ZConstants.Z_OK;
        }

        internal int Inflate(FlushType flush)
        {
            int b;

            if (_codec.InputBuffer == null)
                throw new IOException("InputBuffer is null.");

//             int f = (flush == FlushType.Finish)
//                 ? ZConstants.Z_BUF_ERROR
//                 : ZConstants.Z_OK;

            // workitem 8870
            int f = ZConstants.Z_OK;
            int r = ZConstants.Z_BUF_ERROR;

            while (true)
            {
                switch (mode)
                {
                    case InflateManagerMode.METHOD:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        if (((method = _codec.InputBuffer[_codec.NextIn++]) & 0xf) != Z_DEFLATED)
                        {
                            mode = InflateManagerMode.BAD;
                            _codec.Message = String.Format("unknown compression method (0x{0:X2})", method);
                            marker = 5; // can't try inflateSync
                            break;
                        }
                        if ((method >> 4) + 8 > wbits)
                        {
                            mode = InflateManagerMode.BAD;
                            _codec.Message = String.Format("invalid window size ({0})", (method >> 4) + 8);
                            marker = 5; // can't try inflateSync
                            break;
                        }
                        mode = InflateManagerMode.FLAG;
                        break;


                    case InflateManagerMode.FLAG:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        b = (_codec.InputBuffer[_codec.NextIn++]) & 0xff;

                        if ((((method << 8) + b) % 31) != 0)
                        {
                            mode = InflateManagerMode.BAD;
                            _codec.Message = "incorrect header check";
                            marker = 5; // can't try inflateSync
                            break;
                        }

                        mode = ((b & PRESET_DICT) == 0)
                            ? InflateManagerMode.BLOCKS
                            : InflateManagerMode.DICT4;
                        break;

                    case InflateManagerMode.DICT4:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        expectedCheck = (uint)((_codec.InputBuffer[_codec.NextIn++] << 24) & 0xff000000);
                        mode = InflateManagerMode.DICT3;
                        break;

                    case InflateManagerMode.DICT3:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        expectedCheck += (uint)((_codec.InputBuffer[_codec.NextIn++] << 16) & 0x00ff0000);
                        mode = InflateManagerMode.DICT2;
                        break;

                    case InflateManagerMode.DICT2:

                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        expectedCheck += (uint)((_codec.InputBuffer[_codec.NextIn++] << 8) & 0x0000ff00);
                        mode = InflateManagerMode.DICT1;
                        break;


                    case InflateManagerMode.DICT1:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--; _codec.TotalBytesIn++;
                        expectedCheck += (uint)(_codec.InputBuffer[_codec.NextIn++] & 0x000000ff);
                        _codec._Adler32 = expectedCheck;
                        mode = InflateManagerMode.DICT0;
                        return ZConstants.Z_NEED_DICT;


                    case InflateManagerMode.DICT0:
                        mode = InflateManagerMode.BAD;
                        _codec.Message = "need dictionary";
                        marker = 0; // can try inflateSync
                        return ZConstants.Z_STREAM_ERROR;


                    case InflateManagerMode.BLOCKS:
                        r = blocks.Process(r);
                        if (r == ZConstants.Z_DATA_ERROR)
                        {
                            mode = InflateManagerMode.BAD;
                            marker = 0; // can try inflateSync
                            break;
                        }

                        if (r == ZConstants.Z_OK) r = f;

                        if (r != ZConstants.Z_STREAM_END)
                            return r;

                        r = f;
                        computedCheck = blocks.Reset();
                        if (!HandleRfc1950HeaderBytes)
                        {
                            mode = InflateManagerMode.DONE;
                            return ZConstants.Z_STREAM_END;
                        }
                        mode = InflateManagerMode.CHECK4;
                        break;

                    case InflateManagerMode.CHECK4:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        expectedCheck = (uint)((_codec.InputBuffer[_codec.NextIn++] << 24) & 0xff000000);
                        mode = InflateManagerMode.CHECK3;
                        break;

                    case InflateManagerMode.CHECK3:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--; _codec.TotalBytesIn++;
                        expectedCheck += (uint)((_codec.InputBuffer[_codec.NextIn++] << 16) & 0x00ff0000);
                        mode = InflateManagerMode.CHECK2;
                        break;

                    case InflateManagerMode.CHECK2:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--;
                        _codec.TotalBytesIn++;
                        expectedCheck += (uint)((_codec.InputBuffer[_codec.NextIn++] << 8) & 0x0000ff00);
                        mode = InflateManagerMode.CHECK1;
                        break;

                    case InflateManagerMode.CHECK1:
                        if (_codec.AvailableBytesIn == 0) return r;
                        r = f;
                        _codec.AvailableBytesIn--; _codec.TotalBytesIn++;
                        expectedCheck += (uint)(_codec.InputBuffer[_codec.NextIn++] & 0x000000ff);
                        if (computedCheck != expectedCheck)
                        {
                            mode = InflateManagerMode.BAD;
                            _codec.Message = "incorrect data check";
                            marker = 5; // can't try inflateSync
                            break;
                        }
                        mode = InflateManagerMode.DONE;
                        return ZConstants.Z_STREAM_END;

                    case InflateManagerMode.DONE:
                        return ZConstants.Z_STREAM_END;

                    case InflateManagerMode.BAD:
                        throw new IOException(String.Format("Bad state ({0})", _codec.Message));

                    default:
                        throw new IOException("Stream error.");

                }
            }
        }

        internal int SetDictionary(byte[] dictionary)
        {
            int index = 0;
            int length = dictionary.Length;
            if (mode != InflateManagerMode.DICT0)
                throw new IOException("Stream error.");

            if (Adler.Adler32(1, dictionary, 0, dictionary.Length) != _codec._Adler32)
            {
                return ZConstants.Z_DATA_ERROR;
            }

            _codec._Adler32 = Adler.Adler32(0, null, 0, 0);

            if (length >= (1 << wbits))
            {
                length = (1 << wbits) - 1;
                index = dictionary.Length - length;
            }
            blocks.SetDictionary(dictionary, index, length);
            mode = InflateManagerMode.BLOCKS;
            return ZConstants.Z_OK;
        }

        private static readonly byte[] mark = new byte[] { 0, 0, 0xff, 0xff };

        internal int Sync()
        {
            int n; // number of bytes to look at
            int p; // pointer to bytes
            int m; // number of marker bytes found in a row
            long r, w; // temporaries to save total_in and total_out

            // set up
            if (mode != InflateManagerMode.BAD)
            {
                mode = InflateManagerMode.BAD;
                marker = 0;
            }
            if ((n = _codec.AvailableBytesIn) == 0)
                return ZConstants.Z_BUF_ERROR;
            p = _codec.NextIn;
            m = marker;

            // search
            while (n != 0 && m < 4)
            {
                if (_codec.InputBuffer[p] == mark[m])
                {
                    m++;
                }
                else if (_codec.InputBuffer[p] != 0)
                {
                    m = 0;
                }
                else
                {
                    m = 4 - m;
                }
                p++; n--;
            }

            // restore
            _codec.TotalBytesIn += p - _codec.NextIn;
            _codec.NextIn = p;
            _codec.AvailableBytesIn = n;
            marker = m;

            // return no joy or set up to restart on a new block
            if (m != 4)
            {
                return ZConstants.Z_DATA_ERROR;
            }
            r = _codec.TotalBytesIn;
            w = _codec.TotalBytesOut;
            Reset();
            _codec.TotalBytesIn = r;
            _codec.TotalBytesOut = w;
            mode = InflateManagerMode.BLOCKS;
            return ZConstants.Z_OK;
        }

        // Returns true if inflate is currently at the end of a block generated
        // by Z_SYNC_FLUSH or Z_FULL_FLUSH. This function is used by one PPP
        // implementation to provide an additional safety check. PPP uses Z_SYNC_FLUSH
        // but removes the length bytes of the resulting empty stored block. When
        // decompressing, PPP checks that at the end of input packet, inflate is
        // waiting for these length bytes.
        internal int SyncPoint(ZCodec z)
        {
            return blocks.SyncPoint();
        }
    }
} // namespace UDPServer.zlib
