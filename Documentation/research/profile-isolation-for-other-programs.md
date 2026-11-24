# Profile isolation for other programs

Research note for Companion programs. This branch records what Visual Studio, SQL Server Management Studio, and Tabular Editor do with the launch Power BI already uses. It is not a product decision, and it is not meant to be merged.

## Conclusion

The launch is a Windows process start. `devenv.exe`, SSMS (`ssms.exe` in current docs, `Ssms.exe` in the SSMS 18 config doc), and Tabular Editor 2 (`TabularEditor.exe`) receive the same environment block and the same network-only logon when the launcher starts them the way it starts Power BI. None of the three products documents that contract.

Per-user files that those products place under Local AppData, Roaming AppData, `%USERPROFILE%`, or `%TEMP%` land in the Profile's Isolated Environment when the program resolves those locations from the process environment or from the usual known-folder path. The launcher sets `LOCALAPPDATA`, `APPDATA`, `TEMP`, and `TMP` to folders under the redirected `USERPROFILE`, and it creates those folders before start, so the two spellings are the same directory.

The process token stays the signed-in Windows user. `HKEY_CURRENT_USER`, DPAPI secrets for the current user, and the Windows user name a program displays stay with that user. Visual Studio's personalization account, SSMS Microsoft Entra sign-in, SQL authentication, and Tabular Editor's own OAuth and service-principal sign-in are separate. A Network login supplies default credentials for network access. It leaves those sign-ins alone.

## Launcher baseline

`CreateLaunchStartInfo` builds a `ProcessStartInfo` with `UseShellExecute` false, an empty argument list, and the executable's folder as the working directory. On that start info only, it sets:

- `USERPROFILE` to the Profile root
- `LOCALAPPDATA` to `<root>\AppData\Local`
- `APPDATA` to `<root>\AppData\Roaming`
- `TEMP` and `TMP` to `<root>\AppData\Local\Temp`

`ApplyNetworkCredentials` then sets `UseCredentialsForNetworkingOnly`, plus `UserName`, `Domain`, and `Password`. The launcher does not call `Environment.SetEnvironmentVariable`, and it does not set `LoadUserProfile`.

`ProfilePaths` creates that `AppData\Local` and `AppData\Roaming` nesting before launch. The launcher's own note, from Power BI Desktop, is that Windows resolves those known folders as `%USERPROFILE%\AppData\Local` and `%USERPROFILE%\AppData\Roaming`, and that .NET Framework `Environment.GetFolderPath` returns `""` when the folder is missing. The decision record describes the credential flag as the same option as `RUNAS /netonly`: the process stays the signed-in Windows user, and the saved identity is not Power BI service, Entra ID, MFA, or SSO.

Sources: `Power BI Launcher/Lifecycle/ProfileLifecycle.cs`, `Power BI Launcher/Lifecycle/ProfilePaths.cs`, `Documentation/decisions/versions-and-network-identity.md`.

## What Windows does with that start

Setting `EnvironmentVariables` on the start info passes those values in the child environment block. The dictionary starts as a copy of the launcher's environment, so the child also inherits everything else the launcher has, including `PATH`. Microsoft Learn documents `UseCredentialsForNetworkingOnly` as: the process starts with the caller's identity, Windows creates a new logon session, and those credentials are used on the network only. The password is not checked at start. The process can start and still fail when it touches a network resource.

The .NET runtime implements that property as `CreateProcessWithLogonW` with `LOGON_NETCREDENTIALS_ONLY`. When the start info's environment dictionary has been touched, the runtime passes that block as `lpEnvironment`. The launcher touches it before applying credentials, so this launch does not hit the Win32 rule that a null environment block is built from the named user's profile. `LoadUserProfile` and `UseCredentialsForNetworkingOnly` throw together. The launcher leaves `LoadUserProfile` unset, so the other user's profile hive is not loaded.

