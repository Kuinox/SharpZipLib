using System;
using System.IO;
using System.Text;

namespace ICSharpCode.SharpZipLib.Tar
{
	/// <inheritdoc />
	/// <summary>
	///     The TarInputStream reads a UNIX tar archive as an InputStream.
	///     methods are provided to position at each successive entry in
	///     the archive, and the read each entry as a normal input stream
	///     using read().
	/// </summary>
	public class TarInputStream : Stream
	{
		/// <summary>
		///     Gets or sets a flag indicating ownership of underlying stream.
		///     When the flag is true <see cref="Stream.Dispose()" /> will close the underlying stream also.
		/// </summary>
		/// <remarks>The default value is true.</remarks>
		public bool IsStreamOwner
		{
			set => _tarBuffer.IsStreamOwner = value;
		}

		/// <summary>
		///     Get the record size being used by this stream's TarBuffer.
		/// </summary>
		public int RecordSize => _tarBuffer.RecordSize;

		/// <summary>
		///     Skip bytes in the input buffer. This skips bytes in the
		///     current entry's data, not the entire archive, and will
		///     stop at the end of the current entry's data if the number
		///     to skip extends beyond that point.
		/// </summary>
		/// <param name="skipCount">
		///     The number of bytes to skip.
		/// </param>
		void Skip(long skipCount)
		{
			// TODO: REVIEW efficiency of TarInputStream.Skip
			// This is horribly inefficient, but it ensures that we
			// properly skip over bytes via the TarBuffer...
			//
			var skipBuf = new byte[8 * 1024];

			for (var num = skipCount; num > 0;)
			{
				var toRead = num > skipBuf.Length ? skipBuf.Length : (int)num;
				var numRead = Read(skipBuf, 0, toRead);

				if (numRead == -1)
				{
					break;
				}

				num -= numRead;
			}
		}

		/// <summary>
		///     Get the next entry in this tar archive. This will skip
		///     over any remaining data in the current entry, if there
		///     is one, and place the input stream at the header of the
		///     next entry, and read the header and instantiate a new
		///     TarEntry from the header bytes and return that entry.
		///     If there are no more entries in the archive, null will
		///     be returned to indicate that the end of the archive has
		///     been reached.
		/// </summary>
		/// <returns>
		///     The next TarEntry in the archive, or null.
		/// </returns>
		public TarEntry GetNextEntry()
		{
			if (_hasHitEof)
			{
				return null;
			}

			if (_currentEntry != null)
			{
				SkipToNextEntry();
			}

			var headerBuf = _tarBuffer.ReadBlock();

			if (headerBuf == null)
			{
				_hasHitEof = true;
			}
			else
			{
				_hasHitEof |= TarBuffer.IsEndOfArchiveBlock(headerBuf);
			}

			if (_hasHitEof)
			{
				_currentEntry = null;
			}
			else
			{
				try
				{
					var header = new TarHeader();
					header.ParseBuffer(headerBuf);
					if (!header.IsChecksumValid)
					{
						throw new InvalidDataException("Header checksum is invalid");
					}

					_entryOffset = 0;
					_entrySize = header.Size;

					StringBuilder longName = null;

					switch (header.TypeFlag)
					{
						case TarHeader.LfGnuLongname:
							var nameBuffer = new byte[TarBuffer.BlockSize];
							var numToRead = _entrySize;

							longName = new StringBuilder();

							while (numToRead > 0)
							{
								var numRead = Read(nameBuffer, 0, numToRead > nameBuffer.Length ? nameBuffer.Length : (int)numToRead);

								if (numRead == -1)
								{
									throw new InvalidDataException("Failed to read long name entry");
								}

								longName.Append(TarHeader.ParseName(nameBuffer, 0, numRead));
								numToRead -= numRead;
							}

							SkipToNextEntry();
							headerBuf = _tarBuffer.ReadBlock();
							break;
						case TarHeader.LfGhdr:
							// POSIX global extended header 
							// Ignore things we dont understand completely for now
							SkipToNextEntry();
							headerBuf = _tarBuffer.ReadBlock();
							break;
						case TarHeader.LfXhdr:
							// POSIX extended header
							// Ignore things we dont understand completely for now
							SkipToNextEntry();
							headerBuf = _tarBuffer.ReadBlock();
							break;
						case TarHeader.LfGnuVolhdr:
							// TODO: could show volume name when verbose
							SkipToNextEntry();
							headerBuf = _tarBuffer.ReadBlock();
							break;
						default:
							if (header.TypeFlag != TarHeader.LfNormal &&
							    header.TypeFlag != TarHeader.LfOldnorm &&
							    header.TypeFlag != TarHeader.LfLink &&
							    header.TypeFlag != TarHeader.LfSymlink &&
							    header.TypeFlag != TarHeader.LfDir)
							{
								// Ignore things we dont understand completely for now
								SkipToNextEntry();
								headerBuf = _tarBuffer.ReadBlock();
							}

							break;
					}

					_currentEntry = new TarEntry(headerBuf);
					if (longName != null)
					{
						_currentEntry.Name = longName.ToString();
					}


					// Magic was checked here for 'ustar' but there are multiple valid possibilities
					// so this is not done anymore.

					_entryOffset = 0;

					// TODO: Review How do we resolve this discrepancy?!
					_entrySize = _currentEntry.Size;
				}
				catch (InvalidDataException ex)
				{
					_entrySize = 0;
					_entryOffset = 0;
					_currentEntry = null;
					var errorText = string.Format("Bad header in record {0} block {1} {2}",
						_tarBuffer.CurrentRecord, _tarBuffer.CurrentBlock, ex.Message);
					throw new InvalidDataException(errorText);
				}
			}

			return _currentEntry;
		}

