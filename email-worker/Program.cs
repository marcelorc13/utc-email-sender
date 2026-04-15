using DotNetEnv;
using Npgsql;
using MimeKit;
using MailKit.Net.Smtp;

Env.TraversePath().Load();

var connStr = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                    ?? throw new InvalidOperationException("DB_CONNECTION_STRING não definida.");
var smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST")
                    ?? throw new InvalidOperationException("SMTP_HOST não definida.");
var smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
var smtpUser = Environment.GetEnvironmentVariable("SMTP_USER")
                    ?? throw new InvalidOperationException("SMTP_USER não definida.");
var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    ?? throw new InvalidOperationException("SMTP_PASSWORD não definida.");
var destinatarios = (Environment.GetEnvironmentVariable("EMAIL_DESTINATARIOS")
                    ?? throw new InvalidOperationException("EMAIL_DESTINATARIOS não definida."))
                    .Split(',');
var weatherApiKey = Environment.GetEnvironmentVariable("WHEATHER_API_KEY")
                    ?? throw new InvalidOperationException("WHEATHER_API_KEY não definida.");

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

await using (var cmd = new NpgsqlCommand("LISTEN novo_relatorio;", conn))
    await cmd.ExecuteNonQueryAsync();

// Debounce: evita enviar múltiplos emails quando os 15 INSERTs disparam 15 NOTIFYs de uma vez
var lastEmailSent = DateTime.MinValue;

conn.Notification += (_, _) =>
{
    if ((DateTime.Now - lastEmailSent).TotalSeconds < 30)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] NOTIFY duplicado ignorado (debounce).");
        return;
    }
    lastEmailSent = DateTime.Now;

    Task.Run(async () =>
    {
        try
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Notificação recebida. Gerando relatório...");
            await using var queryConn = new NpgsqlConnection(connStr);
            await queryConn.OpenAsync();
            var html = await GerarHtmlAsync(queryConn);
            EnviarEmail(html, smtpHost, smtpPort, smtpUser, smtpPass, destinatarios);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Email enviado com sucesso.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] Erro ao enviar email: {ex.Message}");
        }
    });
};

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Buscando previsão do tempo via WeatherAPI...");
await FetchWeatherAsync(connStr, weatherApiKey);
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Worker ouvindo PostgreSQL... aguardando notificações.");

while (true)
    await conn.WaitAsync();

