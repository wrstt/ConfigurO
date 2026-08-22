// Linux-only compile stubs for the two Windows-only references:
// System.Management.Automation (the PowerShell SDK) and the Shell32 COM library.
// NEVER compiled on Windows — see build/check.sh. Exists purely so `mcs` can
// type-check the rest of the project on a non-Windows machine.
#if MONO_LINUX_CHECK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shell32
{
    public class ShellLinkObject { public string Path { get; set; } }
    public class FolderItem { public object GetLink { get { return new ShellLinkObject(); } } }
    public class Folder { public FolderItem ParseName(string n) { return new FolderItem(); } }
    public class Shell { public Folder NameSpace(object p) { return new Folder(); } }
}

namespace System.Management.Automation
{
    public class PSObject { }
    public class ErrorRecord { }
    public class PSDataCollection<T> : Collection<T> { }
    public class PSDataStreams { public PSDataCollection<ErrorRecord> Error = new PSDataCollection<ErrorRecord>(); }
    public class PowerShell : IDisposable
    {
        public PSDataStreams Streams = new PSDataStreams();
        public static PowerShell Create() { return new PowerShell(); }
        public PowerShell AddScript(string s) { return this; }
        public Collection<PSObject> Invoke() { return new Collection<PSObject>(); }
        public void Dispose() { }
    }
}
#endif
