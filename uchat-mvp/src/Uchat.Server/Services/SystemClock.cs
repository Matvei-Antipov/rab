namespace Uchat.Server.Services
{
    using System;
    using Uchat.Shared.Abstractions;

    /// <summary>
    /// System implementation of the clock abstraction.
    /// </summary>
    public class SystemClock : IClock
    {
        /// <inheritdoc/>
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