// Busca previsão na WeatherAPI e popula previsao_tempo para o dia de hoje
static async Task FetchWeatherAsync(string connStr, string apiKey)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

    await using var conn = new NpgsqlConnection(connStr);
    await conn.OpenAsync();

    var zones = new List<(int Id, string Cidade, string Query)>();
    await using (var cmd = new NpgsqlCommand(
        "SELECT id, cidade, weather_query FROM utc_zona ORDER BY id", conn))
    await using (var reader = await cmd.ExecuteReaderAsync())
        while (await reader.ReadAsync())
            zones.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));

    var periodos = new Dictionary<string, int[]>
    {
        ["manhã"]  = [7, 8, 9, 10, 11],
        ["tarde"]  = [12, 13, 14, 15, 16, 17],
        ["noite"]  = [18, 19, 20, 21, 22, 23]
    };

    foreach (var (zoneId, cidade, weatherQuery) in zones)
    {
        try
        {
            Console.WriteLine($"  → {cidade} ({weatherQuery})...");
            var url = $"https://api.weatherapi.com/v1/forecast.json"
                    + $"?key={apiKey}&q={weatherQuery}&days=1&aqi=no&alerts=no";
            var json = await http.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var hours = doc.RootElement
                           .GetProperty("forecast")
                           .GetProperty("forecastday")[0]
                           .GetProperty("hour");

            // Remove registros de hoje antes de inserir (idempotente em reinicializações)
            await using (var del = new NpgsqlCommand(
                "DELETE FROM previsao_tempo WHERE utc_id = @id AND data_hora::DATE = CURRENT_DATE",
                conn))
            {
                del.Parameters.AddWithValue("id", zoneId);
                await del.ExecuteNonQueryAsync();
            }

            foreach (var (periodo, range) in periodos)
            {
                double sumTemp = 0, sumHumid = 0;
                var condCounts = new Dictionary<string, int>();
                int count = 0;

                for (int i = 0; i < hours.GetArrayLength(); i++)
                {
                    var hour = hours[i];
                    var timeStr = hour.GetProperty("time").GetString()!;
                    var hourIdx = int.Parse(timeStr.Split(' ')[1].Split(':')[0]);
                    if (!range.Contains(hourIdx)) continue;

                    sumTemp  += hour.GetProperty("temp_c").GetDouble();
                    sumHumid += hour.GetProperty("humidity").GetDouble();
                    var cond  = hour.GetProperty("condition").GetProperty("text").GetString()!;
                    condCounts[cond] = condCounts.GetValueOrDefault(cond) + 1;
                    count++;
                }

                if (count == 0) continue;

                await using var ins = new NpgsqlCommand(@"
                    INSERT INTO previsao_tempo
                        (utc_id, data_hora, periodo, temperatura, condicao, umidade, descricao)
                    VALUES (@utcId, NOW(), @periodo, @temp, @cond, @humid, @desc)", conn);

                ins.Parameters.AddWithValue("utcId",   zoneId);
                ins.Parameters.AddWithValue("periodo",  periodo);
                ins.Parameters.AddWithValue("temp",     Math.Round(sumTemp / count, 1));
                ins.Parameters.AddWithValue("cond",     condCounts.MaxBy(kv => kv.Value).Key);
                ins.Parameters.AddWithValue("humid",    (int)Math.Round(sumHumid / count));
                ins.Parameters.AddWithValue("desc",
                    $"Dados via WeatherAPI — média de {count} horas ({periodo})");

                await ins.ExecuteNonQueryAsync();
                // trg_log_previsao e trg_notify_relatorio disparam automaticamente aqui
            }

            Console.WriteLine($"    OK: {cidade} — 3 períodos inseridos.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"    ERRO na zona {cidade}: {ex.Message}");
        }
    }
}

// Geração de HTML
static async Task<string> GerarHtmlAsync(NpgsqlConnection conn)
{
    var sb = new System.Text.StringBuilder();

    sb.AppendLine("<!DOCTYPE html>");
    sb.AppendLine("<html><head><meta charset='utf-8'></head><body>");
    sb.AppendLine("<h1>Relatório UTC — Jornal da Manhã</h1>");
    sb.AppendLine($"<p><em>Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}</em></p>");

    await using (var cmd = new NpgsqlCommand("SELECT * FROM fn_dados_relatorio();", conn))
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            sb.AppendLine("<hr/>");
            sb.AppendLine($"<h2>{reader["zona_nome"]} ({reader["zona_utc_offset"]})</h2>");
            sb.AppendLine($"<p><b>País:</b> {reader["pais"]} &mdash; <b>Cidade:</b> {reader["cidade"]}</p>");

            var desc = reader["zona_descricao"] as string;
            if (!string.IsNullOrWhiteSpace(desc))
                sb.AppendLine($"<p>{desc}</p>");

            sb.AppendLine(
                $"<p>" +
                $"<b>Período:</b> {reader["periodo"]} | " +
                $"<b>Temperatura:</b> {reader["temperatura"]}°C | " +
                $"<b>Condição:</b> {reader["condicao"]} | " +
                $"<b>Umidade:</b> {reader["umidade"]}%" +
                $"</p>"
            );
        }
    }

    sb.AppendLine("<hr/>");
    sb.AppendLine("<h3>Log de Alterações (hoje)</h3>");
    sb.AppendLine("<ul>");

    await using (var cmd2 = new NpgsqlCommand("SELECT * FROM fn_log_hoje();", conn))
    await using (var reader2 = await cmd2.ExecuteReaderAsync())
    {
        while (await reader2.ReadAsync())
        {
            sb.AppendLine(
                $"<li>[{reader2["operacao"]}] {reader2["tabela"]} &mdash; " +
                $"{reader2["detalhe"]} ({reader2["momento"]})</li>"
            );
        }
    }

    sb.AppendLine("</ul>");
    sb.AppendLine("</body></html>");

    return sb.ToString();
}

// Envio de email via SMTP
static void EnviarEmail(string html, string host, int port, string user, string pass, string[] dests)
{
    var message = new MimeMessage();
    message.From.Add(new MailboxAddress("Relatório UTC", user));

    foreach (var d in dests)
        message.To.Add(MailboxAddress.Parse(d.Trim()));

    message.Subject = $"Relatório UTC — Jornal da Manhã ({DateTime.Now:dd/MM/yyyy})";
    message.Body = new TextPart("html") { Text = html };

    using var client = new SmtpClient();
    client.Connect(host, port, MailKit.Security.SecureSocketOptions.StartTls);
    client.Authenticate(user, pass);
    client.Send(message);
    client.Disconnect(true);
}
