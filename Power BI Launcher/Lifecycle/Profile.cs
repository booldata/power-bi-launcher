using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerBILauncher
{
    public class Profile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int Order { get; set; }
        public string Name { get; set; } = "";
        public string IconPath { get; set; } = "";
        public string AvatarColor { get; set; } = "";
        public string CardColor { get; set; } = "";

        /// <summary>
        /// Registered Version to launch. Null follows the Default Version.
        /// </summary>
        public Guid? VersionId { get; set; }

        /// <summary>
        /// Optional Windows network identity (DOMAIN\user or UPN). Null/empty means off.
        /// Password is stored in Credential Manager, never in JSON.
        /// </summary>
        public string? WindowsNetworkIdentity { get; set; }

        /// <summary>
        /// Whether this Profile appears as a tray launch item.
        /// Null means the JSON field was missing (upgrade default applied by migration).
        /// </summary>
        public bool? ShowOnTray { get; set; }
    }
}

