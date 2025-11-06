using System;
using System.Reflection;

namespace fishmods
{
    //https://github.com/official-notfishvr/shadcn-ui
    public static class AssemblyLoader
    {
        static AssemblyLoader()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (args.Name.Contains("shadcnui"))
                {
                    // The resource name is YourProjectNamespace.Libs.shadcnui.dll
                    // The name is constructed from the default namespace and the path to the file.
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MIMESIS_Mod_Menu.Libs.shadcnui.dll"))
                    {
                        if (stream == null) return null;
                        byte[] assemblyData = new byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        return Assembly.Load(assemblyData);
                    }
                }
                return null;
            };
        }
        public static void Init() { }
    }
}