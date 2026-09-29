using System.Text;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace EsperancaSolidaria.Doacao.Worker.Logging;

/// <summary>
/// Formatador unico da saida do Worker. Toda linha sai com a etiqueta <c>[doacao-worker]</c>,
/// inclusive as do proprio ASP.NET Core e do host — por isso a marcacao vive aqui, e nao
/// concatenada em cada chamada de log, onde bastaria alguem esquecer para abrir um buraco.
/// </summary>
/// <remarks>
/// Formato: <c>&lt;timestamp&gt; &lt;nivel&gt; [doacao-worker] &lt;categoria&gt;: &lt;mensagem&gt; {escopos}</c>.
/// Os escopos carregam <c>DonationId</c> e <c>CorrelationId</c>, abertos por mensagem no
/// <see cref="DonationPaymentWorker"/>: e o que permite seguir uma doacao de ponta a ponta.
/// </remarks>
internal sealed class DoacaoWorkerConsoleFormatter : ConsoleFormatter, IDisposable
{
    /// <summary>Nome com que o formatador e selecionado em <c>AddConsole</c>.</summary>
    public const string FormatterName = "doacao-worker";

    private const string Tag = "[doacao-worker]";
    private const string DefaultTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly IDisposable? optionsReloadToken;
    private ConsoleFormatterOptions options;

    public DoacaoWorkerConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> optionsMonitor)
        : base(FormatterName)
    {
        options = optionsMonitor.CurrentValue;
        optionsReloadToken = optionsMonitor.OnChange(updated => options = updated);
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);

        if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
        {
            return;
        }

        var line = new StringBuilder()
            .Append(Timestamp())
            .Append(' ')
            .Append(Abbreviate(logEntry.LogLevel))
            .Append(' ')
            .Append(Tag)
            .Append(' ')
            .Append(ShortCategory(logEntry.Category))
            .Append(": ")
            .Append(message);

        AppendScopes(line, scopeProvider);

        if (logEntry.Exception is not null)
        {
            line.Append(Environment.NewLine).Append(logEntry.Exception);
        }

        textWriter.WriteLine(line.ToString());
    }

    public void Dispose() => optionsReloadToken?.Dispose();

    private string Timestamp()
    {
        var format = string.IsNullOrWhiteSpace(options.TimestampFormat)
            ? DefaultTimestampFormat
            : options.TimestampFormat;

        var now = options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        return now.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Nivel em largura fixa, para as colunas nao dancarem entre as linhas.</summary>
    private static string Abbreviate(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "NONE ",
    };

    /// <summary>Só o nome da classe: o namespace completo empurraria a mensagem para longe.</summary>
    private static string ShortCategory(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot < 0 ? category : category[(lastDot + 1)..];
    }

    private void AppendScopes(StringBuilder line, IExternalScopeProvider? scopeProvider)
    {
        if (!options.IncludeScopes || scopeProvider is null)
        {
            return;
        }

        var properties = new List<string>();

        scopeProvider.ForEachScope(
            (scope, collected) =>
            {
                switch (scope)
                {
                    case IEnumerable<KeyValuePair<string, object>> pairs:
                        foreach (var pair in pairs)
                        {
                            collected.Add($"{pair.Key}={pair.Value}");
                        }

                        break;

                    case not null:
                        collected.Add(scope.ToString() ?? string.Empty);
                        break;
                }
            },
            properties);

        if (properties.Count > 0)
        {
            line.Append(" {").Append(string.Join(", ", properties)).Append('}');
        }
    }
}
