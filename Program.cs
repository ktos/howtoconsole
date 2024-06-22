using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

IConfiguration configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables("HOWTO")
    .AddJsonFile("howtoconsole.appsettings.json", optional: true)
    .AddUserSecrets<Program>()
    .Build();

string responseContent = "";

var shell = (ParentProcessUtilities.GetParentProcess()?.ProcessName ?? string.Empty) switch
{
    "cmd" => "Windows Command Prompt",
    "pwsh" => "PowerShell",
    "bash" => "bash",
    _ => configuration.GetSection("DefaultShell").Value ?? "Windows Command Prompt",
};

if (args.Length > 0)
{
    await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync(
            "Thinking...",
            async ctx =>
            {
                var prompt =
                    $"Show me how to {string.Join(" ", args)} in {shell}. Write a one-liner and a very short description.";

                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    configuration.GetSection("ApiKey").Value ?? ""
                );

                var json =
                    "{\"model\": \""
                    + (configuration.GetSection("Model").Value ?? "mistral")
                    + "\", \"messages\": [{\"role\": \"user\", \"content\": \""
                    + prompt
                    + "\"}], \"temperature\": 0.7}";

                var content = new StringContent(
                    json,
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                try
                {
                    var response = await client.PostAsync(
                        configuration.GetSection("Url").Value
                            ?? "https://api.openai.com/v1/chat/completions",
                        content
                    );
                    var result = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var start = "{\"role\":\"assistant\",\"content\":\"";
                        var stop = "\"finish_reason\":\"stop\"";

                        var startPos = result.IndexOf(start);
                        var stopPos = result.IndexOf(stop);

                        responseContent = result
                            .Substring(
                                startPos + start.Length,
                                stopPos - startPos - stop.Length - 12
                            )
                            .Replace("\\n", "\n")
                            .Trim();
                    }
                    else
                    {
                        AnsiConsole.MarkupLine(
                            $"[red]Error during AI communication.[/] ({response.StatusCode})"
                        );
                    }
                }
                catch (Exception e)
                {
                    AnsiConsole.MarkupLine($"[red]Error during AI communication.[/] ({e.Message})");
                }
            }
        );
}

Console.WriteLine(responseContent);
