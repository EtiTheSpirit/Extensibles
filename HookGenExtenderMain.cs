using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Reflection.Emit;

namespace HookGenExtender {
	public static class HookGenExtenderMain {
		private static int Main(string[] args) {
			FileInfo dll = new FileInfo(@"E:\Steam Games\steamapps\common\Rain World\BepInEx\utils\PUBLIC-Assembly-CSharp.dll");
			FileInfo hooks = new FileInfo(@"E:\Steam Games\steamapps\common\Rain World\BepInEx\plugins\HOOKS-Assembly-CSharp.dll");
			bool isUnityAssembly = true;

			ModuleContext moduleContext = ModuleDef.CreateModuleContext();
			ModuleCreationOptions options = new ModuleCreationOptions(moduleContext);
			if (isUnityAssembly) {
				options.Runtime = CLRRuntimeReaderKind.Mono;
			}
			ModuleDefMD module = ModuleDefMD.Load(dll.FullName, options);
			ModuleDefMD hooksMod = ModuleDefMD.Load(hooks.FullName, options);

			MirrorGenerator generator = new MirrorGenerator(module, hooksMod);
			generator.Generate();

			FileInfo dest = new FileInfo(".\\TEST.dll");
			generator.Save(dest);

			return 0;
		}

	}
}