`LOGON_NETCREDENTIALS_ONLY` says the new process uses the caller's token, and the named credentials become the default credentials for network connections. The same page's username parameter says the account must have Log on locally. The flag text says the credentials are not validated. Both sentences are in the API page. The flag is the one this property sets. The older `runas` parameter list says `/netonly` means the credentials are for remote access only, and that `/profile` is incompatible with `/netonly`. The privileged-access logon-type table calls the same pattern `RUNAS /NETWORK`, logon type NewCredentials: the current LSA session is kept for local access, and the new credentials are used for network resources. That table lists SQL Windows authentication as a network logon.

`GetTempPath` and `GetTempPath2` (for a non-SYSTEM process) read `TMP`, then `TEMP`, then `USERPROFILE`, then the Windows directory. They do not check that the directory exists. Any program that calls them picks up the Profile temp folder. A `TEMP` value longer than about 130 characters makes `GetTempPath` fall through to `USERPROFILE` on the Windows 7-era implementation Microsoft described. The Profile folder is short on purpose for Power BI; that limit is a separate constraint for Companion programs that call `GetTempPath`.

Known-folder defaults on Microsoft Learn are `FOLDERID_LocalAppData` = `%LOCALAPPDATA%` (`%USERPROFILE%\AppData\Local`), `FOLDERID_RoamingAppData` = `%APPDATA%` (`%USERPROFILE%\AppData\Roaming`), and `FOLDERID_Profile` = `%USERPROFILE%`. `SHGetKnownFolderPath` returns the current path and, unless `KF_FLAG_DONT_VERIFY` is set, fails when the folder is absent. `Environment.GetFolderPath` returns `""` when the special folder does not exist. Microsoft's known-folder page does not say whether a process-level change of `LOCALAPPDATA` is ignored in favor of `%USERPROFILE%\AppData\...`. The launcher records that resolution for Power BI. For this launch the two spellings are the same directory, because `LOCALAPPDATA` is set to `<USERPROFILE>\AppData\Local` and the directory is created first.

`HKEY_CURRENT_USER` is the hive loaded at logon from the signed-in user's `NTUSER.DAT`. A child `USERPROFILE` does not load a different hive. With this logon flag the token is still the caller's, so registry writes through `HKEY_CURRENT_USER` stay in the signed-in user's hive.

`CryptProtectData`, and .NET `ProtectedData` with `DataProtectionScope.CurrentUser`, encrypt for the calling user's logon credential. The ciphertext can sit in the Isolated Environment. Decrypting it still requires the signed-in user.

