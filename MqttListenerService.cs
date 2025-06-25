using System.Diagnostics;
using System.Text.Json;
using MQTTnet;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TextCopy;

namespace AuthCodeListener;

public class MqttListenerService : BackgroundService
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _mqttOptions;
    private readonly MqttConfig _mqttConfig;
    private readonly JsonSerializerOptions _jsonOptions;

    private const string NotificationTitle = "Verification Code Listener";

    public MqttListenerService(IOptions<MqttConfig> mqttConfig)
    {
        _mqttConfig = mqttConfig.Value;

        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += HandleMessage;

        _mqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_mqttConfig.Endpoint, _mqttConfig.Port)
            .WithCredentials(_mqttConfig.Username, _mqttConfig.Password)
            .WithCleanSession()
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting verification code listener service...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    Console.WriteLine($"Connecting to MQTT broker {_mqttConfig.Endpoint}:{_mqttConfig.Port}...");
                    await _client.ConnectAsync(_mqttOptions, cancellationToken);
                    await _client.SubscribeAsync(_mqttConfig.Topic, cancellationToken: cancellationToken);
                    Console.WriteLine($"Subscribed to topic '{_mqttConfig.Topic}'");
                }
            }
            catch (OperationCanceledException) { } // do nothing
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopping verification code listener service...");
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleMessage(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            string payloadAsString = args.ApplicationMessage.ConvertPayloadToString();
            var payload = JsonSerializer.Deserialize<AuthCodePayload>(payloadAsString, _jsonOptions);
            if (payload == null)
            {
                Console.WriteLine("Invalid message payload.");
                return;
            }

            Console.WriteLine($"[{_mqttConfig.Topic}] Received code {payload.Code} from {payload.Source}");
            await ClipboardService.SetTextAsync(payload.Code);

            SendNotification($"Copied code {payload.Code} from {payload.Source}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling message: {ex}");
        }
    }

    private void SendNotification(string body)
    {
        if (OperatingSystem.IsLinux())
        {
            Process.Start("notify-send", [
                "--urgency", "normal",
                "--expire-time", "5000",
                NotificationTitle,
                body
            ]);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("osascript", ["-e", $"display notification \"{body}\" with title \"{NotificationTitle}\""]);
        }
    }
}