		void SkipToNextEntry()
		{
			var numToSkip = _entrySize - _entryOffset;

			if (numToSkip > 0)
			{
				Skip(numToSkip);
			}

			_readBuffer = null;
		}

		#region Constructors

		/// <inheritdoc />
		/// <summary>
		///     Construct a TarInputStream with user specified block factor
		/// </summary>
		/// <param name="inputStream">stream to source data from</param>
		/// <param name="blockFactor">block factor to apply to archive</param>
		public TarInputStream(Stream inputStream, int blockFactor = TarBuffer.DefaultBlockFactor)
		{
			_inputStream = inputStream;
			_tarBuffer = TarBuffer.CreateInputTarBuffer(inputStream, blockFactor);
		}

		#endregion

		#region Stream Overrides

		/// <inheritdoc />
		/// <summary>
		///     Gets a value indicating whether the current stream supports reading
		/// </summary>
		public override bool CanRead => _inputStream.CanRead;

		/// <inheritdoc />
		/// <summary>
		///     Gets a value indicating whether the current stream supports seeking
		///     This property always returns false.
		/// </summary>
		public override bool CanSeek => false;

		/// <summary>
		///     Gets a value indicating if the stream supports writing.
		///     This property always returns false.
		/// </summary>
		public override bool CanWrite => false;

		/// <summary>
		///     The length in bytes of the stream
		/// </summary>
		public override long Length => _inputStream.Length;

		/// <summary>
		///     Gets or sets the position within the stream.
		///     Setting the Position is not supported and throws a NotSupportedExceptionNotSupportedException
		/// </summary>
		/// <exception cref="NotSupportedException">Any attempt to set position</exception>
		public override long Position
		{
			get => _inputStream.Position;
			set => throw new NotSupportedException("TarInputStream Seek not supported");
		}

		/// <summary>
		///     Flushes the baseInputStream
		/// </summary>
		public override void Flush()
		{
			_inputStream.Flush();
		}

		/// <summary>
		///     Set the streams position.  This operation is not supported and will throw a NotSupportedException
		/// </summary>
		/// <param name="offset">The offset relative to the origin to seek to.</param>
		/// <param name="origin">The <see cref="SeekOrigin" /> to start seeking from.</param>
		/// <returns>The new position in the stream.</returns>
		/// <exception cref="NotSupportedException">Any access</exception>
		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("TarInputStream Seek not supported");
		}

		/// <summary>
		///     Sets the length of the stream
		///     This operation is not supported and will throw a NotSupportedException
		/// </summary>
		/// <param name="value">The new stream length.</param>
		/// <exception cref="NotSupportedException">Any access</exception>
		public override void SetLength(long value)
		{
			throw new NotSupportedException("TarInputStream SetLength not supported");
		}

