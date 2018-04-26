using System;
using System.IO;
using System.Text;

namespace ICSharpCode.SharpZipLib.Tar
{
	/// <summary>
	/// Used to advise clients of 'events' while processing archives
	/// </summary>
	public delegate void ProgressMessageHandler(TarArchive archive, TarEntry entry, string message);

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
	/// 
	/// TarArchives are instantiated in either read or write mode,
	/// based upon whether they are instantiated with an InputStream
	/// or an OutputStream. Once instantiated TarArchives read/write
	/// mode can not be changed.
	/// 
	/// There is currently no support for random access to tar archives.
	/// However, it seems that subclassing TarArchive, and using the
	/// TarBuffer.CurrentRecord and TarBuffer.CurrentBlock
	/// properties, this would be rather trivial.
	/// </summary>
	public class TarArchive : IDisposable
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
		protected virtual void OnProgressMessageEvent(TarEntry entry, string message)
		{
			var handler = ProgressMessageEvent;
			handler?.Invoke(this, entry, message);
		}

		#region Constructors

		/// <summary>
		/// Initalise a TarArchive for input.
		/// </summary>
		/// <param name="stream">The <see cref="TarInputStream"/> to use for input.</param>
		protected TarArchive(TarInputStream stream)
		{
			_tarIn = stream ?? throw new ArgumentNullException(nameof(stream));
		}

		/// <summary>
		/// Initialise a TarArchive for output.
		/// </summary>
		/// <param name="stream">The <see cref="TarOutputStream"/> to use for output.</param> 
		protected TarArchive(TarOutputStream stream)
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
		public static TarArchive CreateInputTarArchive(Stream inputStream, int blockFactor)
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
		public static TarArchive CreateOutputTarArchive(Stream outputStream, int blockFactor)
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
		/// Set the flag that determines whether existing files are
		/// kept, or overwritten during extraction.
		/// </summary>
		/// <param name="keepExistingFiles">
		/// If true, do not overwrite existing files.
		/// </param>
		public void SetKeepOldFiles(bool keepExistingFiles)
		{
			if (_isDisposed) {
				throw new ObjectDisposedException("TarArchive");
			}

			_keepOldFiles = keepExistingFiles;
		}

