using System;
using System.IO;

namespace ICSharpCode.SharpZipLib.Tar
{
	/// <inheritdoc />
	/// <summary>
	///     The TarOutputStream writes a UNIX tar archive as an OutputStream.
	///     Methods are provided to put entries, and then write their contents
	///     by writing to this stream using write().
	/// </summary>
	/// public
	public class TarOutputStream : Stream
	{
		/// <summary>
		///     Gets or sets a flag indicating ownership of underlying stream.
		///     When the flag is true <see cref="Stream.Dispose()" /> will close the underlying stream also.
		/// </summary>
		/// <remarks>The default value is true.</remarks>
		public bool IsStreamOwner
		{
			get => _buffer.IsStreamOwner;
			set => _buffer.IsStreamOwner = value;
		}

		/// <inheritdoc />
		/// <summary>
		///     true if the stream supports reading; otherwise, false.
		/// </summary>
		public override bool CanRead => _outputStream.CanRead;

		/// <inheritdoc />
		/// <summary>
		///     true if the stream supports seeking; otherwise, false.
		/// </summary>
		public override bool CanSeek => _outputStream.CanSeek;

		/// <inheritdoc />
		/// <summary>
		///     true if stream supports writing; otherwise, false.
		/// </summary>
		public override bool CanWrite => _outputStream.CanWrite;

		/// <inheritdoc />
		/// <summary>
		///     length of stream in bytes
		/// </summary>
		public override long Length => _outputStream.Length;

		/// <inheritdoc />
		/// <summary>
		///     gets or sets the position within the current stream.
		/// </summary>
		public override long Position
		{
			get => _outputStream.Position;
			set => _outputStream.Position = value;
		}

		/// <summary>
		///     Get the record size being used by this stream's TarBuffer.
		/// </summary>
		public int RecordSize => _buffer.RecordSize;

		/// <summary>
		///     Get a value indicating wether an entry is open, requiring more data to be written.
		/// </summary>
		bool IsEntryOpen => _currBytes < _currSize;

		/// <inheritdoc />
		/// <summary>
		///     set the position within the current stream
		/// </summary>
		/// <param name="offset">The offset relative to the <paramref name="origin" /> to seek to</param>
		/// <param name="origin">The <see cref="T:System.IO.SeekOrigin" /> to seek from.</param>
		/// <returns>The new position in the stream.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			return _outputStream.Seek(offset, origin);
		}

		/// <inheritdoc />
		/// <summary>
		///     Set the length of the current stream
		/// </summary>
		/// <param name="value">The new stream length.</param>
		public override void SetLength(long value)
		{
			_outputStream.SetLength(value);
		}

		/// <inheritdoc />
		/// <summary>
		///     Read a byte from the stream and advance the position within the stream
		///     by one byte or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The byte value or -1 if at end of stream</returns>
		public override int ReadByte() => _outputStream.ReadByte();

		/// <inheritdoc />
		/// <summary>
		///     read bytes from the current stream and advance the position within the
		///     stream by the number of bytes read.
		/// </summary>
		/// <param name="readBuffer">The readBuffer to store read bytes in.</param>
		/// <param name="offset">The index into the readBuffer to being storing bytes at.</param>
		/// <param name="count">The desired number of bytes to read.</param>
		/// <returns>
		///     The total number of bytes read, or zero if at the end of the stream.
		///     The number of bytes may be less than the <paramref name="count">count</paramref>
		///     requested if data is not avialable.
		/// </returns>
		public override int Read(byte[] readBuffer, int offset, int count)
		{
			return _outputStream.Read(readBuffer, offset, count);
		}

		/// <inheritdoc />
		/// <summary>
		///     All buffered data is written to destination
		/// </summary>
		public override void Flush()
		{
			_outputStream.Flush();
		}

		/// <summary>
		///     Ends the TAR archive without closing the underlying OutputStream.
		///     The result is that the EOF block of nulls is written.
		/// </summary>
		public void Finish()
		{
			if (IsEntryOpen)
			{
				CloseEntry();
			}

			WriteEofBlock();
		}

		/// <inheritdoc />
		/// <summary>
		///     Ends the TAR archive and closes the underlying OutputStream.
		/// </summary>
		/// <remarks>
		///     This means that Finish() is called followed by calling the
		///     TarBuffer's Close().
		/// </remarks>
		protected override void Dispose(bool disposing)
		{
			if (!_isClosed)
			{
				_isClosed = true;
				Finish();
				_buffer.Close();
			}
		}

