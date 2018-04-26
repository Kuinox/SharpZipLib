namespace ICSharpCode.SharpZipLib.Tar
{
	/// <inheritdoc />
	/// <summary>
	/// TarException represents exceptions specific to Tar classes and code.
	/// </summary>
	public class TarException : SharpZipBaseException
	{
		/// <inheritdoc />
		/// <summary>
		/// Initialise a new instance of <see cref="T:ICSharpCode.SharpZipLib.Tar.TarException" /> with its message string.
		/// </summary>
		/// <param name="message">A <see cref="T:System.String" /> that describes the error.</param>
		public TarException(string message)
			: base(message)
		{
		}
	}
}
