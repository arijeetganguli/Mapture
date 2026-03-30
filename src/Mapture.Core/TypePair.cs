using System;
using System.Collections.Generic;

namespace Mapture
{
    public sealed class TypePair : IEquatable<TypePair>
    {
        public Type SourceType { get; }
        public Type DestinationType { get; }

        public TypePair(Type sourceType, Type destinationType)
        {
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
        }

        public bool Equals(TypePair? other)
        {
            if (other is null) return false;
            return SourceType == other.SourceType && DestinationType == other.DestinationType;
        }

        public override bool Equals(object? obj) => Equals(obj as TypePair);

        public override int GetHashCode()
        {
            unchecked
            {
                return (SourceType.GetHashCode() * 397) ^ DestinationType.GetHashCode();
            }
        }
    }
}