		/// <summary>
		///     Put an entry on the output stream. This writes the entry's
		///     header and positions the output stream for writing
		///     the contents of the entry. Once this method is called, the
		///     stream is ready for calls to write() to write the entry's
		///     contents. Once the contents are written, closeEntry()
		///     <B>MUST</B> be called to ensure that all buffered data
		///     is completely written to the output stream.
		/// </summary>
		/// <param name="entry">
		///     The TarEntry to be written to the archive.
		/// </param>
		public void PutNextEntry(TarEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			if (entry.TarHeader.Name.Length > TarHeader.Namelen)
			{
				var longHeader = new TarHeader {TypeFlag = TarHeader.LfGnuLongname};
				longHeader.Name = longHeader.Name + "././@LongLink";
				longHeader.Mode = 420; //644 by default
				longHeader.UserId = entry.UserId;
				longHeader.GroupId = entry.GroupId;
				longHeader.GroupName = entry.GroupName;
				longHeader.UserName = entry.UserName;
				longHeader.LinkName = "";
				longHeader.Size = entry.TarHeader.Name.Length + 1; // Plus one to avoid dropping last char

				longHeader.WriteHeader(_blockBuffer);
				_buffer.WriteBlock(_blockBuffer); // Add special long filename header block

				var nameCharIndex = 0;

				while (
					nameCharIndex <
					entry.TarHeader.Name.Length +
					1 /* we've allocated one for the null char, now we must make sure it gets written out */)
				{
					Array.Clear(_blockBuffer, 0, _blockBuffer.Length);
					TarHeader.GetAsciiBytes(entry.TarHeader.Name, nameCharIndex, _blockBuffer, 0,
						TarBuffer.BlockSize); // This func handles OK the extra char out of string length
					nameCharIndex += TarBuffer.BlockSize;
					_buffer.WriteBlock(_blockBuffer);
				}
			}

			entry.WriteEntryHeader(_blockBuffer);
			_buffer.WriteBlock(_blockBuffer);

			_currBytes = 0;

			_currSize = entry.IsDirectory ? 0 : entry.Size;
		}

		/// <summary>
		///     Close an entry. This method MUST be called for all file
		///     entries that contain data. The reason is that we must
		///     readBuffer data written to the stream in order to satisfy
		///     the readBuffer's block based writes. Thus, there may be
		///     data fragments still being assembled that must be written
		///     to the output stream before this entry is closed and the
		///     next entry written.
		/// </summary>
		public void CloseEntry()
		{
			if (_assemblyBufferLength > 0)
			{
				Array.Clear(_assemblyBuffer, _assemblyBufferLength, _assemblyBuffer.Length - _assemblyBufferLength);

				_buffer.WriteBlock(_assemblyBuffer);

				_currBytes += _assemblyBufferLength;
				_assemblyBufferLength = 0;
			}

			if (_currBytes < _currSize)
			{
				var errorText = string.Format(
					"Entry closed at '{0}' before the '{1}' bytes specified in the header were written",
					_currBytes, _currSize);
				throw new TarException(errorText);
			}
		}

		/// <summary>
		///     Writes a byte to the current tar archive entry.
		///     This method simply calls Write(byte[], int, int).
		/// </summary>
		/// <param name="value">
		///     The byte to be written.
		/// </param>
		public override void WriteByte(byte value)
		{
			Write(new[] {value}, 0, 1);
		}

