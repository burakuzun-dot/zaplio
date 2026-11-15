namespace Zaplio;

/// <summary>Rules for the "channel is sleeping" screen that keep it from hammering a provider that refuses the stream.</summary>
internal static class StateScreenPolicy
{
    /// <summary>Seconds the sleeping screen waits before it retries by itself.</summary>
    public const int AutoRetrySeconds = 10;

    /// <summary>After this many failed attempts the screen stops retrying by itself and waits for the user.</summary>
    public const int MaxAutomaticFailures = 2;

    /// <summary>Countdown for the next automatic retry; 0 means manual only.</summary>
    public static int NextAutoRetrySeconds(int failures) => failures >= MaxAutomaticFailures ? 0 : AutoRetrySeconds;
}
