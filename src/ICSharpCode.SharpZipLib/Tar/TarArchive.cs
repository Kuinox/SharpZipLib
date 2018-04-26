using System;
using System.IO;

namespace ICSharpCode.SharpZipLib.Tar
{
	/// <summary>
	/// Used to advise clients of 'events' while processing archives
	/// </summary>
	public delegate void ProgressMessageHandler(TarArchive archive, TarEntry entry, string message);

	/// <inheritdoc />
	/// <summary>
	/// The TarArchive class implements the concept of a
	/// 'Tape Archive'. A tar archive is a series of entries, each of
	/// which represents a file system object. Each entry in
	/// the archive consists of a header block followed by 0 or more data blocks.
	/// Directory entries consist only of the header block, and are followed by entries
	/// for the directory's contents. File entries consist of a
	/// header followed by the number of blocks needed to
	/// contain the file's contents. All entries are written on
	/// block boundaries. Blocks are 512 bytes long.
	/// TarArchives are instantiated in either read or write mode,
	/// based upon whether they are instantiated with an InputStream
	/// or an OutputStream. Once instantiated TarArchives read/write
	/// mode can not be changed.
	/// There is currently no support for random access to tar archives.
	/// However, it seems that subclassing TarArchive, and using the
	/// TarBuffer.CurrentRecord and TarBuffer.CurrentBlock
	/// properties, this would be rather trivial.
	/// </summary>
	public sealed class TarArchive : IDisposable
	{
		/// <summary>
		/// Client hook allowing detailed information to be reported during processing
		/// </summary>
		public event ProgressMessageHandler ProgressMessageEvent;

		/// <summary>
		/// Raises the ProgressMessage event
		/// </summary>
		/// <param name="entry">The <see cref="TarEntry">TarEntry</see> for this event</param>
		/// <param name="message">message for this event.  Null is no message</param>
		void OnProgressMessageEvent(TarEntry entry, string message)
		{
			var handler = ProgressMessageEvent;
			handler?.Invoke(this, entry, message);
		}

		#region Constructors

		/// <summary>
		/// Initalise a TarArchive for input.
		/// </summary>
		/// <param name="stream">The <see cref="TarInputStream"/> to use for input.</param>
		TarArchive(TarInputStream stream)
		{
			_tarIn = stream ?? throw new ArgumentNullException(nameof(stream));
		}

		/// <summary>
		/// Initialise a TarArchive for output.
		/// </summary>
		/// <param name="stream">The <see cref="TarOutputStream"/> to use for output.</param> 
		TarArchive(TarOutputStream stream)
		{
			_tarOut = stream ?? throw new ArgumentNullException(nameof(stream));
		}
		#endregion

		#region Static factory methods
		/// <summary>
		/// The InputStream based constructors create a TarArchive for the
		/// purposes of extracting or listing a tar archive. Thus, use
		/// these constructors when you wish to extract files from or list
		/// the contents of an existing tar archive.
		/// </summary>
		/// <param name="inputStream">The stream to retrieve archive data from.</param>
		/// <returns>Returns a new <see cref="TarArchive"/> suitable for reading from.</returns>
		public static TarArchive CreateInputTarArchive(Stream inputStream)
		{
			switch (inputStream)
			{
				case null:
					throw new ArgumentNullException(nameof(inputStream));
				case TarInputStream tarStream:
					return new TarArchive(tarStream);
			}
			return CreateInputTarArchive(inputStream, TarBuffer.DefaultBlockFactor);

		}

		/// <summary>
		/// Create TarArchive for reading setting block factor
		/// </summary>
		/// <param name="inputStream">A stream containing the tar archive contents</param>
		/// <param name="blockFactor">The blocking factor to apply</param>
		/// <returns>Returns a <see cref="TarArchive"/> suitable for reading.</returns>
		static TarArchive CreateInputTarArchive(Stream inputStream, int blockFactor)
		{
			switch (inputStream)
			{
				case null:
					throw new ArgumentNullException(nameof(inputStream));
				case TarInputStream _:
					throw new ArgumentException("TarInputStream not valid");
			}
			return new TarArchive(new TarInputStream(inputStream, blockFactor));
		}

		/// <summary>
		/// Create a TarArchive for writing to, using the default blocking factor
		/// </summary>
		/// <param name="outputStream">The <see cref="Stream"/> to write to</param>
		/// <returns>Returns a <see cref="TarArchive"/> suitable for writing.</returns>
		public static TarArchive CreateOutputTarArchive(Stream outputStream)
		{
			switch (outputStream)
			{
				case null:
					throw new ArgumentNullException(nameof(outputStream));
				case TarOutputStream tarStream:
					return new TarArchive(tarStream);
			}
			return CreateOutputTarArchive(outputStream, TarBuffer.DefaultBlockFactor);

		}