		/// <inheritdoc />
		/// <summary>
		///     Writes bytes to the current tar archive entry. This method
		///     is aware of the current entry and will throw an exception if
		///     you attempt to write bytes past the length specified for the
		///     current entry. The method is also (painfully) aware of the
		///     record buffering required by TarBuffer, and manages buffers
		///     that are not a multiple of recordsize in length, including
		///     assembling records from small buffers.
		/// </summary>
		/// <param name="readBuffer">
		///     The readBuffer to write to the archive.
		/// </param>
		/// <param name="offset">
		///     The offset in the readBuffer from which to get bytes.
		/// </param>
		/// <param name="count">
		///     The number of bytes to write.
		/// </param>
		public override void Write(byte[] readBuffer, int offset, int count)
		{
			if (readBuffer == null)
			{
				throw new ArgumentNullException(nameof(readBuffer));
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset), "Cannot be negative");
			}

			if (readBuffer.Length - offset < count)
			{
				throw new ArgumentException("offset and count combination is invalid");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count), "Cannot be negative");
			}

			if (_currBytes + count > _currSize)
			{
				var errorText = string.Format("request to write '{0}' bytes exceeds size in header of '{1}' bytes",
					count, _currSize);
				throw new ArgumentOutOfRangeException(nameof(count), errorText);
			}

			//
			// We have to deal with assembly!!!
			// The programmer can be writing little 32 byte chunks for all
			// we know, and we must assemble complete blocks for writing.
			// TODO  REVIEW Maybe this should be in TarBuffer? Could that help to
			//        eliminate some of the readBuffer copying.
			//
			if (_assemblyBufferLength > 0)
			{
				if (_assemblyBufferLength + count >= _blockBuffer.Length)
				{
					var aLen = _blockBuffer.Length - _assemblyBufferLength;

					Array.Copy(_assemblyBuffer, 0, _blockBuffer, 0, _assemblyBufferLength);
					Array.Copy(readBuffer, offset, _blockBuffer, _assemblyBufferLength, aLen);

					_buffer.WriteBlock(_blockBuffer);

					_currBytes += _blockBuffer.Length;

					offset += aLen;
					count -= aLen;

					_assemblyBufferLength = 0;
				}
				else
				{
					Array.Copy(readBuffer, offset, _assemblyBuffer, _assemblyBufferLength, count);
					offset += count;
					_assemblyBufferLength += count;
					count -= count;
				}
			}

			//
			// When we get here we have EITHER:
			//   o An empty "assembly" readBuffer.
			//   o No bytes to write (count == 0)
			//
			while (count > 0)
			{
				if (count < _blockBuffer.Length)
				{
					Array.Copy(readBuffer, offset, _assemblyBuffer, _assemblyBufferLength, count);
					_assemblyBufferLength += count;
					break;
				}

				_buffer.WriteBlock(readBuffer, offset);

				var bufferLength = _blockBuffer.Length;
				_currBytes += bufferLength;
				count -= bufferLength;
				offset += bufferLength;
			}
		}

		/// <summary>
		///     Write an EOF (end of archive) block to the tar archive.
		///     The	end of the archive is indicated	by two blocks consisting entirely of zero bytes.
		/// </summary>
		void WriteEofBlock()
		{
			Array.Clear(_blockBuffer, 0, _blockBuffer.Length);
			_buffer.WriteBlock(_blockBuffer);
			_buffer.WriteBlock(_blockBuffer);
		}

		#region Constructors

		/// <inheritdoc />
		/// <summary>
		///     Construct TarOutputStream with user specified block factor
		/// </summary>
		/// <param name="outputStream">stream to write to</param>
		/// <param name="blockFactor">blocking factor</param>
		public TarOutputStream(Stream outputStream, int blockFactor = TarBuffer.DefaultBlockFactor)
		{
			_outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
			_buffer = TarBuffer.CreateOutputTarBuffer(outputStream, blockFactor);

			_assemblyBuffer = new byte[TarBuffer.BlockSize];
			_blockBuffer = new byte[TarBuffer.BlockSize];
		}

		#endregion

		#region Instance Fields

		/// <summary>
		///     bytes written for this entry so far
		/// </summary>
		long _currBytes;

		/// <summary>
		///     current 'Assembly' readBuffer length
		/// </summary>
		int _assemblyBufferLength;

		/// <summary>
		///     Flag indicating wether this instance has been closed or not.
		/// </summary>
		bool _isClosed;

		/// <summary>
		///     Size for the current entry
		/// </summary>
		long _currSize;

		/// <summary>
		///     single block working readBuffer
		/// </summary>
		readonly byte[] _blockBuffer;

		/// <summary>
		///     'Assembly' readBuffer used to assemble data before writing
		/// </summary>
		readonly byte[] _assemblyBuffer;

		/// <summary>
		///     TarBuffer used to provide correct blocking factor
		/// </summary>
		readonly TarBuffer _buffer;

		/// <summary>
		///     the destination stream for the archive contents
		/// </summary>
		readonly Stream _outputStream;

		#endregion
	}
}