Sources: [ProcessStartInfo.UseCredentialsForNetworkingOnly](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.usecredentialsfornetworkingonly), [Process.Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start), [.NET runtime process start](https://github.com/dotnet/runtime/blob/bbdd508a/src/libraries/System.Diagnostics.Process/src/Microsoft/Win32/SafeHandles/SafeProcessHandle.Windows.cs), [CreateProcessWithLogonW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createprocesswithlogonw), [runas parameters](https://learn.microsoft.com/en-us/previous-versions/bb756922(v=msdn.10)), [logon types](https://learn.microsoft.com/en-us/windows-server/identity/securing-privileged-access/reference-tools-logon-types), [GetTempPath](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-gettemppatha), [GetTempPath2](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-gettemppath2a), [long TEMP](https://learn.microsoft.com/en-us/archive/blogs/larryosterman/its-a-bad-idea-to-have-a-temp-environment-variable-longer-than-about-130-characters), [known folders](https://learn.microsoft.com/en-us/windows/win32/shell/knownfolderid), [SHGetKnownFolderPath](https://learn.microsoft.com/en-us/windows/win32/api/shlobj_core/nf-shlobj_core-shgetknownfolderpath), [Environment.GetFolderPath](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath), [About user profiles](https://learn.microsoft.com/en-us/windows/win32/shell/about-user-profiles), [CryptProtectData](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata), [DataProtectionScope](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.dataprotectionscope).

## Visual Studio (`devenv.exe`)

Microsoft does not say that `devenv.exe` reads `USERPROFILE`, `LOCALAPPDATA`, `APPDATA`, `TEMP`, or `TMP` from the process, or that it honors a network-only logon.

Documented per-user files, which sit on the paths this launch redirects:

- The private registry file is `%localappdata%\Microsoft\VisualStudio\<instance>\privateregistry.bin`. Current Visual Studio keeps instance settings in that file. The experimental-instance tool says that instance does not use the system registry.
- Settings export and the synchronized-settings file are under `%USERPROFILE%\AppData\Local\Microsoft\VisualStudio\<version>_<id>\Settings` (`CurrentSettings.vssettings`). The installation id is per Visual Studio install, not per Profile.
- NuGet's global packages folder is `%userprofile%\.nuget\packages`. The HTTP cache is `%localappdata%\NuGet\v3-cache`. NuGet scratch is `%temp%\NuGetScratch`. The user `NuGet.Config` is `%appdata%\NuGet\NuGet.Config`. `NUGET_PACKAGES` overrides the global-packages path. This launch does not set it.

The documented way to give Visual Studio a separate hive and folder is `devenv /RootSuffix`. This launch passes no arguments, so Visual Studio uses its normal instance folder. Two Profiles that resolve Local AppData into two Profile folders get two copies of that instance folder. They still share one installation id and one machine-wide install under Program Files.

Sign-in is a Microsoft, organizational, or GitHub account, called the personalization account. Unless the user signs out, Visual Studio signs that account in on later starts, and it synchronizes a documented set of settings through the account. Microsoft does not say where the sign-in token is stored. The cloud copy is outside the Isolated Environment. Whether the local token file follows Local AppData is unpublished.

Older extensibility docs still describe options pages under `HKEY_CURRENT_USER\Software\Microsoft\VisualStudio\<version>`. The current instance docs put the registry in the private file above. Microsoft does not say that current `devenv.exe` has stopped writing the signed-in user's `HKEY_CURRENT_USER` hive entirely. Anything that still does stays in the real profile.

Computer-level NuGet config under `%ProgramFiles(x86)%\NuGet\Config` is machine-wide.

Sources: [private registry](https://learn.microsoft.com/en-us/visualstudio/install/tools-for-managing-visual-studio-instances), [experimental instance](https://learn.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance), [CreateExpInstance](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/createexpinstance-utility), [`/RootSuffix`](https://learn.microsoft.com/en-us/visualstudio/extensibility/devenv-command-line-switches-for-vspackage-development), [synchronized settings](https://learn.microsoft.com/en-us/visualstudio/ide/synchronized-settings-in-visual-studio), [settings folder](https://learn.microsoft.com/en-us/visualstudio/ide/identifying-and-customizing-keyboard-shortcuts-in-visual-studio), [sign-in](https://learn.microsoft.com/en-us/visualstudio/ide/signing-in-to-visual-studio), [options registry](https://learn.microsoft.com/en-us/visualstudio/extensibility/internals/options-and-options-pages), [NuGet folders](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders), [NuGet.Config locations](https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior).

## SQL Server Management Studio

Microsoft does not say that SSMS reads those five variables, or that it honors a network-only logon.

Current troubleshooting docs name the executable `ssms.exe`. The default folder they give for SSMS 21 is `C:\Program Files\Microsoft SQL Server Management Studio 21\Release\Common7\IDE`. The SSMS 18 tab-crash workaround names `Ssms.exe.config` under `C:\Program Files (x86)\Microsoft SQL Server Management Studio 18\Common7\IDE`. The command-line article for current SSMS uses `ssms.exe` and, for SSMS 22, `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE`. Neither article says the two file names resolve profile data differently.

Documented per-user folders:

- `%USERPROFILE%\AppData\Local\Microsoft\SQL Server Management Studio`
- `%USERPROFILE%\AppData\Roaming\Microsoft\SQL Server Management Studio`
- Verbose activity logs under `%USERPROFILE%\AppData\Roaming\Microsoft\SSMS`

`RegSrvr*.xml` in the Local folder holds local registered servers. Those groups are unique to each user. After an import, SQL authentication passwords are stored per user and must be entered once. Windows authentication registrations do not store a SQL password. The cache article does not give a versioned `18.0` or `19.0` folder. It is silent on SSMS 18 and 19 AppData layout.

Windows Authentication is "connect through a Windows user account." The user-name field is read-only and shows the current Windows user. Under this launch that name is the signed-in user, because the process token is the caller's. The Windows logon-type reference is the source that classifies SQL Windows authentication as a network logon and classifies this credential flag as the credentials used for network resources. SSMS itself never mentions `runas` or `/netonly`. Microsoft is silent on a local shared-memory or named-pipes connection, where the call might not be a network logon.

SQL Server Authentication is a login and password the user types. Remember Password stores that password. The command line can pass `-U` for a SQL login. `-P` was removed in SSMS 18. This launch passes no arguments.

Microsoft Entra MFA, Entra password, Entra service principal, and Entra integrated are separate authentication modes on the same login page. Entra integrated is described as the Windows identity federated with Entra, and its user name is also the current Windows user. The product doc does not say that mode picks up NewCredentials. SSMS keeps a Microsoft Entra ID token cache, cleared from Help. The troubleshooting article does not say which file holds it, so this note cannot place that cache in the Isolated Environment or in the real profile.

Sources: [SSMS troubleshooting](https://learn.microsoft.com/en-us/ssms/troubleshoot/ssms-troubleshoot), [SSMS 18 config path](https://learn.microsoft.com/en-us/ssms/troubleshoot/workaround-move-tabs), [ssms command line](https://learn.microsoft.com/en-us/ssms/ssms-utility), [registered servers](https://learn.microsoft.com/en-us/ssms/register-servers/create-a-new-registered-server-sql-server-management-studio), [import, per-user SQL passwords](https://learn.microsoft.com/en-us/ssms/register-servers/import-registered-server-information-sql-server-management-studio), [connect, authentication modes](https://learn.microsoft.com/en-us/sql/ssms/f1-help/connect-to-server-login-page-database-engine).

## Tabular Editor

The ticket names `TabularEditor.exe`. That is the file the Tabular Editor 2 repository and its maintainer refer to. Tabular Editor 3's docs do not name the executable. The two products store different files. A Companion program entry pointed at one of them does not cover the other.

### Tabular Editor 2

Current source resolves these files with `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`, then `\TabularEditor\`:

- `Preferences.json`
- `RecentFiles.json`
- `MacroActions.json`
- `CustomActions.json` (still defined; saving macros deletes it)
- `MacroCompilationErrors.log`

That is the known-folder API, not a direct read of the `LOCALAPPDATA` variable. Combined with the launcher's Power BI finding, those files follow the redirected `USERPROFILE` when `AppData\Local` exists. Microsoft's known-folder page does not itself say that a `devenv`-style or Tabular Editor process re-reads `USERPROFILE`. The source is silent on `TEMP`, `TMP`, and on the network-only logon.

The proxy password inside `Preferences.json` is `ProtectedData` with `DataProtectionScope.CurrentUser`. The file can live in the Profile folder. The key is the signed-in user.

Vendor docs disagree with that source on preferences. The user-settings page puts `Preferences.json` in `%ProgramData%\TabularEditor` (all users) and puts per-user `BPARules.json`, `CustomActions.json`, `RecentFiles.json`, and `RecentServers.json` in `%AppData%\Local\TabularEditor`. The sharing page puts `Preferences.json` and macros in `%LOCALAPPDATA%\TabularEditor\`, which matches the source. `%ProgramData%` is the machine profile. It is outside the five variables. The user-settings page also puts machine-wide `BPARules.json` and `TOMWrapper.dll` there. `Analyzer.cs` calls `GetLocalMachineCollection()` and does not show the path. This note did not trace `RecentServers.json` to a source line; only the user-settings page lists it.

### Tabular Editor 3

Vendor docs put preferences in `%localappdata%\TabularEditor3`, "for each Windows user profile." That includes `Preferences.json`. API keys and the MCP access token in that file are encrypted with DPAPI for the current user. The same page is silent on which API expands `%localappdata%`.

`.tmuo` user-options files sit next to a model loaded from disk, or under `UserOptions` for a model opened from a server. The file name includes the Windows user name. Credentials in it are encrypted with that user's Windows key. A model that lives on the real disk keeps its `.tmuo` there. The Windows user name in the file name is the process token, which this launch does not change.

`RecentServers.json` stores the server, authentication type, and user name. It does not store passwords. Power BI connectivity offers Integrated, Service Principal, and Master User, and an embedded browser for OAuth. Proxy settings can store their own user and encrypted password, or "use the current Windows credentials," which the doc says matches Power BI Desktop's proxy behavior. The doc does not mention a network-only logon.

Sources: [TE2 Preferences.cs](https://github.com/TabularEditor/TabularEditor/blob/master/TabularEditor/UIServices/Preferences.cs), [TE2 RecentFiles.cs](https://github.com/TabularEditor/TabularEditor/blob/master/TabularEditor/UIServices/RecentFiles.cs), [TE2 ScriptEngine.cs](https://github.com/TabularEditor/TabularEditor/blob/master/TabularEditor/Scripting/ScriptEngine.cs), [TE2 Crypto.cs](https://github.com/TabularEditor/TabularEditor/blob/master/TabularEditor/UIServices/Crypto.cs), [TE2 user-settings page](https://docs.tabulareditor.com/en/references/user-settings-files-te2.html), [TE2 and TE3 shared paths](https://docs.tabulareditor.com/en/getting-started/sharing-macros-bpa-rules.html), [TE3 preferences](https://docs.tabulareditor.com/en/references/preferences.html), [TE3 local files and DPAPI](https://docs.tabulareditor.com/en/security/ai-compliance.html), [TE3 supported files](https://docs.tabulareditor.com/en/references/supported-files.html).

## What a Network login covers

For any of the three processes, Windows states the same boundary:

- Default credentials on network connections use the saved account. The logon-type reference puts SQL Windows authentication in that class. SSMS does not say so itself.
- Local access uses the signed-in user. File ACLs, the loaded `HKEY_CURRENT_USER` hive, and the user name shown on the SSMS Windows Authentication and Entra integrated fields are that user.
- The password is not validated at start. A wrong password still starts the program.
- The named user's profile is not loaded. `/profile` cannot be combined with `/netonly`, and .NET rejects `LoadUserProfile` together with this flag.

Outside that boundary, from the product docs:

- Visual Studio sign-in, settings sync, GitHub, and Copilot use the personalization account. Microsoft does not tie them to the Windows network logon.
- SSMS SQL authentication, Entra MFA, Entra password, and Entra service principal use credentials the user types or a token SSMS caches. Entra integrated is documented as the Windows identity of the process.
- Tabular Editor 3 Service Principal, Master User, and the OAuth browser are the product's own sign-in. Remembered server entries do not store a password. Proxy basic credentials are a separate encrypted password. "Current Windows credentials" for the proxy is the only TE3 setting that points at the Windows credential, and the doc does not mention NewCredentials.
- DPAPI and `ProtectedData` current-user blobs, including TE2's proxy password and TE3's API key, unlock for the signed-in user.

## Primary-source gaps

- No product doc says `devenv.exe`, `ssms.exe`, `Ssms.exe`, or `TabularEditor.exe` honors this environment block or `UseCredentialsForNetworkingOnly`.
- Microsoft's known-folder page does not say that a process-level `USERPROFILE` or `LOCALAPPDATA` override is what `SHGetKnownFolderPath` expands. The `%USERPROFILE%\AppData\...` rule is the launcher's Power BI note.
- Visual Studio does not say where the personalization-account token is stored, or whether current `devenv.exe` still writes the real `HKEY_CURRENT_USER`.
- SSMS does not say where the Entra token cache file is, does not mention `/netonly`, and does not say what a local SQL connection authenticates as. Current cache paths are unversioned. SSMS 18 and 19 AppData paths are unpublished in the pages read here. The SSMS 18 doc names `Ssms.exe`; current docs name `ssms.exe`.
- Tabular Editor 3 docs do not name the executable, do not say which API expands `%localappdata%`, and do not mention a network-only logon. Tabular Editor 2's user-settings page still places `Preferences.json` in `%ProgramData%`; current source places it under Local AppData. `RecentServers.json` and the machine BPA path were not traced to a source line.
- None of the three product docs says whether the program calls `GetTempPath`. NuGet, used by Visual Studio, documents `%temp%`. The long-`TEMP` fallback is described for an older Windows implementation. Current `GetTempPath` docs are silent on that length limit.
- This note did not launch or install the programs.
