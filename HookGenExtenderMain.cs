using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Reflection.Emit;

namespace HookGenExtender {
	public static class HookGenExtenderMain {
		private static int Main(string[] args) {
#if DEBUG
			args = new string[] { @"E:\Steam Games\steamapps\common\Rain World\BepInEx\utils\PUBLIC-Assembly-CSharp.dll", "true" };
			#else
			if (args.Length == 0) {
				Console.WriteLine("Please pass in a single argument of a DLL or EXE file to generate for.");
				Console.WriteLine("Optionally follow up with a boolean (true/false) indicating whether or not this is a Unity assembly.");
				Console.WriteLine("Press any key to quit . . .");
				Console.ReadLine();
				return 0;
			}
			#endif
			FileInfo dll = new FileInfo(args[0]);
			if (!dll.Exists) {
				Console.WriteLine($"No such file: {dll.FullName}");
				return 1;
			}

			bool isUnityAssembly = false;
			if (args.Length > 1) {
				if (!bool.TryParse(args[1], out isUnityAssembly)) {
					Console.WriteLine("The second argument was unable to be parsed as a boolean.");
				}
			}

			ModuleContext moduleContext = ModuleDef.CreateModuleContext();
			ModuleCreationOptions options = new ModuleCreationOptions(moduleContext);
			if (isUnityAssembly) {
				options.Runtime = CLRRuntimeReaderKind.Mono;
			}
			ModuleDefMD module = ModuleDefMD.Load(dll.FullName, options);

			MirrorGenerator generator = new MirrorGenerator(module);
			generator.Generate();

			FileInfo dest = new FileInfo(".\\TEST.dll");
			generator.Save(dest);

			return 0;
		}

	}
}