namespace ICSharpCode.SharpZipLib.Tar
{
	/// <inheritdoc />
	/// <summary>
	/// This exception is used to indicate that there is a problem
	/// with a TAR archive header.
	/// </summary>
	public class InvalidHeaderException : TarException
	{
		/// <inheritdoc />
		/// <summary>
		/// Initialises a new instance of the InvalidHeaderException class with a specified message.
		/// </summary>
		/// <param name="message">Message describing the exception cause.</param>
		public InvalidHeaderException(string message)
			: base(message)
		{
		}
	}
}
