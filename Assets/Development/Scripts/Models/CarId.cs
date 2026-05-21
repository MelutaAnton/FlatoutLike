using System;

namespace FlatOutLike.Models
{
    public readonly struct CarId : IEquatable<CarId>
    {
        public readonly int Value;

        public CarId(int value) => Value = value;

        public static readonly CarId None = new CarId(-1);

        public bool Equals(CarId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is CarId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => $"CarId({Value})";

        public static bool operator ==(CarId left, CarId right) => left.Equals(right);

        public static bool operator !=(CarId left, CarId right) => !left.Equals(right);
    }
}
