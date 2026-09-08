using DndOnePlaceManager.Application.Generic.Command;
using DndOnePlaceManager.Domain.Enums;
using System;

namespace DndOnePlaceManager.Application.Commands.Addons.InstallAddon
{
    public class InstallAddonCommand : GamePlayerCommandBase<(CommandResponse, InstallAddonCommandResponse)>
    {
        public bool? AutoInstallDeps { get; set; }
        public byte[]? AddonFile { get; set; }
        public string? AddonFileName { get; set; }
        public string? AddonSourceKey { get; set; }
        public bool Reinstall { get; set; } = false;

        /// <summary>
        /// Optional progress callback, invoked as scripts/resources/actions/templates/views
        /// are installed. Mirrors LinkDirectoryCommand.OnProgress. Not propagated into
        /// recursive dependency installs — a dependency is reported as a single step rather
        /// than expanding its own entries into the parent's total.
        /// </summary>
        public Action<InstallAddonProgress>? OnProgress { get; set; }
    }

    public class InstallAddonProgress
    {
        public string Phase { get; set; } = string.Empty;
        public int Current { get; set; }
        public int Total { get; set; }
        public string? Message { get; set; }
    }
}
