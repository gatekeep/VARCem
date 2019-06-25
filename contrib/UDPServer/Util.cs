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
using System.Reflection;

namespace UDPServer
{
    /// <summary>
    /// Common global utility methods.
    /// </summary>
    internal static class Util
    {
        /**
         * Methods
         */
        /// <summary>
        /// Writes the exception stack trace to the console/trace log
        /// </summary>
        /// <param name="throwable">Exception to obtain information from</param>
        /// <param name="reThrow"></param>
        public static void StackTrace(Exception throwable, bool reThrow = true)
        {
            StackTrace(string.Empty, throwable, reThrow);
        }

        /// <summary>
        /// Writes the exception stack trace to the console/trace log
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="throwable">Exception to obtain information from</param>
        /// <param name="reThrow"></param>
        public static void StackTrace(string msg, Exception throwable, bool reThrow = true)
        {
            MethodBase mb = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod();
            ParameterInfo[] param = mb.GetParameters();
            string funcParams = string.Empty;
            for (int i = 0; i < param.Length; i++)
                if (i < param.Length - 1)
                    funcParams += param[i].ParameterType.Name + ", ";
                else
                    funcParams += param[i].ParameterType.Name;

            Exception inner = throwable.InnerException;

            Console.WriteLine("caught an unrecoverable exception! " + msg);
            Console.WriteLine("---- TRACE SNIP ----");
            Console.WriteLine(throwable.Message + (inner != null ? " (Inner: " + inner.Message + ")" : ""));
            Console.WriteLine(throwable.GetType().ToString());

            Console.WriteLine("<" + mb.ReflectedType.Name + "::" + mb.Name + "(" + funcParams + ")>");
            Console.WriteLine(throwable.Source);
            foreach (string str in throwable.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                Console.WriteLine(str);
            if (inner != null)
                foreach (string str in throwable.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
                    Console.WriteLine("inner trace: " + str);
            Console.WriteLine("---- TRACE SNIP ----");

            if (reThrow)
                throw throwable;
        }

        /// <summary>
        /// Perform a hex dump of a buffer.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="buffer"></param>
        /// <param name="maxLength"></param>
        /// <param name="myTraceFilter"></param>
        /// <param name="startOffset"></param>
        public static void TraceHex(string message, byte[] buffer, int maxLength = 32, int startOffset = 0)
        {
            int bCount = 0, j = 0, lenCount = 0;

            // iterate through buffer printing all the stored bytes
            string traceMsg = message + " Off [" + j.ToString("X4") + "] -> [";
            for (int i = startOffset; i < buffer.Length; i++)
            {
                byte b = buffer[i];

                // split the message every 16 bytes...
                if (bCount == 16)
                {
                    traceMsg += "]";
                    Console.WriteLine(traceMsg);

                    bCount = 0;
                    j += 16;
                    traceMsg = message + " Off [" + j.ToString("X4") + "] -> [";
                }
                else
                    traceMsg += (bCount > 0) ? " " : "";

                traceMsg += b.ToString("X2");

                bCount++;

                // increment the length counter, and check if we've exceeded the specified
                // maximum, then break the loop
                lenCount++;
                if (lenCount > maxLength)
                    break;
            }

            // if the byte count at this point is non-zero print the message
            if (bCount != 0)
            {
                traceMsg += "]";
                Console.WriteLine(traceMsg);
            }
        }

        /// <summary>
        /// Write the given bytes in the unsigned short into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(ushort val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val & 0xFF);
            buffer[offset + 1] = (byte)((val >> 8) & 0xFF);
        }

        /// <summary>
        /// Write the given bytes in the unsigned integer into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(uint val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val & 0xFF);
            buffer[offset + 1] = (byte)((val >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((val >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((val >> 24) & 0xFF);
        }

        /// <summary>
        /// Write the given bytes in the unsigned long into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(ulong val, byte[] buffer, int offset)
        {
            buffer[offset] = (byte)(val & 0xFF);
            buffer[offset + 1] = (byte)((val >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((val >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((val >> 24) & 0xFF);
            buffer[offset + 4] = (byte)((val >> 32) & 0xFF);
            buffer[offset + 5] = (byte)((val >> 40) & 0xFF);
            buffer[offset + 6] = (byte)((val >> 48) & 0xFF);
            buffer[offset + 7] = (byte)((val >> 56) & 0xFF);
        }

        /// <summary>
        /// Write the given bytes in the short into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(short val, byte[] buffer, int offset)
        {
            WriteBytesLittleEndian((ushort)val, buffer, offset);
        }

        /// <summary>
        /// Write the given bytes in the integer into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(int val, byte[] buffer, int offset)
        {
            WriteBytesLittleEndian((uint)val, buffer, offset);
        }

        /// <summary>
        /// Write the given bytes in the long into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(long val, byte[] buffer, int offset)
        {
            WriteBytesLittleEndian((ulong)val, buffer, offset);
        }

        /// <summary>
        /// Write the given bytes in the Guid into the given buffer (by least significant byte)
        /// </summary>
        /// <param name="val"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        public static void WriteBytesLittleEndian(Guid val, byte[] buffer, int offset)
        {
            byte[] le = val.ToByteArray();
            Array.Copy(le, 0, buffer, offset, 16);
        }

        /// <summary>
        /// Get an unsigned short value from the given bytes. (by least significant byte)
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static ushort ToUInt16LittleEndian(byte[] buffer, int offset)
        {
            return (ushort)(((buffer[offset + 1] << 8) & 0xFF00) | ((buffer[offset + 0] << 0) & 0x00FF));
        }

        /// <summary>
        /// Read bytes until buffer filled or EOF.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="buffer">The buffer to populate.</param>
        /// <param name="offset">Offset in the buffer to start.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        public static int ReadFully(Stream stream, byte[] buffer, int offset, int length)
        {
            int totalRead = 0;
            int numRead = stream.Read(buffer, offset, length);
            while (numRead > 0)
            {
                totalRead += numRead;
                if (totalRead == length)
                    break;

                numRead = stream.Read(buffer, offset + totalRead, length - totalRead);
            }

            return totalRead;
        }

        /// <summary>
        /// Read bytes until buffer filled or throw IOException.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>The data read from the stream.</returns>
        public static byte[] ReadFully(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            if (ReadFully(stream, buffer, 0, count) == count)
                return buffer;
            else
                throw new IOException("Unable to complete read of " + count + " bytes");
        }

        /// <summary>
        /// Round up a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-up value.</returns>
        public static long RoundUp(long value, long unit)
        {
            return ((value + (unit - 1)) / unit) * unit;
        }

        /// <summary>
        /// Round up a value to a multiple of a unit size.
        /// </summary>
        /// <param name="value">The value to round up.</param>
        /// <param name="unit">The unit (the returned value will be a multiple of this number).</param>
        /// <returns>The rounded-up value.</returns>
        public static int RoundUp(int value, int unit)
        {
            return ((value + (unit - 1)) / unit) * unit;
        }
    } // public static class Util
} // namespace UDPServer
