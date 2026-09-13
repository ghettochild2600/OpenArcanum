using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Arcanum.Runtime.Save
{
    /// <summary>Dispatches serialized versions into the single current snapshot shape.</summary>
    internal static class SessionSaveMigrator
    {
        internal static SessionLoadResult TryMigrateToCurrent(string json, out SessionSaveData current)
        {
            current = null;
            JObject envelope;
            try
            {
                envelope = JObject.Parse(json ?? string.Empty);
            }
            catch (JsonException ex)
            {
                return Failure(SessionLoadFailure.MalformedJson, ex.Message);
            }

            JToken formatToken = envelope["format"];
            if (formatToken?.Type != JTokenType.String
                || !string.Equals((string)formatToken, SessionSaveService.FormatIdentifier, StringComparison.Ordinal))
                return Failure(SessionLoadFailure.UnknownFormat, "Unknown session-save format.");

            JToken versionToken = envelope["version"];
            if (versionToken?.Type != JTokenType.Integer)
                return Failure(SessionLoadFailure.MissingRequiredField, "A numeric session-save version is required.");

            int version;
            try
            {
                version = versionToken.Value<int>();
            }
            catch (Exception ex) when (ex is OverflowException or FormatException)
            {
                return Failure(SessionLoadFailure.UnsupportedVersion, "The session-save version is out of range.");
            }

            return version switch
            {
                1 => MigrateV1(json, out current),
                _ => Failure(SessionLoadFailure.UnsupportedVersion,
                    $"Schema version {version} is unsupported; current is {SessionSaveService.CurrentVersion}."),
            };
        }

        private static SessionLoadResult MigrateV1(string json, out SessionSaveData current)
        {
            current = null;
            try
            {
                // Current is still V1. Keeping this explicit step is the compatibility seam for V1 -> future DTOs.
                current = JsonConvert.DeserializeObject<SessionSaveData>(json, SessionSaveService.JsonSettings);
            }
            catch (JsonException ex)
            {
                return Failure(SessionLoadFailure.MalformedJson, ex.Message);
            }
            return current == null
                ? Failure(SessionLoadFailure.MalformedJson, "The V1 session snapshot is empty.")
                : new SessionLoadResult(SessionLoadFailure.None);
        }

        private static SessionLoadResult Failure(SessionLoadFailure failure, string message)
            => new(failure, message);
    }
}
