namespace Oficina.Application.Common;

public sealed class ConflitoConcorrenciaException(string message, Exception innerException)
    : Exception(message, innerException);
