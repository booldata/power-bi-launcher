# Program name and icon from the program file

Throwaway research for the Companion program spec. This branch is not meant to be merged.

Question: when the user picks a program file, which Windows field should supply the editable default display name, which API should supply the icon, and what should happen when either is missing?

## Recommendation

Prefer the VERSIONINFO string **FileDescription** for the editable default display name. Read it the way the launcher already reads version strings, through `FileVersionInfo`.

Take the icon from **SHGetFileInfoW** with **SHGFI_ICON**. That handle is the icon the shell uses to represent the file.

When FileDescription is missing, use the picked file's name without its directory or extension. When SHGetFileInfoW fails, keep no file icon and show a placeholder. A successful icon call is the file's icon, including the generic icon Explorer shows for a type with no custom icon.

Do not use ProductName as the name, and do not use ExtractIconEx as the icon source.

## Display name

### What the two fields mean

Both strings live in the VERSIONINFO resource's StringFileInfo block. The resource format lists them as predefined names ([VERSIONINFO resource](https://learn.microsoft.com/en-us/windows/win32/menurc/versioninfo-resource), [StringFileInfo block](https://learn.microsoft.com/en-us/windows/win32/menurc/stringfileinfo-block)):

- **FileDescription** is the "file description to be presented to users." The same page says it may be displayed in a list box when the user is choosing files to install, and gives "Keyboard Driver for AT-Style Keyboards" as the example. The version-resource String layout repeats that: the value "describes the file in such a way that it can be presented to users," with "Microsoft Word for Windows" as an example ([String structure](https://learn.microsoft.com/en-us/windows/win32/menurc/string-str)).
- **ProductName** is the "name of the product with which the file is distributed." The String structure's example is "Microsoft Windows," the product, not one program inside it.

`FileVersionInfo` exposes those same strings. `FileDescription` is "the description of the file," and `ProductName` is "the name of the product this file is distributed with." Either property is `null` when the file did not contain version information ([FileDescription](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.fileversioninfo.filedescription), [ProductName](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.fileversioninfo.productname)).

Win32 reads a named string with `VerQueryValue` after `GetFileVersionInfoSize` and `GetFileVersionInfo`. The predefined names include both FileDescription and ProductName. If the name does not exist, or the resource is not valid, `VerQueryValue` returns zero ([VerQueryValueW](https://learn.microsoft.com/en-us/windows/win32/api/winver/nf-winver-verqueryvaluew)). `GetFileVersionInfo` takes the non-fixed strings (the text values, as opposed to the binary version numbers) from the file's `.mui` when one exists, and otherwise from the binary ([GetFileVersionInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winver/nf-winver-getfileversioninfow)). FileDescription is the string those APIs treat as the user-facing description of that file.

The resource text marks FileDescription and ProductName as required. That is an authoring rule for a well-formed VERSIONINFO. It does not mean every program file has them. `GetVersionInfo` still returns a `FileVersionInfo` when the file has no version information; that object "contains only the name of the file requested" ([GetVersionInfo](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.fileversioninfo.getversioninfo)). `VerQueryValue` returns zero when a named string is absent even if other version data exists.

### What the launcher already reads

`FileVersionInfoReader.Read` calls `FileVersionInfo.GetVersionInfo` and returns both `ProductName` and `FileDescription` (`Power BI Launcher/Versions/FileVersionInfoReader.cs`). Callers use the pair to recognize a Power BI executable and to tell Desktop from Report Server:

- `PbiExecutablePolicy.IsPowerBiExecutable` requires the file name `PBIDesktop.exe` and the marker "Power BI" in either string (`Power BI Launcher/Versions/PbiExecutablePolicy.cs`).
- `PbiVersionKindPolicy.Infer` checks the path, ProductName, FileDescription, and an optional display hint for Report Server wording (`Power BI Launcher/Versions/PbiVersionKind.cs`).
- `ScanPolicy.DisplayName` does not display either string. A Version title is a kind label ("Desktop" or "Report Server"), plus Microsoft Store or Downloaded, plus a month-year taken from a display hint, the product name, or the path (`Power BI Launcher/Versions/ScanPolicy.cs`).

Those rules answer "is this Power BI, and which kind." They do not answer "what should this program be called." A Companion program is an arbitrary program file, so the spec should follow the VERSIONINFO field that is defined as the description presented to users.

### Name to prefer, and the missing-name fallback

Use FileDescription when it contains non-whitespace text. That is the editable default. The user can still change it.

When FileDescription is null, empty, or only whitespace, use the picked path's file name with the directory and extension removed.

Why that fallback, and why not the other version strings:

- `GetVersionInfo` documents that a file with no version information leaves only the file name. `FileName` is "the name of the file that this instance of FileVersionInfo describes" ([FileName](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.fileversioninfo.filename)). Stripping the directory and the extension is a spec rule so the label is a program name rather than a path. The API does not define that trimming.
- ProductName answers a different question. It names the product the file ships with, and the documented example is a suite name. The same missing-version case makes ProductName null as well, so it is not a useful second choice. The .NET docs do not say what either property returns when a version resource exists but omits that one key. At the Win32 layer, `VerQueryValue` returns zero for a missing name.
- OriginalFilename is the original name without a path, so a program can tell that the user renamed the file ([VERSIONINFO resource](https://learn.microsoft.com/en-us/windows/win32/menurc/versioninfo-resource)). It is not the description presented to users, and it can disagree with the file the user just picked.
- InternalName's authoring note says a file with no internal name should store the original filename without an extension. That tells the person writing the resource how to fill InternalName. It is not a reader fallback for a display name.
- `SHGetFileInfoW` with `SHGFI_DISPLAYNAME` returns "the name as it appears in Windows Explorer," and "the display name can be affected by settings such as whether extensions are shown" ([SHGetFileInfoW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)). That is the shell's file label, not the program description, and it depends on the user's Explorer setting.

Microsoft does not document an application fallback order when FileDescription is missing. The file-name fallback above is the spec's rule, grounded in what `GetVersionInfo` still returns. The docs are also silent on whitespace-only FileDescription; treat that as missing because it is not a name to show.

`GetVersionInfo` throws `FileNotFoundException` when the file cannot be found. A picked file that disappears before the read has no version strings. Use the same file-name fallback, taken from the path the user picked.

## Icon

### API to use

Call `SHGetFileInfoW` with `SHGFI_ICON`. The flag "retrieve[s] the handle to the icon that represents the file" and copies it to `SHFILEINFO.hIcon`. `SHGFI_LARGEICON` (value 0) or `SHGFI_SMALLICON` only chooses the size; both require `SHGFI_ICON`. If the flags do not include `SHGFI_EXETYPE` or `SHGFI_SYSICONINDEX`, the return value is nonzero on success and zero otherwise ([SHGetFileInfoW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow)).

That is the icon Windows shows for the file. An archived Microsoft article says the same function "provid[es] the icon that the shell would display for the file" and works on any type of file ([Icons](https://learn.microsoft.com/en-us/previous-versions/ms997538(v=msdn.10))). The shell's icon can be the executable's own icon, or a `DefaultIcon` registration of the form `path,index` on the file type or ProgID ([How to assign a custom icon to a file type](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-assign-a-custom-icon-to-a-file-type)). `SHGFI_ICONLOCATION` reports the file and index that `IExtractIcon::GetIconLocation` chose; it is a location query, not the icon handle.

`ExtractIconExW` is the wrong source. It "creates an array of handles to large or small icons extracted from the specified executable file, DLL, or icon file." With `nIconIndex` of -1 and both icon arrays null, the return is the number of icons in the file (the number of `RT_GROUP_ICON` resources for an executable or DLL). Otherwise the return is how many icons were extracted, or `UINT_MAX` on error, including when the file is not found ([ExtractIconExW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-extracticonexw)). `ExtractIcon` returns `NULL` when no icons were found ([ExtractIconA](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-extracticona)). Those functions read icon resources embedded in the file. They do not return the shell icon when that icon comes from a `DefaultIcon` entry pointing at a different file or index. The launcher does not call either function today.

### When the icon is missing

If `SHGetFileInfoW` returns zero, it did not supply an icon. Leave the Companion program without a file icon and show a placeholder. Do not call `DestroyIcon` unless the function wrote a handle.

If the call succeeds, store that icon. When no custom default icon is assigned to a file type, Explorer displays a generic default icon ([How to assign a custom icon to a file type](https://learn.microsoft.com/en-us/windows/win32/shell/how-to-assign-a-custom-icon-to-a-file-type)). `SHGetFileInfoW` does not document a separate "this is the generic icon" result, so the spec should not try to tell a generic shell icon from a custom one. A successful handle is the file's icon.

Microsoft is silent on whether an executable that contains zero `RT_GROUP_ICON` resources makes `SHGetFileInfoW` fail or return the generic application icon. Do not add an `ExtractIconExW` probe to decide. One icon API is enough, and a zero return from it is the missing case.

### Caller obligations the docs attach to this API

These are part of the `SHGetFileInfoW` contract, not a second icon source:

- `pszPath` is a string of maximum length `MAX_PATH`. A longer picked path is outside that contract. Treat a failed call as a missing icon.
- Initialize COM with `CoInitialize` or `OleInitialize` before the call.
- The remarks say to call it from a background thread so the UI does not stop responding.
- When `hIcon` is returned, the caller frees it with `DestroyIcon`.

The launcher does not read a program-file icon today. `FileVersionInfoReader` only returns the two version strings.

## What primary sources do not say

- They do not rank FileDescription above ProductName for an application the user adds by picking a file. The ranking above follows from the definitions: FileDescription is presented to users; ProductName names the product the file is distributed with.
- They do not define a fallback label when FileDescription is null, empty, or whitespace. The file-name fallback is this spec's rule.
- They do not say what `FileVersionInfo.FileDescription` returns when a version resource exists but omits that key. `VerQueryValue` returns zero in that case.
- They do not say that a successful `SHGetFileInfoW` icon is, or is not, the generic default. They do say Explorer uses a generic default when a type has no custom icon, and that a zero return means the call did not succeed.
- They do not mention Companion programs. Nothing in the launcher stores an icon or a display name taken from FileDescription for an arbitrary program file.
