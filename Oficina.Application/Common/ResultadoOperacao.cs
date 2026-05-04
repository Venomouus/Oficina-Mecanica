namespace Oficina.Application.Common
{
    public enum TipoResultado
    {
        Sucesso,
        NaoEncontrado,
        DadosInvalidos,
        Conflito,
        NaoAutorizado
    }

    public class ResultadoOperacao
    {
        protected ResultadoOperacao(TipoResultado tipo, string? mensagem = null)
        {
            Tipo = tipo;
            Mensagem = mensagem;
        }

        public TipoResultado Tipo { get; }
        public string? Mensagem { get; }
        public bool Sucesso => Tipo == TipoResultado.Sucesso;

        public static ResultadoOperacao Ok() => new(TipoResultado.Sucesso);
        public static ResultadoOperacao NaoEncontrado(string? mensagem = null) => new(TipoResultado.NaoEncontrado, mensagem);
        public static ResultadoOperacao DadosInvalidos(string mensagem) => new(TipoResultado.DadosInvalidos, mensagem);
        public static ResultadoOperacao Conflito(string mensagem) => new(TipoResultado.Conflito, mensagem);
        public static ResultadoOperacao NaoAutorizado(string mensagem) => new(TipoResultado.NaoAutorizado, mensagem);
    }

    public class ResultadoOperacao<T> : ResultadoOperacao
    {
        private ResultadoOperacao(TipoResultado tipo, T? valor = default, string? mensagem = null)
            : base(tipo, mensagem)
        {
            Valor = valor;
        }

        public T? Valor { get; }

        public static ResultadoOperacao<T> Ok(T valor) => new(TipoResultado.Sucesso, valor);
        public new static ResultadoOperacao<T> NaoEncontrado(string? mensagem = null) => new(TipoResultado.NaoEncontrado, default, mensagem);
        public new static ResultadoOperacao<T> DadosInvalidos(string mensagem) => new(TipoResultado.DadosInvalidos, default, mensagem);
        public new static ResultadoOperacao<T> Conflito(string mensagem) => new(TipoResultado.Conflito, default, mensagem);
        public new static ResultadoOperacao<T> NaoAutorizado(string mensagem) => new(TipoResultado.NaoAutorizado, default, mensagem);
    }
}
