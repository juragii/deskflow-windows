using System;
using System.IO;
using System.Reflection;
#if OLD_PAYLOAD
[assembly:AssemblyVersion("2.2.0.0")]
[assembly:AssemblyFileVersion("2.2.0.0")]
#elif BAD_PAYLOAD
[assembly:AssemblyVersion("2.2.2.0")]
[assembly:AssemblyFileVersion("2.2.2.0")]
#else
[assembly:AssemblyVersion("2.2.1.0")]
[assembly:AssemblyFileVersion("2.2.1.0")]
#endif
class UpdateTestPayload {
 [STAThread]static void Main(string[] args){
#if BAD_PAYLOAD
 Environment.Exit(3);
#else
 int i=Array.IndexOf(args,"--update-health");if(i>=0&&i+1<args.Length)File.WriteAllText(args[i+1],"ready");
#endif
 }
}
