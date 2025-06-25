using System.Text.Json.Serialization;

namespace AuthCodeListener;

[method: JsonConstructor]
public class AuthCodePayload(string code, string source)
{
    public string Code { get; init; } = code;
    public string Source { get; init; } = source;
}
