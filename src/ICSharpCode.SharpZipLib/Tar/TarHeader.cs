using System;
using System.Text;

namespace ICSharpCode.SharpZipLib.Tar
{
	/// <summary>
	///     This class encapsulates the Tar Entry Header used in Tar Archives.
	///     The class also holds a number of tar constants, used mostly in headers.
	/// </summary>
	/// <remarks>
	///     The tar format and its POSIX successor PAX have a long history which makes for compatability
	///     issues when creating and reading files.
	///     This is further complicated by a large number of programs with variations on formats
	///     One common issue is the handling of names longer than 100 characters.
	///     GNU style long names are currently supported.
	///     This is the ustar (Posix 1003.1) header.
	///     struct header
	///     {
	///     char t_name[100];          //   0 Filename
	///     char t_mode[8];            // 100 Permissions
	///     char t_uid[8];             // 108 Numerical User ID
	///     char t_gid[8];             // 116 Numerical Group ID
	///     char t_size[12];           // 124 Filesize
	///     char t_mtime[12];          // 136 st_mtime
	///     char t_chksum[8];          // 148 Checksum
	///     char t_typeflag;           // 156 Type of File
	///     char t_linkname[100];      // 157 Target of Links
	///     char t_magic[6];           // 257 "ustar" or other...
	///     char t_version[2];         // 263 Version fixed to 00
	///     char t_uname[32];          // 265 User Name
	///     char t_gname[32];          // 297 Group Name
	///     char t_devmajor[8];        // 329 Major for devices
	///     char t_devminor[8];        // 337 Minor for devices
	///     char t_prefix[155];        // 345 Prefix for t_name
	///     char t_mfill[12];          // 500 Filler up to 512
	///     };
	/// </remarks>
	public class TarHeader
	{
		#region Constructors

		/// <summary>
		///     Initialise a default TarHeader instance
		/// </summary>
		public TarHeader()
		{
			Magic = Tmagic;
			Version = " ";

			Name = "";
			LinkName = "";

			UserId = _defaultUserId;
			GroupId = _defaultGroupId;
			UserName = _defaultUser;
			GroupName = _defaultGroupName;
			Size = 0;
		}

		#endregion

		#region ICloneable Members

		/// <summary>
		///     Create a new <see cref="TarHeader" /> that is a copy of the current instance.
		/// </summary>
		/// <returns>A new <see cref="object" /> that is a copy of the current instance.</returns>
		public object Clone()
		{
			return MemberwiseClone();
		}

		#endregion

		/// <summary>
		///     Parse TarHeader information from a header buffer.
		/// </summary>
		/// <param name="header">
		///     The tar entry header buffer to get information from.
		/// </param>
		public void ParseBuffer(byte[] header)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			var offset = 0;

			_name = ParseName(header, offset, Namelen).ToString();
			offset += Namelen;

			Mode = (int) ParseOctal(header, offset, Modelen);
			offset += Modelen;

			UserId = (int) ParseOctal(header, offset, Uidlen);
			offset += Uidlen;

			GroupId = (int) ParseOctal(header, offset, Gidlen);
			offset += Gidlen;

			Size = ParseBinaryOrOctal(header, offset, Sizelen);
			offset += Sizelen;

			ModTime = GetDateTimeFromCTime(ParseOctal(header, offset, Modtimelen));
			offset += Modtimelen;

			Checksum = (int) ParseOctal(header, offset, Chksumlen);
			offset += Chksumlen;

			TypeFlag = header[offset++];

			LinkName = ParseName(header, offset, Namelen).ToString();
			offset += Namelen;

			Magic = ParseName(header, offset, Magiclen).ToString();
			offset += Magiclen;

			if (Magic == "ustar")
			{
				Version = ParseName(header, offset, Versionlen).ToString();
				offset += Versionlen;

				UserName = ParseName(header, offset, Unamelen).ToString();
				offset += Unamelen;

				GroupName = ParseName(header, offset, Gnamelen).ToString();
				offset += Gnamelen;

				DevMajor = (int) ParseOctal(header, offset, Devlen);
				offset += Devlen;

				DevMinor = (int) ParseOctal(header, offset, Devlen);
				offset += Devlen;

				var prefix = ParseName(header, offset, Prefixlen).ToString();
				if (!string.IsNullOrEmpty(prefix)) Name = prefix + '/' + Name;
			}

