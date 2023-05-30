using System.Runtime.Serialization;

namespace jihub.Base;

/// <inheritdoc />
public class ConfigurationException : Exception
{
    /// <inheritdoc />
    public ConfigurationException() { }

    /// <inheritdoc />
    public ConfigurationException(string message) : base(message) { }

    /// <inheritdoc />
    public ConfigurationException(string message, Exception inner) : base(message, inner) { }

    /// <inheritdoc />
    protected ConfigurationException(
        SerializationInfo info,
        StreamingContext context) : base(info, context) { }
}