		/// <summary>
		///     Writes a block of bytes to this stream using data from a buffer.
		///     This operation is not supported and will throw a NotSupportedException
		/// </summary>
		/// <param name="buffer">The buffer containing bytes to write.</param>
		/// <param name="offset">The offset in the buffer of the frist byte to write.</param>
		/// <param name="count">The number of bytes to write.</param>
		/// <exception cref="NotSupportedException">Any access</exception>
		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("TarInputStream Write not supported");
		}

		/// <summary>
		///     Writes a byte to the current position in the file stream.
		///     This operation is not supported and will throw a NotSupportedException
		/// </summary>
		/// <param name="value">The byte value to write.</param>
		/// <exception cref="NotSupportedException">Any access</exception>
		public override void WriteByte(byte value)
		{
			throw new NotSupportedException("TarInputStream WriteByte not supported");
		}

		/// <summary>
		///     Reads a byte from the current tar archive entry.
		/// </summary>
		/// <returns>A byte cast to an int; -1 if the at the end of the stream.</returns>
		public override int ReadByte()
		{
			var oneByteBuffer = new byte[1];
			var num = Read(oneByteBuffer, 0, 1);
			if (num <= 0)
			{
				// return -1 to indicate that no byte was read.
				return -1;
			}

			return oneByteBuffer[0];
		}

		/// <summary>
		///     Reads bytes from the current tar archive entry.
		///     This method is aware of the boundaries of the current
		///     entry in the archive and will deal with them appropriately
		/// </summary>
		/// <param name="buffer">
		///     The buffer into which to place bytes read.
		/// </param>
		/// <param name="offset">
		///     The offset at which to place bytes read.
		/// </param>
		/// <param name="count">
		///     The number of bytes to read.
		/// </param>
		/// <returns>
		///     The number of bytes read, or 0 at end of stream/EOF.
		/// </returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			var totalRead = 0;

			if (_entryOffset >= _entrySize)
			{
				return 0;
			}

			long numToRead = count;

			if (numToRead + _entryOffset > _entrySize)
			{
				numToRead = _entrySize - _entryOffset;
			}

			if (_readBuffer != null)
			{
				var sz = numToRead > _readBuffer.Length ? _readBuffer.Length : (int)numToRead;

				Array.Copy(_readBuffer, 0, buffer, offset, sz);

				if (sz >= _readBuffer.Length)
				{
					_readBuffer = null;
				}
				else
				{
					var newLen = _readBuffer.Length - sz;
					var newBuf = new byte[newLen];
					Array.Copy(_readBuffer, sz, newBuf, 0, newLen);
					_readBuffer = newBuf;
				}

				totalRead += sz;
				numToRead -= sz;
				offset += sz;
			}

			while (numToRead > 0)
			{
				var rec = _tarBuffer.ReadBlock();
				if (rec == null)
				{
					// Unexpected EOF!
					throw new InvalidDataException("unexpected EOF with " + numToRead + " bytes unread");
				}

				var sz = (int)numToRead;
				var recLen = rec.Length;

				if (recLen > sz)
				{
					Array.Copy(rec, 0, buffer, offset, sz);
					_readBuffer = new byte[recLen - sz];
					Array.Copy(rec, sz, _readBuffer, 0, recLen - sz);
				}
				else
				{
					sz = recLen;
					Array.Copy(rec, 0, buffer, offset, recLen);
				}

				totalRead += sz;
				numToRead -= sz;
				offset += sz;
			}

			_entryOffset += totalRead;

			return totalRead;
		}

		/// <inheritdoc />
		/// <summary>
		///     Closes this stream. Calls the TarBuffer's close() method.
		///     The underlying stream is closed by the TarBuffer.
		/// </summary>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_tarBuffer.Close();
			}
		}

		#endregion

		#region Instance Fields

		/// <summary>
		///     Flag set when last block has been read
		/// </summary>
		bool _hasHitEof;

		/// <summary>
		///     Size of this entry as recorded in header
		/// </summary>
		long _entrySize;

		/// <summary>
		///     Number of bytes read for this entry so far
		/// </summary>
		long _entryOffset;

		/// <summary>
		///     Buffer used with calls to <code>Read()</code>
		/// </summary>
		byte[] _readBuffer;

		/// <summary>
		///     Working buffer
		/// </summary>
		readonly TarBuffer _tarBuffer;

		/// <summary>
		///     Current entry being read
		/// </summary>
		TarEntry _currentEntry;

		/// <summary>
		///     Stream used as the source of input data.
		/// </summary>
		readonly Stream _inputStream;

		#endregion
	}
}
