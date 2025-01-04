
namespace BarySignalR.Core.Provider
{
    [GenerateSerializer]
    public record MethodMessage(string MethodName, byte[] SerializedArgs);
}
