namespace FiapEsperancaSolidaria.Doacao.Infrastructure.Abstractions
{
    public interface ICorrelationIdAccessor
    {
        string CorrelationId { get; }
    }
}
