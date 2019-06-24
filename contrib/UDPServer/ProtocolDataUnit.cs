/**
 * UDP-socket Network Server
 * INTERNAL/PROPRIETARY/CONFIDENTIAL. Use is subject to license terms.
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 * 
 * Author:	Bryan Biedenkapp, <gatekeep@gmail.com>
 * Copyright 2018 Bryan Biedenkapp
 */
//
// Based on code from the DiscUtils project. (http://discutils.codeplex.com/)
// Copyright (c) 2008-2011, Kenneth Bell
// Licensed under the MIT License (http://www.opensource.org/licenses/MIT)
//
using System;
using System.Diagnostics;
using System.IO;

using UDPServer.zlib;

namespace UDPServer
{
    /// <summary>
    /// 
    /// </summary>
    public class HandshakeHeader : IByteArraySerializable
    {
        /**
         * Fields
         */
        public const ushort MAGIC_HEADER = 0x4958;

        /// <summary>
        /// 
        /// </summary>
        public byte CheckSum;
        /// <summary>
        /// 
        /// </summary>
        public ushort DataLength;
        /// <summary>
        /// 
        /// </summary>
        public ushort CompressedLength;

        /// <summary>
        /// 
        /// </summary>
        public byte[] MacAddr;

        /// <summary>
        /// 
        /// </summary>
        public ushort Length;

        /**
         * Properties
         */
        /// <summary>
        /// Gets the total number of bytes the structure occupies.
        /// </summary>
        public int Size
        {
            get { return 16; }
        }

        /**
         * Methods
         */
        /// <summary>
        /// Reads the structure from a byte array.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The buffer offset to start reading from.</param>
        /// <returns>The number of bytes read.</returns>
        public int ReadFrom(byte[] buffer, int offset)
        {
            // build actual header data
            CheckSum = buffer[offset + 2];
            DataLength = Util.ToUInt16LittleEndian(buffer, offset + 4);
            CompressedLength = Util.ToUInt16LittleEndian(buffer, offset + 6);

            // get mac address values
            int macOffset = offset + 8;
            MacAddr = new byte[6];

            for (int i = 0; i < 6; i++)
                MacAddr[i] = buffer[macOffset + i];

            Length = Util.ToUInt16LittleEndian(buffer, offset + 14);

            return Size;
        }

        /// <summary>
        /// Writes a structure to a byte array.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="offset">The buffer offset to start writing to.</param>
        /// <returns></returns>
        public byte[] WriteTo(byte[] buffer, int offset)
        {
            Util.WriteBytesLittleEndian(MAGIC_HEADER, buffer, offset);
            buffer[offset + 2] = CheckSum;
            Util.WriteBytesLittleEndian(DataLength, buffer, offset + 4);
            Util.WriteBytesLittleEndian(CompressedLength, buffer, offset + 6);

            int macOffset = offset + 8;
            for (int i = 0; i < 6; i++)
                buffer[macOffset + i] = MacAddr[i];

            Util.WriteBytesLittleEndian(Length, buffer, offset + 14);

            return buffer;
        }
    } // public class HandshakeHeader : IByteArraySerializable

    /// <summary>
    /// Implements the wrapper that handles protocol data.
    /// </summary>
    public class ProtocolDataUnit
    {
        /**
         * Properties
         */
        /// <summary>
        /// Raw Protocol Header Data
        /// </summary>
        public byte[] HeaderData
        {
            get;
            private set;
        }

        /// <summary>
        /// 
        /// </summary>
        public HandshakeHeader Header
        {
            get;
            private set;
        }

        /// <summary>
        /// Raw Packet content
        /// </summary>
        public byte[] ContentData
        {
            get;
            private set;
        }

        /**
         * Methods
         */
        /// <summary>
        /// Initializes a new instance of the <see cref="ProtocolDataUnit"/> class.
        /// </summary>
        /// <param name="bhs"></param>
        /// <param name="rawHeaderData"></param>
        /// <param name="rawContentData"></param>
        public ProtocolDataUnit(byte[] rawHeaderData, byte[] rawContentData)
        {
            this.HeaderData = rawHeaderData;

            HandshakeHeader header = new HandshakeHeader();
            header.ReadFrom(rawHeaderData, 0);
            this.Header = header;

            // decompress content data
            if ((header.DataLength > 0) && (header.CompressedLength > 0))
            {
                // put raw data into a stream to decompress
                MemoryStream memoryStream = new MemoryStream(ZStream.UncompressBuffer(rawContentData));
                this.ContentData = Util.ReadFully(memoryStream, header.DataLength);
            }
            else
                this.ContentData = null;
        }

        /// <summary>
        /// Static helper function to return a protocol data unit.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static ProtocolDataUnit ReadFrom(byte[] buffer)
        {
            int numRead = 0;

            // read the first bytes from the stream
            byte[] headerData = new byte[16];
            Buffer.BlockCopy(buffer, 0, headerData, 0, 16);
            numRead += 16;

            // build header data
#if TRACE
            Util.TraceHex("magic [" + HandshakeHeader.MAGIC_HEADER.ToString("X") + "] dump", headerData, 2);
#endif
            ushort magicHeader = Util.ToUInt16LittleEndian(headerData, 0);
            if (magicHeader == HandshakeHeader.MAGIC_HEADER)
            {
                ushort dataLength = Util.ToUInt16LittleEndian(headerData, 6);

                byte[] contentData = new byte[dataLength];
                if (dataLength > 0)
                {
                    Buffer.BlockCopy(buffer, 16, contentData, 0, dataLength);
                    numRead += dataLength;
                }

                return new ProtocolDataUnit(headerData, contentData);
            }
            else
                Trace.WriteLine("header magic expected [" + HandshakeHeader.MAGIC_HEADER.ToString("X") + "] got [" + magicHeader.ToString("X") + "]!");

            return null;
        }
    } // public class ProtocolDataUnit
} // namespace UDPServer
