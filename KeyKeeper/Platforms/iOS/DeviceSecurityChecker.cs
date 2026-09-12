using System.Runtime.InteropServices;
using Foundation;
using KeyKeeper.Core.Security;
using UIKit;

namespace KeyKeeper.Services;

/// <summary>
/// iOS implementation of <see cref="IDeviceSecurityChecker"/> -- see that interface's doc comment
/// for why several independent, individually-imperfect signals are combined instead of one
/// "definitely jailbroken" check. Every individual check below is wrapped in its own try/catch,
/// same reasoning as the Android implementation.
///
/// Unverified beyond compiling: no Mac/Xcode is available in this environment (same limitation as
/// every other iOS-only piece of this app since Phase 2's SQLCipher testing section and Phase 3's
/// LocalAuthentication biometric implementation) -- see ARCHITECTURE.md's Phase 7 section.
/// </summary>
public sealed class DeviceSecurityChecker : IDeviceSecurityChecker
{
    // Files/directories that only exist once a jailbreak tool (Cydia/Sileo/Zebra, Substrate/
    // Substitute injection, or the apt-based package manager underneath them) has been installed.
    private static readonly string[] JailbreakPaths =
    {
        "/Applications/Cydia.app",
        "/Applications/Sileo.app",
        "/Applications/Zebra.app",
        "/Applications/Installer.app",
        "/Applications/blackra1n.app",
        "/Library/MobileSubstrate/MobileSubstrate.dylib",
        "/Library/MobileSubstrate/DynamicLibraries",
        "/bin/bash",
        "/usr/sbin/sshd",
        "/etc/apt",
        "/private/var/lib/apt",
        "/private/var/lib/cydia",
        "/private/var/tmp/cydia.log",
        "/private/var/stash",
    };

    public DeviceSecurityCheckResult CheckDeviceSecurity()
    {
        var signals = new List<string>();

        if (AnyJailbreakPathExists())
        {
            signals.Add("jailbreak-file-found");
        }

        if (CanWriteOutsideSandbox())
        {
            signals.Add("sandbox-escape-write-succeeded");
        }

        if (CanOpenCydiaUrlScheme())
        {
            signals.Add("cydia-url-scheme-openable");
        }

        if (HasDyldInsertLibraries())
        {
            signals.Add("dyld-insert-libraries-present");
        }

        bool isJailbroken = signals.Count > 0;
        bool isDebuggerAttached = IsDebuggerAttached();
        if (isDebuggerAttached)
        {
            signals.Add("debugger-attached");
        }

        return new DeviceSecurityCheckResult(isJailbroken, isDebuggerAttached, signals);
    }

    private static bool AnyJailbreakPathExists()
    {
        try
        {
            return JailbreakPaths.Any(path => File.Exists(path) || Directory.Exists(path));
        }
        catch
        {
            return false;
        }
    }

    private static bool CanWriteOutsideSandbox()
    {
        // A regular (non-jailbroken) app is sandboxed and cannot write outside its own container
        // -- this should throw. On a jailbroken device the sandbox is commonly weakened or
        // disabled entirely, and the write can succeed.
        string path = $"/private/keykeeper-jbtest-{Guid.NewGuid():N}.txt";
        try
        {
            File.WriteAllText(path, "keykeeper-sandbox-probe");
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanOpenCydiaUrlScheme()
    {
        // Requires "cydia" in Info.plist's LSApplicationQueriesSchemes -- otherwise CanOpenUrl
        // always answers false regardless of whether Cydia is actually installed (iOS 9+
        // restriction), which would make this check silently useless rather than merely
        // conservative.
        try
        {
            return UIApplication.SharedApplication.CanOpenUrl(new NSUrl("cydia://package/com.example.package"));
        }
        catch
        {
            return false;
        }
    }

    private static bool HasDyldInsertLibraries()
    {
        // A non-empty DYLD_INSERT_LIBRARIES means something (a jailbreak tweak injector like
        // Substrate/Substitute, or a dynamic instrumentation tool like Frida) is being loaded
        // into this process at launch -- not exclusively a jailbreak signal, but a genuine sign
        // of tampering either way.
        try
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DYLD_INSERT_LIBRARIES"));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDebuggerAttached()
    {
#if DEBUG
        // Same reasoning as the Android implementation: a debugger is legitimately attached
        // during every normal development run. See ARCHITECTURE.md's Phase 7 section.
        return false;
#else
        try
        {
            return IsPTraced();
        }
        catch
        {
            return false;
        }
#endif
    }

    // Classic Apple Technical Q&A QA1361 ("Detecting the Debugger") technique: ask the kernel for
    // this process's kinfo_proc via sysctl(CTL_KERN, KERN_PROC, KERN_PROC_PID, getpid()) and
    // inspect the P_TRACED bit of kp_proc.p_flag. Reads that one field by its known byte offset
    // (32 -- see KinfoProcPFlagOffset below) rather than marshaling the entire legacy BSD
    // `extern_proc`/`kinfo_proc` struct field-by-field into C#; both land on the identical bytes.
    private static bool IsPTraced()
    {
        int[] mib = { CtlKern, KernProc, KernProcPid, getpid() };
        IntPtr buffer = Marshal.AllocHGlobal(KinfoProcBufferSize);
        try
        {
            IntPtr size = (IntPtr)KinfoProcBufferSize;
            int result = sysctl(mib, (uint)mib.Length, buffer, ref size, IntPtr.Zero, 0);
            if (result != 0)
            {
                return false;
            }

            int flags = Marshal.ReadInt32(buffer, KinfoProcPFlagOffset);
            return (flags & PTraced) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int CtlKern = 1;
    private const int KernProc = 14;
    private const int KernProcPid = 1;
    private const int PTraced = 0x00000800;

    // extern_proc's layout up to p_flag, on 64-bit Darwin: p_un (16 bytes -- either two 8-byte
    // proc pointers, or a `struct timeval` of two `long`s) + p_vmspace (8-byte pointer) +
    // p_sigacts (8-byte pointer) = 32 bytes, then `int p_flag` immediately follows. kinfo_proc's
    // first member is kp_proc (an extern_proc), so this is also kinfo_proc's own p_flag offset.
    private const int KinfoProcPFlagOffset = 32;
    private const int KinfoProcBufferSize = 1024;

    [DllImport("libc")]
    private static extern int sysctl(int[] name, uint namelen, IntPtr oldp, ref IntPtr oldlenp, IntPtr newp, uint newlen);

    [DllImport("libc")]
    private static extern int getpid();
}
