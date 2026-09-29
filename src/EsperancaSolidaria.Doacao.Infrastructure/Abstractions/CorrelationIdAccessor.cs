using Microsoft.AspNetCore.Http;

namespace FiapEsperancaSolidaria.Doacao.Infrastructure.Abstractions
{
    public class CorrelationIdAccessor : ICorrelationIdAccessor
    {
        public const string HeaderName = "X-Correlation-Id";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string CorrelationId =>
            _httpContextAccessor.HttpContext?.Items[HeaderName] as string ?? Guid.NewGuid().ToString();
    }
}
