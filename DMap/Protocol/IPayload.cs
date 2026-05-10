namespace DMap.Protocol;

public interface IPayload
{
    /// <summary>
    /// Serializes this payload to a fixed buffer length.
    /// </summary>
    byte[] Serialize();
}
