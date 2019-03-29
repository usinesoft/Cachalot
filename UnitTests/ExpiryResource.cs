using System;

namespace UnitTests
{
    /// <summary>
    /// Simulates a resource which expires. Sometimes randomly, always after N usages
    /// </summary>
    class ExpiryResource
    {
        private static readonly Random _randGen = new Random();
        private bool? _expired;

        private int _useCount;

        public bool IsValid
        {
            get
            {
                if (_useCount > 10)
                    return false;

                lock (_randGen)
                {
                    if (_expired.HasValue)
                        return !_expired.Value;

                    _expired = _randGen.Next(0, 100) < 10;

                    return !_expired.Value;
                }
            }
        }

        public void Use()
        {
            _useCount++;
        }
    }
}