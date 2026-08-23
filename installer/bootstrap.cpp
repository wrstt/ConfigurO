// ConfigurO setup.
//
// Native Win32 on purpose. The whole point of this program is to run on a
// machine that may have no .NET Framework at all, so it cannot itself be a
// .NET program -- and it is built with a static CRT so it does not need the
// Visual C++ runtime either. It depends on nothing but Windows.
//
// It does three things: make sure the .NET Framework 4.8 is present, put
// ConfigurO on disk, and start it.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <urlmon.h>
#include <shlobj.h>
#include <shellapi.h>
#include <objbase.h>
#include <wincrypt.h>
#include <wintrust.h>
#include <softpub.h>
#include <string>
#include <vector>

#pragma comment(lib, "urlmon.lib")
#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "user32.lib")

static const wchar_t* TITLE        = L"ConfigurO Setup";
static const DWORD    NET48        = 528040;   // registry Release for 4.8
static const wchar_t* NET48_URL    = L"https://go.microsoft.com/fwlink/?LinkId=2085155";
static const wchar_t* NET48_MANUAL = L"https://dotnet.microsoft.com/download/dotnet-framework/net48";

static void Say(const wchar_t* text, UINT icon) { MessageBoxW(NULL, text, TITLE, MB_OK | icon); }

// ── .NET Framework ──────────────────────────────────────────────────────
//
// Read through the 32-bit view, which is where this key lives on both
// architectures. A key that cannot be read is reported as absent rather than
// as zero so the caller can tell "old" from "unknown".
static bool NetRelease(DWORD& release)
{
    HKEY key = NULL;
    LONG r = RegOpenKeyExW(HKEY_LOCAL_MACHINE,
                           L"SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full",
                           0, KEY_READ | KEY_WOW64_32KEY, &key);
    if (r != ERROR_SUCCESS) return false;

    DWORD type = 0, value = 0, size = sizeof(value);
    r = RegQueryValueExW(key, L"Release", NULL, &type, (LPBYTE)&value, &size);
    RegCloseKey(key);

    if (r != ERROR_SUCCESS || type != REG_DWORD) return false;
    release = value;
    return true;
}

// ── Verifying what was downloaded ───────────────────────────────────────
//
// HTTPS proves who we talked to, not what they gave us. Between a hostile DNS
// answer, a proxy that terminates TLS -- which plenty of corporate networks do
// as policy -- and a cache in between, what lands on disk is not guaranteed to
// be what Microsoft published. This program then runs that file elevated, so
// "probably fine" is not good enough.
//
// It is also the difference between this installer and a dropper. Fetching an
// executable and running it is what droppers do; checking that it is signed by
// the publisher you expected, before running it, is what they never do.

static bool ChainIsValid(const std::wstring& path)
{
    WINTRUST_FILE_INFO file = {0};
    file.cbStruct      = sizeof(file);
    file.pcwszFilePath = path.c_str();

    GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;

    WINTRUST_DATA data = {0};
    data.cbStruct            = sizeof(data);
    data.dwUIChoice          = WTD_UI_NONE;
    data.fdwRevocationChecks = WTD_REVOKE_NONE;   // must not fail when offline
    data.dwUnionChoice       = WTD_CHOICE_FILE;
    data.pFile               = &file;
    data.dwStateAction       = WTD_STATEACTION_VERIFY;

    LONG status = WinVerifyTrust((HWND)INVALID_HANDLE_VALUE, &action, &data);

    data.dwStateAction = WTD_STATEACTION_CLOSE;
    WinVerifyTrust((HWND)INVALID_HANDLE_VALUE, &action, &data);

    return status == ERROR_SUCCESS;
}

