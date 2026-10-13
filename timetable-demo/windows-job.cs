using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

// The launcher owns the only job handle. Closing its terminal also terminates
// the server and all of its descendants, even if JS shutdown cannot run.
// https://learn.microsoft.com/windows/win32/procthread/job-objects
public sealed class TimetableJob : IDisposable
{
    private IntPtr job;
    private IntPtr process;
    public uint ProcessId { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcesses;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo {
        public uint Size;
        public string Reserved, Desktop, Title;
        public uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        public ushort ShowWindow, ReservedSize;
        public IntPtr ReservedPointer, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int type, ref ExtendedLimits info, uint length);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo info);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")]
    private static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public TimetableJob(string executable, string entryPoint, string directory) {
        ProcessInfo child = new ProcessInfo();
        try {
            job = CreateJobObject(IntPtr.Zero, null);
            if (job == IntPtr.Zero) throw new Win32Exception();
            var limits = new ExtendedLimits();
            limits.Basic.Flags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf(limits))) throw new Win32Exception();
            var startup = new StartupInfo();
            startup.Size = (uint)Marshal.SizeOf(startup);
            var command = new StringBuilder("\"" + executable + "\" \"" + entryPoint + "\"");
            // Suspend before assignment so no child can escape the job at startup.
            if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false, 4, IntPtr.Zero, directory, ref startup, out child)) throw new Win32Exception();
            process = child.Process;
            ProcessId = child.ProcessId;
            if (!AssignProcessToJobObject(job, process)) throw new Win32Exception();
            if (ResumeThread(child.Thread) == UInt32.MaxValue) throw new Win32Exception();
        } catch {
            if (process != IntPtr.Zero) TerminateProcess(process, 1);
            Dispose();
            throw;
        } finally {
            if (child.Thread != IntPtr.Zero) CloseHandle(child.Thread);
        }
    }
    public bool Wait(uint milliseconds) {
        uint result = WaitForSingleObject(process, milliseconds);
        if (result == UInt32.MaxValue) throw new Win32Exception();
        return result == 0;
    }
    public uint ExitCode {
        get { uint code; if (!GetExitCodeProcess(process, out code)) throw new Win32Exception(); return code; }
    }
    public void Dispose() {
        if (job != IntPtr.Zero) { CloseHandle(job); job = IntPtr.Zero; }
        if (process != IntPtr.Zero) { CloseHandle(process); process = IntPtr.Zero; }
    }
}
