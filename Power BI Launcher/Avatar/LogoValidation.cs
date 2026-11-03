using System;
using System.IO;
using System.Linq;

namespace PowerBILauncher
{
    public enum LogoValidationKind
    {
        PathEmpty,
        UnsupportedFormat
    }

    public sealed class LogoValidationException : InvalidOperationException
    {
        public LogoValidationException(LogoValidationKind kind, string? extension)
            : base(kind.ToString())
        {
            Kind = kind;
            Extension = extension;
        }

        public LogoValidationKind Kind { get; }
        public string? Extension { get; }
    }

    public static class LogoValidation
    {
        public static readonly string[] AllowedExtensions =
        {
            ".png", ".jpg", ".jpeg", ".ico", ".bmp"
        };

        public static string AllowedExtensionsDescription =>
            string.Join(", ", AllowedExtensions.Select(ext => ext.TrimStart('.')));

        public static LogoValidationKind? TryValidateLogoExtension(string path, out string? extension)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                extension = null;
                return LogoValidationKind.PathEmpty;
            }

            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext))
            {
                extension = null;
                return LogoValidationKind.UnsupportedFormat;
            }

            extension = ext;
            bool allowed = AllowedExtensions.Any(supported =>
                string.Equals(ext, supported, StringComparison.OrdinalIgnoreCase));

            return allowed ? null : LogoValidationKind.UnsupportedFormat;
        }
    }
}