// A valid signature is not enough on its own: anyone can sign anything. The
// name on the certificate is what says this came from Microsoft.
static bool SignedByMicrosoft(const std::wstring& path)
{
    HCERTSTORE store = NULL;
    HCRYPTMSG  msg   = NULL;
    DWORD encoding = 0, contentType = 0, formatType = 0;

    if (!CryptQueryObject(CERT_QUERY_OBJECT_FILE, path.c_str(),
                          CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                          CERT_QUERY_FORMAT_FLAG_BINARY, 0,
                          &encoding, &contentType, &formatType,
                          &store, &msg, NULL))
        return false;

    bool ok = false;
    DWORD size = 0;

    if (CryptMsgGetParam(msg, CMSG_SIGNER_INFO_PARAM, 0, NULL, &size) && size > 0)
    {
        std::vector<BYTE> buffer(size);
        if (CryptMsgGetParam(msg, CMSG_SIGNER_INFO_PARAM, 0, &buffer[0], &size))
        {
            CMSG_SIGNER_INFO* signer = (CMSG_SIGNER_INFO*)&buffer[0];

            CERT_INFO wanted = {0};
            wanted.Issuer       = signer->Issuer;
            wanted.SerialNumber = signer->SerialNumber;

            PCCERT_CONTEXT cert = CertFindCertificateInStore(
                store, encoding, 0, CERT_FIND_SUBJECT_CERT, &wanted, NULL);

            if (cert)
            {
                wchar_t name[256] = {0};
                DWORD written = CertGetNameStringW(cert, CERT_NAME_SIMPLE_DISPLAY_TYPE,
                                                   0, NULL, name, 256);
                if (written > 1 && wcsstr(name, L"Microsoft Corporation") != NULL)
                    ok = true;
                CertFreeCertificateContext(cert);
            }
        }
    }

    if (msg)   CryptMsgClose(msg);
    if (store) CertCloseStore(store, 0);
    return ok;
}

static bool TrustedMicrosoftBinary(const std::wstring& path)
{
    return ChainIsValid(path) && SignedByMicrosoft(path);
}

static std::wstring TempPath(const wchar_t* leaf)
{
    wchar_t dir[MAX_PATH] = {0};
    if (!GetTempPathW(MAX_PATH, dir)) return L"";
    return std::wstring(dir) + leaf;
}

// Blocks until the process exits. Returns false if it could not be started.
static bool RunWait(const std::wstring& file, const std::wstring& args, DWORD& exitCode)
{
    SHELLEXECUTEINFOW info = {0};
    info.cbSize       = sizeof(info);
    info.fMask        = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC;
    info.lpVerb       = L"open";
    info.lpFile       = file.c_str();
    info.lpParameters = args.empty() ? NULL : args.c_str();
    info.nShow        = SW_SHOWNORMAL;

    if (!ShellExecuteExW(&info) || !info.hProcess) return false;

    WaitForSingleObject(info.hProcess, INFINITE);
    if (!GetExitCodeProcess(info.hProcess, &exitCode)) exitCode = 0;
    CloseHandle(info.hProcess);
    return true;
}

// Returns false only when the user should not be sent on to the app.
static bool EnsureFramework()
{
    DWORD release = 0;
    if (NetRelease(release) && release >= NET48) return true;

    if (IDYES != MessageBoxW(NULL,
            L"ConfigurO needs the Microsoft .NET Framework 4.8, which is not installed "
            L"on this PC.\n\nDownload and install it now? It comes from Microsoft and is free.\n\n"
            L"This can take several minutes and may ask you to restart.",
            TITLE, MB_YESNO | MB_ICONQUESTION))
        return false;

    std::wstring installer = TempPath(L"ndp48-web.exe");
    if (installer.empty()) { Say(L"Could not use the temporary folder.", MB_ICONERROR); return false; }

    // Downloaded rather than carried: the offline package is about 100 MB, and
    // most people already have the framework and would never need a byte of it.
    if (FAILED(URLDownloadToFileW(NULL, NET48_URL, installer.c_str(), 0, NULL)))
    {
        Say(L"The .NET Framework installer could not be downloaded.\n\n"
            L"Check the internet connection, or install it by hand from:\n"
            L"https://dotnet.microsoft.com/download/dotnet-framework/net48", MB_ICONERROR);
        return false;
    }

    // Checked before it is executed, not after.
    if (!TrustedMicrosoftBinary(installer))
    {
        DeleteFileW(installer.c_str());
        Say(L"The downloaded .NET Framework installer is not signed by Microsoft, "
            L"so it has not been run.\n\n"
            L"Something on the network may be interfering with the download. "
            L"Install the framework by hand from:\n"
            L"https://dotnet.microsoft.com/download/dotnet-framework/net48", MB_ICONERROR);
        return false;
    }

    DWORD code = 0;
    bool started = RunWait(installer, L"/q /norestart", code);
    DeleteFileW(installer.c_str());

    if (!started) { Say(L"The .NET Framework installer would not start.", MB_ICONERROR); return false; }

    // 3010 is "installed, restart required", which is a success.
    if (code != 0 && code != 3010)
    {
        Say(L"The .NET Framework did not install correctly.\n\n"
            L"Install it by hand from:\n"
            L"https://dotnet.microsoft.com/download/dotnet-framework/net48", MB_ICONERROR);
        return false;
    }

    if (code == 3010)
        Say(L"The .NET Framework was installed and Windows needs to restart.\n\n"
            L"Restart, then run this setup again to finish installing ConfigurO.", MB_ICONINFORMATION);

    return code != 3010;
}

