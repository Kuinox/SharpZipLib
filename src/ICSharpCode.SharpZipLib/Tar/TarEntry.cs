using System;
using System.IO;
using static System.String;

namespace ICSharpCode.SharpZipLib.Tar
{
	/// <summary>
	/// This class represents an entry in a Tar archive. It consists
	/// of the entry's header, as well as the entry's File. Entries
	/// can be instantiated in one of three ways, depending on how
	/// they are to be used.
	/// <p>
	/// TarEntries that are created from the header bytes read from
	/// an archive are instantiated with the TarEntry( byte[] )
	/// constructor. These entries will be used when extracting from
	/// or listing the contents of an archive. These entries have their
	/// header filled in using the header bytes. They also set the File
	/// to null, since they reference an archive entry not a file.</p>
	/// <p>
	/// TarEntries that are created from files that are to be written
	/// into an archive are instantiated with the CreateEntryFromFile(string)
	/// pseudo constructor. These entries have their header filled in using
	/// the File's information. They also keep a reference to the File
	/// for convenience when writing entries.</p>
	/// <p>
	/// Finally, TarEntries can be constructed from nothing but a name.
	/// This allows the programmer to construct the entry by hand, for
	/// instance when only an InputStream is available for writing to
	/// the archive, and the header information is constructed from
	/// other information. In this case the header fields are set to
	/// defaults and the File is set to null.</p>
	/// <see cref="TarHeader"/>
	/// </summary>
	public class TarEntry
	{
		
		/// <summary>
		/// Initialise a default instance of <see cref="TarEntry"/>.
		/// </summary>
		TarEntry()
		{
			TarHeader = new TarHeader();
		}

		/// <summary>
		/// Construct an entry from an archive's header bytes. File is set
		/// to null.
		/// </summary>
		/// <param name = "headerBuffer">
		/// The header bytes from a tar archive entry.
		/// </param>
		public TarEntry(byte[] headerBuffer)
		{
			TarHeader = new TarHeader();
			TarHeader.ParseBuffer(headerBuffer);
		}

		/// <summary>
		/// Construct a TarEntry using the <paramref name="header">header</paramref> provided
		/// </summary>
		/// <param name="header">Header details for entry</param>
		public TarEntry(TarHeader header)
		{
			if (header == null) {
				throw new ArgumentNullException(nameof(header));
			}

			TarHeader = (TarHeader)header.Clone();
		}
		/// <summary>
		/// Clone this tar entry.
		/// </summary>
		/// <returns>Returns a clone of this entry.</returns>
		public object Clone()
		{
			var entry = new TarEntry
			{
				File = File,
				TarHeader = (TarHeader) TarHeader.Clone(),
				Name = Name
			};
			return entry;
		}

		/// <summary>
		/// Construct an entry with only a <paramref name="name">name</paramref>.
		/// This allows the programmer to construct the entry's header "by hand". 
		/// </summary>
		/// <param name="name">The name to use for the entry</param>
		/// <returns>Returns the newly created <see cref="TarEntry"/></returns>
		public static TarEntry CreateTarEntry(string name)
		{
			var entry = new TarEntry();
			NameTarHeader(entry.TarHeader, name);
			return entry;
		}

		/// <summary>
		/// Determine if the two entries are equal. Equality is determined
		/// by the header names being equal.
		/// </summary>
		/// <param name="obj">The <see cref="Object"/> to compare with the current Object.</param>
		/// <returns>
		/// True if the entries are equal; false if not.
		/// </returns>
		public override bool Equals(object obj)
		{
			if (obj is TarEntry localEntry)
			{
				return Name.Equals(localEntry.Name);
			}
			return false;
		}

		/// <summary>
		/// Derive a Hash value for the current <see cref="Object"/>
		/// </summary>
		/// <returns>A Hash code for the current <see cref="Object"/></returns>
		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		/// <summary>
		/// Get this entry's header.
		/// </summary>
		/// <returns>
		/// This entry's TarHeader.
		/// </returns>
		public TarHeader TarHeader { get; private set; }

		/// <summary>
		/// Get/Set this entry's name.
		/// </summary>
		public string Name {
			get => TarHeader.Name;
			set => TarHeader.Name = value;
		}

		/// <summary>
		/// Get/set this entry's user id.
		/// </summary>
		public int UserId {
			get => TarHeader.UserId;
			set => TarHeader.UserId = value;
		}

		/// <summary>
		/// Get/set this entry's group id.
		/// </summary>
		public int GroupId {
			get => TarHeader.GroupId;
			set => TarHeader.GroupId = value;
		}

		/// <summary>
		/// Get/set this entry's user name.
		/// </summary>
		public string UserName {
			get => TarHeader.UserName;
			set => TarHeader.UserName = value;
		}

		/// <summary>
		/// Get/set this entry's group name.
		/// </summary>
		public string GroupName {
			get => TarHeader.GroupName;
			set => TarHeader.GroupName = value;
		}

		/// <summary>
		/// Get/Set the modification time for this entry
		/// </summary>
		public DateTime ModTime {
			get => TarHeader.ModTime;
			set => TarHeader.ModTime = value;
		}

		/// <summary>
		/// Get this entry's file.
		/// </summary>
		/// <returns>
		/// This entry's file.
		/// </returns>
		public string File { get; private set; }

		/// <summary>
		/// Get/set this entry's recorded file size.
		/// </summary>
		public long Size {
			get => TarHeader.Size;
			set => TarHeader.Size = value;
		}

		/// <summary>
		/// Return true if this entry represents a directory, false otherwise
		/// </summary>
		/// <returns>
		/// True if this entry is a directory.
		/// </returns>
		public bool IsDirectory {
			get {
				if (File != null) {
					return Directory.Exists(File);
				}

				if (TarHeader != null) {
					if (TarHeader.TypeFlag == TarHeader.LfDir || Name.EndsWith("/", StringComparison.Ordinal)) {
						return true;
					}
				}
				return false;
			}
		}

		/// <summary>
		/// Write an entry's header information to a header buffer.
		/// </summary>
		/// <param name = "outBuffer">
		/// The tar entry header buffer to fill in.
		/// </param>
		public void WriteEntryHeader(byte[] outBuffer)
		{
			TarHeader.WriteHeader(outBuffer);
		}

		/// <summary>
		/// Fill in a TarHeader given only the entry's name.
		/// </summary>
		/// <param name="header">
		/// The TarHeader to fill in.
		/// </param>
		/// <param name="name">
		/// The tar entry name.
		/// </param>
		static void NameTarHeader(TarHeader header, string name)
		{
			if (header == null) {
				throw new ArgumentNullException(nameof(header));
			}

			if (name == null) {
				throw new ArgumentNullException(nameof(name));
			}

			bool isDir = name.EndsWith("/", StringComparison.Ordinal);

			header.Name = name;
			header.Mode = isDir ? 1003 : 33216;
			header.UserId = 0;
			header.GroupId = 0;
			header.Size = 0;

			header.ModTime = DateTime.UtcNow;

			header.TypeFlag = isDir ? TarHeader.LfDir : TarHeader.LfNormal;

			header.LinkName = Empty;
			header.UserName = Empty;
			header.GroupName = Empty;

			header.DevMajor = 0;
			header.DevMinor = 0;
		}
	}
}
