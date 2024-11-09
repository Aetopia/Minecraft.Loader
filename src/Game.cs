using System;
using System.IO;
using System.Linq;
using Windows.System;
using System.Threading;
using System.ComponentModel;
using Windows.Management.Core;
using Windows.ApplicationModel;
using System.Security.Principal;
using System.Security.AccessControl;
using Windows.Management.Deployment;
using System.Runtime.InteropServices;
using static Native;

static class Game
{
    static readonly PackageManager PackageManager = new();

    static readonly ApplicationActivationManager ApplicationActivationManager = new();

    static readonly PackageDebugSettings PackageDebugSettings = new();

    static readonly SecurityIdentifier Identifier = new("S-1-15-2-1");

    static readonly nint lpStartAddress;

    static Game()
    {
        nint hModule = default;
        try { hModule = LoadLibraryEx("Kernel32.dll", default, LOAD_LIBRARY_SEARCH_SYSTEM32); lpStartAddress = GetProcAddress(hModule, "LoadLibraryW"); }
        finally { FreeLibrary(hModule); }
    }

    static void LoadLibrary(int processId, string path)
    {
        FileInfo info = new(path = Path.GetFullPath(path)); var security = info.GetAccessControl();
        security.AddAccessRule(new(Identifier, FileSystemRights.ReadAndExecute, AccessControlType.Allow));
        info.SetAccessControl(security);

        nint hProcess = default, lpBaseAddress = default, hThread = default;
        try
        {
            hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (hProcess == default) throw new Win32Exception(Marshal.GetLastWin32Error());

            var size = sizeof(char) * (path.Length + 1);

            lpBaseAddress = VirtualAllocEx(hProcess, default, size, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
            if (lpBaseAddress == default) throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!WriteProcessMemory(hProcess, lpBaseAddress, Marshal.StringToHGlobalUni(path), size)) throw new Win32Exception(Marshal.GetLastWin32Error());

            hThread = CreateRemoteThread(hProcess, default, 0, lpStartAddress, lpBaseAddress, 0);
            if (hThread == default) throw new Win32Exception(Marshal.GetLastWin32Error());
            WaitForSingleObject(hThread, Timeout.Infinite);
        }
        finally
        {
            VirtualFreeEx(hProcess, lpBaseAddress, 0, MEM_RELEASE);
            CloseHandle(hThread);
            CloseHandle(hProcess);
        }
    }

    internal static void Launch(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new DllNotFoundException();
        var package = PackageManager.FindPackagesForUser(string.Empty, "Microsoft.MinecraftUWP_8wekyb3d8bbwe").FirstOrDefault();
        if (package is null) Marshal.ThrowExceptionForHR(ERROR_INSTALL_PACKAGE_NOT_FOUND);

        Marshal.ThrowExceptionForHR(PackageDebugSettings.TerminateAllProcesses(package.Id.FullName));
        Marshal.ThrowExceptionForHR(PackageDebugSettings.EnableDebugging(package.Id.FullName, default, default));

        using ManualResetEventSlim @event = new();
        using FileSystemWatcher watcher = new(ApplicationDataManager.CreateForPackageFamily(package.Id.FamilyName).LocalFolder.Path)
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        watcher.Deleted += (_, e) => { if (e.Name.Equals(@"games\com.mojang\minecraftpe\resource_init_lock", StringComparison.OrdinalIgnoreCase)) @event.Set(); };

        Marshal.ThrowExceptionForHR(ApplicationActivationManager.ActivateApplication(package.GetAppListEntries().First().AppUserModelId, null, AO_NOERRORUI, out var processId));
        @event.Wait();

        LoadLibrary(processId, path);
    }
}