			IsChecksumValid = Checksum == MakeCheckSum(header);
		}

		/// <summary>
		///     'Write' header information to buffer provided, updating the <see cref="Checksum">check sum</see>.
		/// </summary>
		/// <param name="outBuffer">output buffer for header information</param>
		public void WriteHeader(byte[] outBuffer)
		{
			if (outBuffer == null)
			{
				throw new ArgumentNullException(nameof(outBuffer));
			}

			var offset = 0;

			offset = GetNameBytes(Name, outBuffer, offset, Namelen);
			offset = GetOctalBytes(Mode, outBuffer, offset, Modelen);
			offset = GetOctalBytes(UserId, outBuffer, offset, Uidlen);
			offset = GetOctalBytes(GroupId, outBuffer, offset, Gidlen);

			offset = GetBinaryOrOctalBytes(Size, outBuffer, offset, Sizelen);
			offset = GetOctalBytes(GetCTime(ModTime), outBuffer, offset, Modtimelen);

			var csOffset = offset;
			for (var c = 0; c < Chksumlen; ++c) outBuffer[offset++] = (byte) ' ';

			outBuffer[offset++] = TypeFlag;

			offset = GetNameBytes(LinkName, outBuffer, offset, Namelen);
			offset = GetAsciiBytes(Magic, 0, outBuffer, offset, Magiclen);
			offset = GetNameBytes(Version, outBuffer, offset, Versionlen);
			offset = GetNameBytes(UserName, outBuffer, offset, Unamelen);
			offset = GetNameBytes(GroupName, outBuffer, offset, Gnamelen);

			if (TypeFlag == LfChr || TypeFlag == LfBlk)
			{
				offset = GetOctalBytes(DevMajor, outBuffer, offset, Devlen);
				offset = GetOctalBytes(DevMinor, outBuffer, offset, Devlen);
			}

			for (; offset < outBuffer.Length;) outBuffer[offset++] = 0;

			Checksum = ComputeCheckSum(outBuffer);

			GetCheckSumOctalBytes(Checksum, outBuffer, csOffset, Chksumlen);
			IsChecksumValid = true;
		}

		/// <summary>
		///     Get a hash code for the current object.
		/// </summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		/// <summary>
		///     Determines if this instance is equal to the specified object.
		/// </summary>
		/// <param name="obj">The object to compare with.</param>
		/// <returns>true if the objects are equal, false otherwise.</returns>
		public override bool Equals(object obj)
		{
			bool result;
			if (obj is TarHeader localHeader)
			{
				result = _name == localHeader._name
						 && Mode == localHeader.Mode
						 && UserId == localHeader.UserId
						 && GroupId == localHeader.GroupId
						 && Size == localHeader.Size
						 && ModTime == localHeader.ModTime
						 && Checksum == localHeader.Checksum
						 && TypeFlag == localHeader.TypeFlag
						 && LinkName == localHeader.LinkName
						 && Magic == localHeader.Magic
						 && Version == localHeader.Version
						 && UserName == localHeader.UserName
						 && GroupName == localHeader.GroupName
						 && DevMajor == localHeader.DevMajor
						 && DevMinor == localHeader.DevMinor;
			}
			else
			{
				result = false;
			}

			return result;
		}

		/// <summary>
		///     Set defaults for values used when constructing a TarHeader instance.
		/// </summary>
		/// <param name="userId">Value to apply as a default for userId.</param>
		/// <param name="userName">Value to apply as a default for userName.</param>
		/// <param name="groupId">Value to apply as a default for groupId.</param>
		/// <param name="groupName">Value to apply as a default for groupName.</param>
		internal static void SetValueDefaults(int userId, string userName, int groupId, string groupName)
		{
			_defaultUserId = _userIdAsSet = userId;
			_defaultUser = _userNameAsSet = userName;
			_defaultGroupId = _groupIdAsSet = groupId;
			_defaultGroupName = _groupNameAsSet = groupName;
		}