		/// <summary>
		/// Create a <see cref="TarArchive">tar archive</see> for writing.
		/// </summary>
		/// <param name="outputStream">The stream to write to</param>
		/// <param name="blockFactor">The blocking factor to use for buffering.</param>
		/// <returns>Returns a <see cref="TarArchive"/> suitable for writing.</returns>
		static TarArchive CreateOutputTarArchive(Stream outputStream, int blockFactor)
		{
			switch (outputStream)
			{
				case null:
					throw new ArgumentNullException(nameof(outputStream));
				case TarOutputStream _:
					throw new ArgumentException("TarOutputStream is not valid");
			}

			return new TarArchive(new TarOutputStream(outputStream, blockFactor));
		}
		#endregion

		/// <summary>
		/// Get the archive's record size. Tar archives are composed of
		/// a series of RECORDS each containing a number of BLOCKS.
		/// This allowed tar archives to match the IO characteristics of
		/// the physical device being used. Archives are expected
		/// to be properly "blocked".
		/// </summary>
		/// <returns>
		/// The record size this archive is using.
		/// </returns>
		public int RecordSize
		{
			get
			{
				if (_isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				if (_tarIn != null)
				{
					return _tarIn.RecordSize;
				}

				return _tarOut?.RecordSize ?? TarBuffer.DefaultRecordSize;
			}
		}

		/// <summary>
		/// Perform the "list" command for the archive contents.
		/// 
		/// NOTE That this method uses the <see cref="ProgressMessageEvent"> progress event</see> to actually list
		/// the contents. If the progress display event is not set, nothing will be listed!
		/// </summary>
		public void ListContents()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			while (true)
			{
				TarEntry entry = _tarIn.GetNextEntry();

				if (entry == null)
				{
					break;
				}
				OnProgressMessageEvent(entry, null);
			}
		}

		/// <summary>
		/// Perform the "extract" command and extract the contents of the archive.
		/// </summary>
		/// <param name="destinationDirectory">
		/// The destination directory into which to extract.
		/// </param>
		// ReSharper disable once UnusedMember.Global
		public void ExtractContents(string destinationDirectory)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			while (true)
			{
				TarEntry entry = _tarIn.GetNextEntry();

				if (entry == null)
				{
					break;
				}

				if (entry.TarHeader.TypeFlag == TarHeader.LfLink || entry.TarHeader.TypeFlag == TarHeader.LfSymlink)
				{
					continue;
				}

				ExtractEntry(destinationDirectory, entry);
			}
		}

		/// <summary>
		/// Extract an entry from the archive. This method assumes that the
		/// tarIn stream has been properly set with a call to GetNextEntry().
		/// </summary>
		/// <param name="destDir">
		/// The destination directory into which to extract.
		/// </param>
		/// <param name="entry">
		/// The TarEntry returned by tarIn.GetNextEntry().
		/// </param>
		void ExtractEntry(string destDir, TarEntry entry)
		{
			OnProgressMessageEvent(entry, null);

			var name = entry.Name;

			if (Path.IsPathRooted(name))
			{
				// NOTE:
				// for UNC names...  \\machine\share\zoom\beet.txt gives \zoom\beet.txt
				name = name.Substring(Path.GetPathRoot(name).Length);
			}

			name = name.Replace('/', Path.DirectorySeparatorChar);

			var destFile = Path.Combine(destDir, name);

			var rdbuf = new byte[32 * 1024];
			if (entry.IsDirectory)
			{
				EnsureDirectoryExists(destFile);
			}
			else
			{
				var parentDirectory = Path.GetDirectoryName(destFile);
				EnsureDirectoryExists(parentDirectory);

				var process = true;
				var fileInfo = new FileInfo(destFile);
				if (fileInfo.Exists)
				{
					if ((fileInfo.Attributes & FileAttributes.ReadOnly) != 0)
					{
						OnProgressMessageEvent(entry, "Destination file already exists, and is read-only");
						process = false;
					}
				}

				if (!process) return;

				Stream outputStream = File.Create(destFile);
				while (true)
				{
					var numRead = _tarIn.Read(rdbuf, 0, rdbuf.Length);

					if (numRead <= 0)
					{
						break;
					}
					outputStream.Write(rdbuf, 0, numRead);
				}
				outputStream.Dispose();
			}
		}

		/// <inheritdoc />
		/// <summary>
		/// Releases the unmanaged resources used by the FileStream and optionally releases the managed resources.
		/// </summary>
		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;
			_tarOut?.Flush();
			_tarOut?.Dispose();
			_tarIn?.Dispose();
		}

		static void EnsureDirectoryExists(string directoryName)
		{
			if (Directory.Exists(directoryName)) return;
			try
			{
				Directory.CreateDirectory(directoryName);
			}
			catch (Exception e)
			{
				throw new IOException("Exception creating directory '" + directoryName + "', " + e.Message, e);
			}
		}

		// TODO: TarArchive - Is there a better way to test for a text file?
		// It no longer reads entire files into memory but is still a weak test!
		// This assumes that byte values 0-7, 14-31 or 255 are binary
		// and that all non text files contain one of these values

		#region Instance Fields
		readonly TarInputStream _tarIn;
		readonly TarOutputStream _tarOut;
		bool _isDisposed;
		#endregion
	}
}
