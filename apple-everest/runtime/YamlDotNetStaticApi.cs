using System;

namespace YamlDotNet.Serialization;

// The static Apple closure consumes this marker on the build host when it
// generates typed YAML factories.  Distributed modules can therefore retain
// their ordinary DTO metadata without shipping YamlDotNet or reflecting over
// attributes on device.
[AttributeUsage(AttributeTargets.Property)]
public sealed class YamlIgnoreAttribute : Attribute
{
}
