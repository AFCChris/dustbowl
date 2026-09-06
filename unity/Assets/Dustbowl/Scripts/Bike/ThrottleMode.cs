namespace Dustbowl.Bike
{
    /// <summary>
    /// Player-selected throttle scheme, mirroring the web build's AUTO / MANUAL
    /// toggle. AUTO drives the engine for the rider (brake suppresses it while
    /// held); MANUAL leaves acceleration to the throttle input.
    /// </summary>
    public enum ThrottleMode
    {
        Auto,
        Manual
    }
}
