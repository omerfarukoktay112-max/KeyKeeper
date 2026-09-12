using Android.App;
using Android.Content.PM;
using Android.OS;
using KeyKeeper.Core.Security;

namespace KeyKeeper.Services;

/// <summary>
/// Android implementation of <see cref="IDeviceSecurityChecker"/> -- see that interface's doc
/// comment for why several independent, individually-imperfect signals are combined instead of
/// one "definitely rooted" check. Every individual check below is wrapped in its own try/catch:
/// a check that throws (e.g. a <see cref="PackageManager"/> quirk on some OEM skin) counts as "no
/// evidence from that check" and never takes down the whole result -- consistent with this app's
/// overall bias toward false positives (an extra dismissable warning) over a crash.
/// </summary>
public sealed class DeviceSecurityChecker : IDeviceSecurityChecker
{
    // Common su binary install locations across mainline root solutions (Magisk, SuperSU, and
    // older su-based root methods). Presence of any one is treated as one signal, not several --
    // see AddIfExists below.
    private static readonly string[] SuBinaryPaths =
    {
        "/system/bin/su",
        "/system/xbin/su",
        "/sbin/su",
        "/system/sd/xbin/su",
        "/system/bin/failsafe/su",
        "/data/local/su",
        "/data/local/bin/su",
        "/data/local/xbin/su",
        "/su/bin/su",
    };

    // Known root-manager / root-broker package names. Not exhaustive -- new ones appear over
    // time -- but each additional entry costs nothing and widens the "any signal" net.
    private static readonly string[] RootManagerPackages =
    {
        "com.topjohnwu.magisk",
        "eu.chainfire.supersu",
        "com.noshufou.android.su",
        "com.noshufou.android.su.elite",
        "com.koushikdutta.superuser",
        "com.thirdparty.superuser",
        "com.yellowes.su",
        "com.kingroot.kinguser",
        "com.kingo.root",
        "com.smedialink.oneclickroot",
        "com.zhiqupk.root.global",
        "com.alephzain.framaroot",
    };

    public DeviceSecurityCheckResult CheckDeviceSecurity()
    {
        var signals = new List<string>();

        if (AnyPathExists(SuBinaryPaths))
        {
            signals.Add("su-binary-found");
        }

        if (AnyPackageInstalled(RootManagerPackages))
        {
            signals.Add("root-manager-package-found");
        }

        if (HasTestKeysBuildTag())
        {
            signals.Add("build-tags-test-keys");
        }

        if (IsNonUserBuildType())
        {
            signals.Add("build-type-not-user");
        }

        bool isRooted = signals.Count > 0;
        bool isDebuggerAttached = IsDebuggerAttached();
        if (isDebuggerAttached)
        {
            signals.Add("debugger-attached");
        }

        return new DeviceSecurityCheckResult(isRooted, isDebuggerAttached, signals);
    }

    private static bool AnyPathExists(string[] paths)
    {
        try
        {
            return paths.Any(File.Exists);
        }
        catch
        {
            return false;
        }
    }

    private static bool AnyPackageInstalled(string[] packageNames)
    {
        try
        {
            PackageManager? packageManager = Android.App.Application.Context.PackageManager;
            if (packageManager is null)
            {
                return false;
            }

            foreach (string packageName in packageNames)
            {
                try
                {
                    packageManager.GetPackageInfo(packageName, PackageInfoFlags.MatchAll);
                    return true;
                }
                catch (PackageManager.NameNotFoundException)
                {
                    // Not installed -- expected for almost every package on almost every device.
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasTestKeysBuildTag()
    {
        try
        {
            return Build.Tags?.Contains("test-keys") == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNonUserBuildType()
    {
        try
        {
            // "user" is the standard retail build type; "userdebug"/"eng" builds (common on
            // custom ROMs and development devices) ship with a debuggable system and adbd
            // running as root -- a weaker posture even without an explicit su binary found.
            return !string.Equals(Build.Type, "user", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDebuggerAttached()
    {
#if DEBUG
        // A debugger is legitimately attached during every normal development run (dotnet build
        // ... -t:Run, Visual Studio's own debugger) -- checking this in Debug builds would fire
        // on every single dev session, not just a real attack. See ARCHITECTURE.md's Phase 7
        // section.
        return false;
#else
        try
        {
            return Debug.IsDebuggerConnected;
        }
        catch
        {
            return false;
        }
#endif
    }
}
