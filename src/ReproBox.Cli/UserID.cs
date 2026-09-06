using System.Runtime.InteropServices;

static class UnixUser
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Passwd
    {
        public IntPtr pw_name;
        public IntPtr pw_passwd;
        public uint pw_uid;
        public uint pw_gid;
        public IntPtr pw_gecos;
        public IntPtr pw_dir;
        public IntPtr pw_shell;
    }

    [DllImport("libc")]
    private static extern IntPtr getpwuid(uint uid);

    public static string? GetUsername(uint? uid)
    {
        if (uid == null)
            return null;
        IntPtr ptr = getpwuid((uint)uid);

        if (ptr == IntPtr.Zero)
            return null;

        Passwd passwd = Marshal.PtrToStructure<Passwd>(ptr);

        return Marshal.PtrToStringAnsi(passwd.pw_name);
    }
}