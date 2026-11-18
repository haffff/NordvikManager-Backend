using NordvikManager.Application.DataTransferObjects.Transfer;
using NordvikManager.Application.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace NordvikManager.Application.Commands.Transfer
{
    internal static class EntityExports
    {
        public const string NotAnExport = "This isn't an exported map, card or playlist.";

        /// <summary>Refuses what isn't an export this server can import (as <paramref name="expectedKind"/>, when given).</summary>
        public static void Check([NotNull] EntityExportDto? export, string? expectedKind = null)
        {
            if (export == null || export.Format != EntityExportDto.FormatName || export.Data == null)
                throw WrongArgumentsException.Because(NotAnExport);
            if (export.Version < 1 || export.Version > EntityExportDto.CurrentVersion)
                throw WrongArgumentsException.Because("This file was exported by a newer version of NordvikManager.");
            if (!EntityTransferKinds.IsKnown(export.Kind))
                throw WrongArgumentsException.Because($"Can't import a \"{export.Kind}\".");
            if (expectedKind != null && expectedKind != export.Kind)
                throw WrongArgumentsException.Because($"This file holds a {export.Kind}, not a {expectedKind}.");
        }

        /// <summary>The export in this JSON, checked; refused if it isn't one.</summary>
        public static EntityExportDto From(JObject json, string? expectedKind = null)
        {
            EntityExportDto? export;
            try
            {
                export = json.ToObject<EntityExportDto>(JsonSerializer.Create(ExportSignature.Settings));
            }
            catch (Exception e) when (e is JsonException or FormatException or ArgumentException or InvalidCastException)
            {
                throw WrongArgumentsException.Because(NotAnExport);
            }
            Check(export, expectedKind);
            return export;
        }
    }
}
