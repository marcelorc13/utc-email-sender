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

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

await using (var cmd = new NpgsqlCommand("LISTEN novo_relatorio;", conn))
    await cmd.ExecuteNonQueryAsync();

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Worker ouvindo PostgreSQL... aguardando notificações.");

// Espera por uma notificação no postgres
conn.Notification += (_, _) =>
{
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

while (true)
    await conn.WaitAsync();

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

            var img = reader["link_imagem"] as string;
            if (!string.IsNullOrWhiteSpace(img))
                sb.AppendLine($"<p><a href=\"{img}\">Ver imagem da região</a></p>");

            var vid = reader["link_video"] as string;
            if (!string.IsNullOrWhiteSpace(vid))
                sb.AppendLine($"<p><a href=\"{vid}\">Ver vídeo da região</a></p>");
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