// ── ConfigurO itself ────────────────────────────────────────────────────
static bool WriteApp(const std::wstring& path)
{
    HRSRC found = FindResourceW(NULL, L"APPBIN", RT_RCDATA);
    if (!found) return false;
    HGLOBAL loaded = LoadResource(NULL, found);
    if (!loaded) return false;

    const void* data = LockResource(loaded);
    DWORD size = SizeofResource(NULL, found);
    if (!data || size == 0) return false;

    HANDLE file = CreateFileW(path.c_str(), GENERIC_WRITE, 0, NULL,
                              CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE) return false;

    DWORD written = 0;
    BOOL ok = WriteFile(file, data, size, &written, NULL);
    CloseHandle(file);
    return ok && written == size;
}

static void CreateShortcut(const std::wstring& target, const std::wstring& link)
{
    IShellLinkW* shell = NULL;
    if (FAILED(CoCreateInstance(CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER,
                                IID_IShellLinkW, (void**)&shell)))
        return;

    shell->SetPath(target.c_str());
    std::wstring dir = target.substr(0, target.find_last_of(L'\\'));
    shell->SetWorkingDirectory(dir.c_str());
    shell->SetDescription(L"Windows configuration, privacy and cleanup");

    IPersistFile* persist = NULL;
    if (SUCCEEDED(shell->QueryInterface(IID_IPersistFile, (void**)&persist)))
    {
        persist->Save(link.c_str(), TRUE);
        persist->Release();
    }
    shell->Release();
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    if (!EnsureFramework()) { CoUninitialize(); return 1; }

    wchar_t programs[MAX_PATH] = {0};
    if (FAILED(SHGetFolderPathW(NULL, CSIDL_PROGRAM_FILES, NULL, 0, programs)))
    {
        Say(L"Could not locate the Program Files folder.", MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    std::wstring dir = std::wstring(programs) + L"\\ConfigurO";
    CreateDirectoryW(dir.c_str(), NULL);
    std::wstring exe = dir + L"\\ConfigurO.exe";

    if (!WriteApp(exe))
    {
        Say(L"ConfigurO could not be written to Program Files.\n\n"
            L"Run this setup as an administrator, or check that ConfigurO is not "
            L"already running.", MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    wchar_t start[MAX_PATH] = {0};
    if (SUCCEEDED(SHGetFolderPathW(NULL, CSIDL_COMMON_PROGRAMS, NULL, 0, start)))
        CreateShortcut(exe, std::wstring(start) + L"\\ConfigurO.lnk");

    // Started without waiting: ConfigurO asks for elevation itself, and setup
    // has no reason to sit in the process list while it runs.
    ShellExecuteW(NULL, L"open", exe.c_str(), NULL, dir.c_str(), SW_SHOWNORMAL);

    CoUninitialize();
    return 0;
}
