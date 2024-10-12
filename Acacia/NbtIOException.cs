namespace Acacia;

public class NbtIOException(string message, Exception innerException) : IOException(message, innerException);