		internal static void RestoreSetValues()
		{
			_defaultUserId = _userIdAsSet;
			_defaultUser = _userNameAsSet;
			_defaultGroupId = _groupIdAsSet;
			_defaultGroupName = _groupNameAsSet;
		}

		// Return value that may be stored in octal or binary. Length must exceed 8.
		//
		static long ParseBinaryOrOctal(byte[] header, int offset, int length)
		{
			if (header[offset] >= 0x80)
			{
				// File sizes over 8GB are stored in 8 right-justified bytes of binary indicated by setting the high-order bit of the leftmost byte of a numeric field.
				long result = 0;
				for (var pos = length - 8; pos < length; pos++) result = (result << 8) | header[offset + pos];
				return result;
			}

			return ParseOctal(header, offset, length);
		}

		/// <summary>
		///     Parse an octal string from a header buffer.
		/// </summary>
		/// <param name="header">The header buffer from which to parse.</param>
		/// <param name="offset">The offset into the buffer from which to parse.</param>
		/// <param name="length">The number of header bytes to parse.</param>
		/// <returns>The long equivalent of the octal string.</returns>
		public static long ParseOctal(byte[] header, int offset, int length)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			long result = 0;
			var stillPadding = true;

			var end = offset + length;
			for (var i = offset; i < end; ++i)
			{
				if (header[i] == 0)
				{
					break;
				}

				if (header[i] == (byte) ' ' || header[i] == '0')
				{
					if (stillPadding)
					{
						continue;
					}

					if (header[i] == (byte) ' ')
					{
						break;
					}
				}

				stillPadding = false;

				result = (result << 3) + (header[i] - '0');
			}

