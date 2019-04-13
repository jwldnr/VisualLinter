﻿namespace jwldnr.VisualLinter
{
    using System;

    /// <summary>
    /// Helper class that exposes all GUIDs used across VS Package.
    /// </summary>
    internal sealed partial class PackageGuids
    {
        public const string GuidVisualLinterPackageString = "9cd8a6da-b309-4066-8732-63117a3faaa9";
        public const string GuidVisualLinterOptionsDialogPageString = "2654ec05-adcd-47fa-8df8-197af3485d05";
        public const string GuidVisualLinterPackageCmdSetString = "709c5aff-bcec-42a8-b426-67253dc0c16d";
        public const string GuidVisualLinterPackageOutputPaneString = "d3ce5cd3-098d-41e1-a694-61d732cf1bde";
        public static Guid GuidVisualLinterPackage = new Guid(GuidVisualLinterPackageString);
        public static Guid GuidVisualLinterOptionsDialogPage = new Guid(GuidVisualLinterOptionsDialogPageString);
        public static Guid GuidVisualLinterPackageCmdSet = new Guid(GuidVisualLinterPackageCmdSetString);
        public static Guid GuidVisualLinterPackageOutputPane = new Guid(GuidVisualLinterPackageOutputPaneString);
    }

    /// <summary>
    /// Helper class that encapsulates all CommandIDs uses across VS Package.
    /// </summary>
    internal sealed partial class PackageIds
    {
    }
}