		/// <summary>
		/// Get/set the ascii file translation flag. If ascii file translation
		/// is true, then the file is checked to see if it a binary file or not. 
		/// If the flag is true and the test indicates it is ascii text 
		/// file, it will be translated. The translation converts the local
		/// operating system's concept of line ends into the UNIX line end,
		/// '\n', which is the defacto standard for a TAR archive. This makes
		/// text files compatible with UNIX.
		/// </summary>
		public bool AsciiTranslate {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}
				return _asciiTranslate;
			}

			set {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				_asciiTranslate = value;
			}

		}

		/// <summary>
		/// Set the ascii file translation flag.
		/// </summary>
		/// <param name= "translateAsciiFiles">
		/// If true, translate ascii text files.
		/// </param>
		[Obsolete("Use the AsciiTranslate property")]
		public void SetAsciiTranslation(bool translateAsciiFiles)
		{
			if (_isDisposed) {
				throw new ObjectDisposedException("TarArchive");
			}

			_asciiTranslate = translateAsciiFiles;
		}

		/// <summary>
		/// PathPrefix is added to entry names as they are written if the value is not null.
		/// A slash character is appended after PathPrefix 
		/// </summary>
		public string PathPrefix {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _pathPrefix;
			}

			set {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				_pathPrefix = value;
			}

		}

		/// <summary>
		/// RootPath is removed from entry names if it is found at the
		/// beginning of the name.
		/// </summary>
		public string RootPath {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _rootPath;
			}

			set {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}
				// Convert to forward slashes for matching. Trim trailing / for correct final path
				_rootPath = value.Replace('\\', '/').TrimEnd('/');
			}
		}

		/// <summary>
		/// Set user and group information that will be used to fill in the
		/// tar archive's entry headers. This information is based on that available 
		/// for the linux operating system, which is not always available on other
		/// operating systems.  TarArchive allows the programmer to specify values
		/// to be used in their place.
		/// <see cref="ApplyUserInfoOverrides"/> is set to true by this call.
		/// </summary>
		/// <param name="userId">
		/// The user id to use in the headers.
		/// </param>
		/// <param name="userName">
		/// The user name to use in the headers.
		/// </param>
		/// <param name="groupId">
		/// The group id to use in the headers.
		/// </param>
		/// <param name="groupName">
		/// The group name to use in the headers.
		/// </param>
		public void SetUserInfo(int userId, string userName, int groupId, string groupName)
		{
			if (_isDisposed) {
				throw new ObjectDisposedException("TarArchive");
			}

			_userId = userId;
			_userName = userName;
			_groupId = groupId;
			_groupName = groupName;
			_applyUserInfoOverrides = true;
		}

		/// <summary>
		/// Get or set a value indicating if overrides defined by <see cref="SetUserInfo">SetUserInfo</see> should be applied.
		/// </summary>
		/// <remarks>If overrides are not applied then the values as set in each header will be used.</remarks>
		public bool ApplyUserInfoOverrides {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _applyUserInfoOverrides;
			}

			set {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				_applyUserInfoOverrides = value;
			}
		}

		/// <summary>
		/// Get the archive user id.
		/// See <see cref="ApplyUserInfoOverrides">ApplyUserInfoOverrides</see> for detail
		/// on how to allow setting values on a per entry basis.
		/// </summary>
		/// <returns>
		/// The current user id.
		/// </returns>
		public int UserId {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _userId;
			}
		}

		/// <summary>
		/// Get the archive user name.
		/// See <see cref="ApplyUserInfoOverrides">ApplyUserInfoOverrides</see> for detail
		/// on how to allow setting values on a per entry basis.
		/// </summary>
		/// <returns>
		/// The current user name.
		/// </returns>
		public string UserName {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _userName;
			}
		}

		/// <summary>
		/// Get the archive group id.
		/// See <see cref="ApplyUserInfoOverrides">ApplyUserInfoOverrides</see> for detail
		/// on how to allow setting values on a per entry basis.
		/// </summary>
		/// <returns>
		/// The current group id.
		/// </returns>
		public int GroupId {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _groupId;
			}
		}

		/// <summary>
		/// Get the archive group name.
		/// See <see cref="ApplyUserInfoOverrides">ApplyUserInfoOverrides</see> for detail
		/// on how to allow setting values on a per entry basis.
		/// </summary>
		/// <returns>
		/// The current group name.
		/// </returns>
		public string GroupName {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				return _groupName;
			}
		}

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
		public int RecordSize {
			get {
				if (_isDisposed) {
					throw new ObjectDisposedException("TarArchive");
				}

				if (_tarIn != null) {
					return _tarIn.RecordSize;
				} else if (_tarOut != null) {
					return _tarOut.RecordSize;
				}
				return TarBuffer.DefaultRecordSize;
			}
		}

		/// <summary>
		/// Sets the IsStreamOwner property on the underlying stream.
		/// Set this to false to prevent the Close of the TarArchive from closing the stream.
		/// </summary>
		public bool IsStreamOwner {
			set {
				if (_tarIn != null) {
					_tarIn.IsStreamOwner = value;
				} else {
					_tarOut.IsStreamOwner = value;
				}
			}
		}

		/// <summary>
		/// Close the archive.
		/// </summary>
		[Obsolete("Use Close instead")]
		public void CloseArchive()
		{
			Close();
		}

		/// <summary>
		/// Perform the "list" command for the archive contents.
		/// 
		/// NOTE That this method uses the <see cref="ProgressMessageEvent"> progress event</see> to actually list
		/// the contents. If the progress display event is not set, nothing will be listed!
		/// </summary>
		public void ListContents()
		{
			if (_isDisposed) {
				throw new ObjectDisposedException("TarArchive");
			}

			while (true) {
				TarEntry entry = _tarIn.GetNextEntry();

				if (entry == null) {
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
		public void ExtractContents(string destinationDirectory)
		{
			if (_isDisposed) {
				throw new ObjectDisposedException("TarArchive");
			}

			while (true) {
				TarEntry entry = _tarIn.GetNextEntry();

				if (entry == null) {
					break;
				}

				if (entry.TarHeader.TypeFlag == TarHeader.LfLink || entry.TarHeader.TypeFlag == TarHeader.LfSymlink)
					continue;

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

			if (Path.IsPathRooted(name)) {
				// NOTE:
				// for UNC names...  \\machine\share\zoom\beet.txt gives \zoom\beet.txt
				name = name.Substring(Path.GetPathRoot(name).Length);
			}

			name = name.Replace('/', Path.DirectorySeparatorChar);

			var destFile = Path.Combine(destDir, name);

			byte[] rdbuf = new byte[32 * 1024];
			if (entry.IsDirectory) {
				EnsureDirectoryExists(destFile);
			} else {
				var parentDirectory = Path.GetDirectoryName(destFile);
				EnsureDirectoryExists(parentDirectory);

				var process = true;
				var fileInfo = new FileInfo(destFile);
				if (fileInfo.Exists) {
					if (_keepOldFiles) {
						OnProgressMessageEvent(entry, "Destination file already exists");
						process = false;
					} else if ((fileInfo.Attributes & FileAttributes.ReadOnly) != 0) {
						OnProgressMessageEvent(entry, "Destination file already exists, and is read-only");
						process = false;
					}
				}

				if (!process) return;
				var asciiTrans = false;

				Stream outputStream = File.Create(destFile);
				if (_asciiTranslate) {
					asciiTrans = !IsBinary(destFile);
				}

				StreamWriter outw = null;
				if (asciiTrans) {
					outw = new StreamWriter(outputStream);
				}

				while (true) {
					var numRead = _tarIn.Read(rdbuf, 0, rdbuf.Length);

					if (numRead <= 0) {
						break;
					}

					if (asciiTrans) {
						for (int off = 0, b = 0; b < numRead; ++b) {
							if (rdbuf[b] != 10) continue;
							var s = Encoding.ASCII.GetString(rdbuf, off, (b - off));
							outw.WriteLine(s);
							off = b + 1;
						}
					} else {
						outputStream.Write(rdbuf, 0, numRead);
					}
				}

				if (asciiTrans) {
					outw.Dispose();
				} else {
					outputStream.Dispose();
				}
			}
		}

		/// <summary>
		/// Write an entry to the archive. This method will call the putNextEntry
		/// and then write the contents of the entry, and finally call closeEntry()
		/// for entries that are files. For directories, it will call putNextEntry(),
		/// and then, if the recurse flag is true, process each entry that is a
		/// child of the directory.
		/// </summary>
		/// <param name="sourceEntry">
		/// The TarEntry representing the entry to write to the archive.
		/// </param>
		/// <param name="recurse">
		/// If true, process the children of directory entries.
		/// </param>
		public void WriteEntry(TarEntry sourceEntry, bool recurse)
		{
			if (sourceEntry == null) {
				throw new ArgumentNullException(nameof(sourceEntry));
			}

			if (_isDisposed) {
				throw new ObjectDisposedException("TarArchive");
			}

			try {
				if (recurse) {
					TarHeader.SetValueDefaults(sourceEntry.UserId, sourceEntry.UserName,
											   sourceEntry.GroupId, sourceEntry.GroupName);
				}
				WriteEntryCore(sourceEntry, recurse);
			} finally {
				if (recurse) {
					TarHeader.RestoreSetValues();
				}
			}
		}

		/// <summary>
		/// Write an entry to the archive. This method will call the putNextEntry
		/// and then write the contents of the entry, and finally call closeEntry()
		/// for entries that are files. For directories, it will call putNextEntry(),
		/// and then, if the recurse flag is true, process each entry that is a
		/// child of the directory.
		/// </summary>
		/// <param name="sourceEntry">
		/// The TarEntry representing the entry to write to the archive.
		/// </param>
		/// <param name="recurse">
		/// If true, process the children of directory entries.
		/// </param>
		void WriteEntryCore(TarEntry sourceEntry, bool recurse)
		{
			string tempFileName = null;
			var entryFilename = sourceEntry.File;

			var entry = (TarEntry)sourceEntry.Clone();

			if (_applyUserInfoOverrides) {
				entry.GroupId = _groupId;
				entry.GroupName = _groupName;
				entry.UserId = _userId;
				entry.UserName = _userName;
			}

			OnProgressMessageEvent(entry, null);

			if (_asciiTranslate && !entry.IsDirectory) {

				if (!IsBinary(entryFilename)) {
					tempFileName = Path.GetTempFileName();

					using (var inStream = File.OpenText(entryFilename)) {
						using (Stream outStream = File.Create(tempFileName)) {

							while (true) {
								var line = inStream.ReadLine();
								if (line == null) {
									break;
								}
								var data = Encoding.ASCII.GetBytes(line);
								outStream.Write(data, 0, data.Length);
								outStream.WriteByte((byte)'\n');
							}

							outStream.Flush();
						}
					}

					entry.Size = new FileInfo(tempFileName).Length;
					entryFilename = tempFileName;
				}
			}

			string newName = null;

			if (_rootPath != null) {
				if (entry.Name.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)) {
					newName = entry.Name.Substring(_rootPath.Length + 1);
				}
			}

			if (_pathPrefix != null) {
				newName = (newName == null) ? _pathPrefix + "/" + entry.Name : _pathPrefix + "/" + newName;
			}

			if (newName != null) {
				entry.Name = newName;
			}

			_tarOut.PutNextEntry(entry);

			if (entry.IsDirectory) {
				if (!recurse) return;
				var list = entry.GetDirectoryEntries();
				foreach (var t in list)
				{
					WriteEntryCore(t, true);
				}
			} else {
				using (Stream inputStream = File.OpenRead(entryFilename)) {
					var localBuffer = new byte[32 * 1024];
					while (true) {
						var numRead = inputStream.Read(localBuffer, 0, localBuffer.Length);

						if (numRead <= 0) {
							break;
						}

						_tarOut.Write(localBuffer, 0, numRead);
					}
				}

				if (!string.IsNullOrEmpty(tempFileName)) {
					File.Delete(tempFileName);
				}

				_tarOut.CloseEntry();
			}
		}

		/// <inheritdoc />
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the FileStream and optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing">true to release both managed and unmanaged resources;
		/// false to release only unmanaged resources.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (_isDisposed) return;
			_isDisposed = true;
			if (!disposing) return;
			if (_tarOut != null) {
				_tarOut.Flush();
				_tarOut.Dispose();
			}
			_tarIn?.Dispose();
		}

		/// <summary>
		/// Closes the archive and releases any associated resources.
		/// </summary>
		protected virtual void Close()
		{
			Dispose(true);
		}

		/// <summary>
		/// Ensures that resources are freed and other cleanup operations are performed
		/// when the garbage collector reclaims the <see cref="TarArchive"/>.
		/// </summary>
		~TarArchive()
		{
			Dispose(false);
		}

		static void EnsureDirectoryExists(string directoryName)
		{
			if (Directory.Exists(directoryName)) return;
			try {
				Directory.CreateDirectory(directoryName);
			} catch (Exception e) {
				throw new IOException("Exception creating directory '" + directoryName + "', " + e.Message, e);
			}
		}

		// TODO: TarArchive - Is there a better way to test for a text file?
		// It no longer reads entire files into memory but is still a weak test!
		// This assumes that byte values 0-7, 14-31 or 255 are binary
		// and that all non text files contain one of these values
		static bool IsBinary(string filename)
		{
			using (var fs = File.OpenRead(filename)) {
				var sampleSize = Math.Min(4096, (int)fs.Length);
				var content = new byte[sampleSize];

				var bytesRead = fs.Read(content, 0, sampleSize);

				for (var i = 0; i < bytesRead; ++i) {
					var b = content[i];
					if (b < 8 || b > 13 && b < 32 || b == 255) {
						return true;
					}
				}
			}
			return false;
		}

		#region Instance Fields
		bool _keepOldFiles;
		bool _asciiTranslate;

		int _userId;
		string _userName = string.Empty;
		int _groupId;
		string _groupName = string.Empty;

		string _rootPath;
		string _pathPrefix;

		bool _applyUserInfoOverrides;

		readonly TarInputStream _tarIn;
		readonly TarOutputStream _tarOut;
		bool _isDisposed;
		#endregion
	}
}
