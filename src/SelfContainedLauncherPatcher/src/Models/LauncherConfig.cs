﻿using System.Text.Json.Serialization;

namespace CsLauncher.Models
{
    internal class LauncherConfig
    {
        [JsonPropertyName("installDirectory")]
        public string? InstallDirectory { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(LauncherConfig))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}