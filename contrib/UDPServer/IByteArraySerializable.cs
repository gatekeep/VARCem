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
using System.IO;

namespace UDPServer
{
    /// <summary>
    /// Common interface for reading structures to/from byte arrays.
    /// </summary>
    public interface IByteArraySerializable
    {
        /**
         * Properties
         */
        /// <summary>
        /// Gets the total number of bytes the structure occupies.
        /// </summary>
        int Size 
        {
            get; 
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
        int ReadFrom(byte[] buffer, int offset);

        /// <summary>
        /// Writes a structure to a byte array.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="offset">The buffer offset to start writing to.</param>
        /// <returns></returns>
        byte[] WriteTo(byte[] buffer, int offset);
    } // internal interface IByteArraySerializable
} // namespace UDPServer
