using System.Text.Json.Serialization;

namespace WasabiBot.Contracts;

public class LambdaContext
{
    [JsonPropertyName("request_id")]
    public required Guid RequestId { get; set; }

    [JsonPropertyName("deadline")]
    public required long Deadline { get; set; }

    [JsonPropertyName("invoked_function_arn")]
    public required string InvokedFunctionArn { get; set; }

    [JsonPropertyName("env_config")]
    public required LambdaEnvironmentConfig LambdaEnvironmentConfig { get; set; }
}

public class LambdaEnvironmentConfig
{
    [JsonPropertyName("function_name")]
    public required string FunctionName { get; set; }

    [JsonPropertyName("memory")]
    public required long Memory { get; set; }

    [JsonPropertyName("version")]
    public required string Version { get; set; }
}