			return result;
		}

		/// <summary>
		///     Parse a name from a header buffer.
		/// </summary>
		/// <param name="header">
		///     The header buffer from which to parse.
		/// </param>
		/// <param name="offset">
		///     The offset into the buffer from which to parse.
		/// </param>
		/// <param name="length">
		///     The number of header bytes to parse.
		/// </param>
		/// <returns>
		///     The name parsed.
		/// </returns>
		public static StringBuilder ParseName(byte[] header, int offset, int length)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset), "Cannot be less than zero");
			}

			if (length < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(length), "Cannot be less than zero");
			}

			if (offset + length > header.Length)
			{
				throw new ArgumentException("Exceeds header size", nameof(length));
			}

			var result = new StringBuilder(length);

			for (var i = offset; i < offset + length; ++i)
			{
				if (header[i] == 0)
				{
					break;
				}

				result.Append((char) header[i]);
			}

			return result;
		}

		/// <summary>
		///     Add <paramref name="name">name</paramref> to the buffer as a collection of bytes
		/// </summary>
		/// <param name="name">The name to add</param>
		/// <param name="nameOffset">The offset of the first character</param>
		/// <param name="buffer">The buffer to add to</param>
		/// <param name="bufferOffset">The index of the first byte to add</param>
		/// <param name="length">The number of characters/bytes to add</param>
		/// <returns>The next free index in the <paramref name="buffer" /></returns>
		public static int GetNameBytes(StringBuilder name, int nameOffset, byte[] buffer, int bufferOffset, int length)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			return GetNameBytes(name.ToString(), nameOffset, buffer, bufferOffset, length);
		}

		/// <summary>
		///     Add <paramref name="name">name</paramref> to the buffer as a collection of bytes
		/// </summary>
		/// <param name="name">The name to add</param>
		/// <param name="nameOffset">The offset of the first character</param>
		/// <param name="buffer">The buffer to add to</param>
		/// <param name="bufferOffset">The index of the first byte to add</param>
		/// <param name="length">The number of characters/bytes to add</param>
		/// <returns>The next free index in the <paramref name="buffer" /></returns>
		public static int GetNameBytes(string name, int nameOffset, byte[] buffer, int bufferOffset, int length)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			int i;

			for (i = 0; i < length && nameOffset + i < name.Length; ++i) buffer[bufferOffset + i] = (byte) name[nameOffset + i];

			for (; i < length; ++i) buffer[bufferOffset + i] = 0;

			return bufferOffset + length;
		}

		/// <summary>
		///     Add an entry name to the buffer
		/// </summary>
		/// <param name="name">
		///     The name to add
		/// </param>
		/// <param name="buffer">
		///     The buffer to add to
		/// </param>
		/// <param name="offset">
		///     The offset into the buffer from which to start adding
		/// </param>
		/// <param name="length">
		///     The number of header bytes to add
		/// </param>
		/// <returns>
		///     The index of the next free byte in the buffer
		/// </returns>
		public static int GetNameBytes(StringBuilder name, byte[] buffer, int offset, int length)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			return GetNameBytes(name.ToString(), 0, buffer, offset, length);
		}

		/// <summary>
		///     Add an entry name to the buffer
		/// </summary>
		/// <param name="name">The name to add</param>
		/// <param name="buffer">The buffer to add to</param>
		/// <param name="offset">The offset into the buffer from which to start adding</param>
		/// <param name="length">The number of header bytes to add</param>
		/// <returns>The index of the next free byte in the buffer</returns>
		public static int GetNameBytes(string name, byte[] buffer, int offset, int length)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			return GetNameBytes(name, 0, buffer, offset, length);
		}

		/// <summary>
		///     Add a string to a buffer as a collection of ascii bytes.
		/// </summary>
		/// <param name="toAdd">The string to add</param>
		/// <param name="nameOffset">The offset of the first character to add.</param>
		/// <param name="buffer">The buffer to add to.</param>
		/// <param name="bufferOffset">The offset to start adding at.</param>
		/// <param name="length">The number of ascii characters to add.</param>
		/// <returns>The next free index in the buffer.</returns>
		public static int GetAsciiBytes(string toAdd, int nameOffset, byte[] buffer, int bufferOffset, int length)
		{
			if (toAdd == null)
			{
				throw new ArgumentNullException(nameof(toAdd));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			int i;
			for (i = 0; i < length && nameOffset + i < toAdd.Length; ++i)
				buffer[bufferOffset + i] = (byte) toAdd[nameOffset + i];
			// If length is beyond the toAdd string length (which is OK by the prev loop condition), eg if a field has fixed length and the string is shorter, make sure all of the extra chars are written as NULLs, so that the reader func would ignore them and get back the original string
			for (; i < length; ++i)
				buffer[bufferOffset + i] = 0;
			return bufferOffset + length;
		}

		/// <summary>
		///     Put an octal representation of a value into a buffer
		/// </summary>
		/// <param name="value">
		///     the value to be converted to octal
		/// </param>
		/// <param name="buffer">
		///     buffer to store the octal string
		/// </param>
		/// <param name="offset">
		///     The offset into the buffer where the value starts
		/// </param>
		/// <param name="length">
		///     The length of the octal string to create
		/// </param>
		/// <returns>
		///     The offset of the character next byte after the octal string
		/// </returns>
		public static int GetOctalBytes(long value, byte[] buffer, int offset, int length)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			var localIndex = length - 1;

			// Either a space or null is valid here.  We use NULL as per GNUTar
			buffer[offset + localIndex] = 0;
			--localIndex;

			if (value > 0)
			{
				for (var v = value; localIndex >= 0 && v > 0; --localIndex)
				{
					buffer[offset + localIndex] = (byte) ((byte) '0' + (byte) (v & 7));
					v >>= 3;
				}
			}

			for (; localIndex >= 0; --localIndex) buffer[offset + localIndex] = (byte) '0';

			return offset + length;
		}

		/// <summary>
		///     Put an octal or binary representation of a value into a buffer
		/// </summary>
		/// <param name="value">Value to be convert to octal</param>
		/// <param name="buffer">The buffer to update</param>
		/// <param name="offset">The offset into the buffer to store the value</param>
		/// <param name="length">The length of the octal string. Must be 12.</param>
		/// <returns>Index of next byte</returns>
		static int GetBinaryOrOctalBytes(long value, byte[] buffer, int offset, int length)
		{
			if (value > 0x1FFFFFFFF)
			{
				// Octal 77777777777 (11 digits)
				// Put value as binary, right-justified into the buffer. Set high order bit of left-most byte.
				for (var pos = length - 1; pos > 0; pos--)
				{
					buffer[offset + pos] = (byte) value;
					value = value >> 8;
				}

				buffer[offset] = 0x80;
				return offset + length;
			}

			return GetOctalBytes(value, buffer, offset, length);
		}

		/// <summary>
		///     Add the checksum integer to header buffer.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="buffer">The header buffer to set the checksum for</param>
		/// <param name="offset">The offset into the buffer for the checksum</param>
		/// <param name="length">
		///     The number of header bytes to update.
		///     It's formatted differently from the other fields: it has 6 digits, a
		///     null, then a space -- rather than digits, a space, then a null.
		///     The final space is already there, from checksumming
		/// </param>
		/// <returns>The modified buffer offset</returns>
		static void GetCheckSumOctalBytes(long value, byte[] buffer, int offset, int length)
		{
			GetOctalBytes(value, buffer, offset, length - 1);
		}

		/// <summary>
		///     Compute the checksum for a tar entry header.
		///     The checksum field must be all spaces prior to this happening
		/// </summary>
		/// <param name="buffer">The tar entry's header buffer.</param>
		/// <returns>The computed checksum.</returns>
		static int ComputeCheckSum(byte[] buffer)
		{
			var sum = 0;
			for (var i = 0; i < buffer.Length; ++i) sum += buffer[i];
			return sum;
		}

		/// <summary>
		///     Make a checksum for a tar entry ignoring the checksum contents.
		/// </summary>
		/// <param name="buffer">The tar entry's header buffer.</param>
		/// <returns>The checksum for the buffer</returns>
		static int MakeCheckSum(byte[] buffer)
		{
			var sum = 0;
			for (var i = 0; i < Chksumofs; ++i) sum += buffer[i];

			for (var i = 0; i < Chksumlen; ++i) sum += (byte) ' ';

			for (var i = Chksumofs + Chksumlen; i < buffer.Length; ++i) sum += buffer[i];
			return sum;
		}

		static int GetCTime(DateTime dateTime)
		{
			return unchecked((int) ((dateTime.Ticks - DateTime1970.Ticks) / TimeConversionFactor));
		}

		static DateTime GetDateTimeFromCTime(long ticks)
		{
			DateTime result;

			try
			{
				result = new DateTime(DateTime1970.Ticks + ticks * TimeConversionFactor);
			}
			catch (ArgumentOutOfRangeException)
			{
				result = DateTime1970;
			}

			return result;
		}

		#region Constants

		/// <summary>
		///     The length of the name field in a header buffer.
		/// </summary>
		public const int Namelen = 100;

		/// <summary>
		///     The length of the mode field in a header buffer.
		/// </summary>
		const int Modelen = 8;

		/// <summary>
		///     The length of the user id field in a header buffer.
		/// </summary>
		const int Uidlen = 8;

		/// <summary>
		///     The length of the group id field in a header buffer.
		/// </summary>
		const int Gidlen = 8;

		/// <summary>
		///     The length of the checksum field in a header buffer.
		/// </summary>
		const int Chksumlen = 8;

		/// <summary>
		///     Offset of checksum in a header buffer.
		/// </summary>
		const int Chksumofs = 148;

		/// <summary>
		///     The length of the size field in a header buffer.
		/// </summary>
		const int Sizelen = 12;

		/// <summary>
		///     The length of the magic field in a header buffer.
		/// </summary>
		const int Magiclen = 6;

		/// <summary>
		///     The length of the version field in a header buffer.
		/// </summary>
		const int Versionlen = 2;

		/// <summary>
		///     The length of the modification time field in a header buffer.
		/// </summary>
		const int Modtimelen = 12;

		/// <summary>
		///     The length of the user name field in a header buffer.
		/// </summary>
		const int Unamelen = 32;

		/// <summary>
		///     The length of the group name field in a header buffer.
		/// </summary>
		const int Gnamelen = 32;

		/// <summary>
		///     The length of the devices field in a header buffer.
		/// </summary>
		const int Devlen = 8;

		/// <summary>
		///     The length of the name prefix field in a header buffer.
		/// </summary>
		const int Prefixlen = 155;

		//
		// LF_ constants represent the "type" of an entry
		//

		/// <summary>
		///     The "old way" of indicating a normal file.
		/// </summary>
		public const byte LfOldnorm = 0;

		/// <summary>
		///     Normal file type.
		/// </summary>
		public const byte LfNormal = (byte) '0';

		/// <summary>
		///     Link file type.
		/// </summary>
		public const byte LfLink = (byte) '1';

		/// <summary>
		///     Symbolic link file type.
		/// </summary>
		public const byte LfSymlink = (byte) '2';

		/// <summary>
		///     Character device file type.
		/// </summary>
		const byte LfChr = (byte) '3';

		/// <summary>
		///     Block device file type.
		/// </summary>
		const byte LfBlk = (byte) '4';

		/// <summary>
		///     Directory file type.
		/// </summary>
		public const byte LfDir = (byte) '5';

		/// <summary>
		///     FIFO (pipe) file type.
		/// </summary>
		public const byte LfFifo = (byte) '6';

		/// <summary>
		///     Contiguous file type.
		/// </summary>
		public const byte LfContig = (byte) '7';

		/// <summary>
		///     Posix.1 2001 global extended header
		/// </summary>
		public const byte LfGhdr = (byte) 'g';

		/// <summary>
		///     Posix.1 2001 extended header
		/// </summary>
		public const byte LfXhdr = (byte) 'x';

		// POSIX allows for upper case ascii type as extensions

		/// <summary>
		///     Solaris access control list file type
		/// </summary>
		public const byte LfAcl = (byte) 'A';

		/// <summary>
		///     GNU dir dump file type
		///     This is a dir entry that contains the names of files that were in the
		///     dir at the time the dump was made
		/// </summary>
		public const byte LfGnuDumpdir = (byte) 'D';

		/// <summary>
		///     Solaris Extended Attribute File
		/// </summary>
		public const byte LfExtattr = (byte) 'E';

		/// <summary>
		///     Inode (metadata only) no file content
		/// </summary>
		public const byte LfMeta = (byte) 'I';

		/// <summary>
		///     Identifies the next file on the tape as having a long link name
		/// </summary>
		public const byte LfGnuLonglink = (byte) 'K';

		/// <summary>
		///     Identifies the next file on the tape as having a long name
		/// </summary>
		public const byte LfGnuLongname = (byte) 'L';

		/// <summary>
		///     Continuation of a file that began on another volume
		/// </summary>
		public const byte LfGnuMultivol = (byte) 'M';

		/// <summary>
		///     For storing filenames that dont fit in the main header (old GNU)
		/// </summary>
		public const byte LfGnuNames = (byte) 'N';

		/// <summary>
		///     GNU Sparse file
		/// </summary>
		public const byte LfGnuSparse = (byte) 'S';

		/// <summary>
		///     GNU Tape/volume header ignore on extraction
		/// </summary>
		public const byte LfGnuVolhdr = (byte) 'V';

		/// <summary>
		///     The magic tag representing a POSIX tar archive.  (would be written with a trailing NULL)
		/// </summary>
		const string Tmagic = "ustar";

		/// <summary>
		///     The magic tag representing an old GNU tar archive where version is included in magic and overwrites it
		/// </summary>
		public const string GnuTmagic = "ustar  ";

		const long TimeConversionFactor = 10000000L; // 1 tick == 100 nanoseconds
		static readonly DateTime DateTime1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		#endregion

		#region Properties

		/// <summary>
		///     Get/set the name for this tar entry.
		/// </summary>
		/// <exception cref="ArgumentNullException">Thrown when attempting to set the property to null.</exception>
		public string Name
		{
			get => _name;
			set => _name = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <summary>
		///     Get/set the entry's Unix style permission mode.
		/// </summary>
		public int Mode { get; set; }


		/// <summary>
		///     The entry's user id.
		/// </summary>
		/// <remarks>
		///     This is only directly relevant to unix systems.
		///     The default is zero.
		/// </remarks>
		public int UserId { get; set; }


		/// <summary>
		///     Get/set the entry's group id.
		/// </summary>
		/// <remarks>
		///     This is only directly relevant to linux/unix systems.
		///     The default value is zero.
		/// </remarks>
		public int GroupId { get; set; }


		/// <summary>
		///     Get/set the entry's size.
		/// </summary>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when setting the size to less than zero.</exception>
		public long Size
		{
			get => _size;
			set
			{
				if (value < 0)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "Cannot be less than zero");
				}

				_size = value;
			}
		}


		/// <summary>
		///     Get/set the entry's modification time.
		/// </summary>
		/// <remarks>
		///     The modification time is only accurate to within a second.
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when setting the date time to less than 1/1/1970.</exception>
		public DateTime ModTime
		{
			get => _modTime;
			set
			{
				if (value < DateTime1970)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "ModTime cannot be before Jan 1st 1970");
				}

				_modTime = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second);
			}
		}


		/// <summary>
		///     Get the entry's checksum.  This is only valid/updated after writing or reading an entry.
		/// </summary>
		public int Checksum { get; private set; }


		/// <summary>
		///     Get value of true if the header checksum is valid, false otherwise.
		/// </summary>
		public bool IsChecksumValid { get; private set; }


		/// <summary>
		///     Get/set the entry's type flag.
		/// </summary>
		public byte TypeFlag { get; set; }


		/// <summary>
		///     The entry's link name.
		/// </summary>
		/// <exception cref="ArgumentNullException">Thrown when attempting to set LinkName to null.</exception>
		public string LinkName
		{
			get => _linkName;
			set => _linkName = value ?? throw new ArgumentNullException(nameof(value));
		}


		/// <summary>
		///     Get/set the entry's magic tag.
		/// </summary>
		/// <exception cref="ArgumentNullException">Thrown when attempting to set Magic to null.</exception>
		public string Magic
		{
			get => _magic;
			set => _magic = value ?? throw new ArgumentNullException(nameof(value));
		}


		/// <summary>
		///     The entry's version.
		/// </summary>
		/// <exception cref="ArgumentNullException">Thrown when attempting to set Version to null.</exception>
		public string Version
		{
			get => _version;

			set => _version = value ?? throw new ArgumentNullException(nameof(value));
		}


		/// <summary>
		///     The entry's user name.
		/// </summary>
		public string UserName
		{
			get => _userName;
			set
			{
				if (value != null)
				{
					_userName = value.Substring(0, Math.Min(Unamelen, value.Length));
				}
				else
				{
					var currentUser = "user";
					if (currentUser.Length > Unamelen)
					{
						currentUser = currentUser.Substring(0, Unamelen);
					}

					_userName = currentUser;
				}
			}
		}


		/// <summary>
		///     Get/set the entry's group name.
		/// </summary>
		/// <remarks>
		///     This is only directly relevant to unix systems.
		/// </remarks>
		public string GroupName
		{
			get => _groupName;
			set => _groupName = value ?? "None";
		}


		/// <summary>
		///     Get/set the entry's major device number.
		/// </summary>
		public int DevMajor { get; set; }


		/// <summary>
		///     Get/set the entry's minor device number.
		/// </summary>
		public int DevMinor { get; set; }

		#endregion

		#region Instance Fields

		string _name;
		long _size;
		DateTime _modTime;
		string _linkName;
		string _magic;
		string _version;
		string _userName;
		string _groupName;

		#endregion

		#region Class Fields

		// Values used during recursive operations.
		static int _userIdAsSet;
		static int _groupIdAsSet;
		static string _userNameAsSet;
		static string _groupNameAsSet = "None";

		static int _defaultUserId;
		static int _defaultGroupId;
		static string _defaultGroupName = "None";
		static string _defaultUser;

		#endregion
	}
}
