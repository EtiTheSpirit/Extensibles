using dnlib.DotNet;
using HookGenExtender.Core.DataStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Core.Utils {

	public static class ShittyDocumentationGenerator {

		private const string XML_START = "<?xml version=\"1.0\"?>\n<doc>";
		private const string XML_END = "</doc>";

		private static void AppendList(StringBuilder xml, (string, string)[] termsAndDescriptions, string type = "bullet") {
			xml.AppendFormat("<list type=\"{0}\">\n", type);
			foreach ((string, string) item in termsAndDescriptions) {
				xml.AppendLine("<item>");
				xml.AppendFormat("<term>{0}</term>\n", item.Item1);
				xml.AppendFormat("<description>{0}</description>\n", item.Item2);
				xml.AppendLine("</item>");
			}
			xml.AppendLine("</list>");
		}

		public static void GenerateDocumentation(ExtensiblesGenerator main, FileInfo toFile) {
			StringBuilder xml = new StringBuilder(XML_START);
			xml.AppendFormat("<assembly><name>{0}</name></assembly>\n", main.Extensibles.Name);
			xml.AppendLine("<members>");
			foreach (ExtensibleTypeData info in main.GetAllExtensibleTypes()) {
				// Replacement type:
				xml.AppendFormat("<member name=\"T:{0}\">", info.ExtensibleType.Reference.FullName.Replace("/", "."));
				xml.AppendLine("<summary>");
				xml.AppendFormat("Extend this type to create an object that behaves as if it were an instance of <see cref=\"{0}\"/>.\n", info._originalGameType.FullName);
				xml.Append("Your type will inherit all methods, properties, and fields of the original type.<para/>\n\n");
				xml.AppendFormat("<strong>IMPORTANT:</strong> You <em>must</em> hook the original type (<see cref=\"{0}\"/>)'s <c>.ctor</c> and use this Extensible type's <see cref=\"{1}\">Binder</see>. For more information, check the Binder class stored within this class.<para/>\n\n", info.ImportedGameType.FullName, info.Binder.Reference.FullName);
				xml.AppendLine("Fields can <em>not</em> be overridden, as they are proxies. However, certain special rules apply to properties and methods, which <em>can</em> be overridden.");
				xml.AppendLine("When you override these properties or methods, they behave as an <strong>extensible hook</strong>. ");
				xml.AppendLine("Extensible hooks serve both as a proxy to the original game code, and are also a BepInEx <c>On.</c> hook, at the same time. ");
				xml.AppendLine("This introduces some very specific behavioral rules that you <strong>MUST</strong> pay attention to!");
				AppendList(xml, new (string, string)[] {
					("When used manually...", "Using base.Method() will execute original game code, <em>including</em> mod hooks, but <em>excluding</em> this function<br/>(it won't be called as part of a hook)."),
					("When called by BepInEx (as a hook)...", "Using base.Method() is identical to what would traditionally be written as <c>orig(self)</c>.")
				});
				xml.AppendLine("This behavior might seem a bit daunting at first. The takeaway is that you just need to focus on writing your code as if you are making a new object. Extensibles abstracts all the complicated hooking jargon away, while still being compatible with other mods, so that you can focus on your code where it matters.");
				xml.AppendLine("</summary>");
				xml.AppendLine("</member>");

				// Binder type:
				xml.AppendFormat("<member name=\"T:{0}\">", info.Binder.Reference.FullName.Replace("/", "."));
				xml.AppendLine("<summary>");
				xml.AppendLine("Every extensible class comes with a <strong>Binder</strong>. The binder's job is to bridge the gap between the game and BepInEx mod hooks, and your special Extensible type.<br/>");
				xml.AppendFormat("Use the <c>Bind</c> method to tell the system that your instance of <see cref=\"{0}\"/> corresponds to the original instance of <see cref=\"{1}\"/> that you provide to it.<para/>\n\n", info.ExtensibleType.Reference.FullName, info.ImportedGameType.FullName);
				xml.AppendLine("<strong>IMPORTANT:</strong> You can only bind one instance of your Extensible type to any given instance of the original type at a time! If you try to bind to an object that already has a binding, an exception will be raised. To clean up bindings, use the <c>TryReleaseCurrentBinding</c> method.");
				xml.AppendLine("</summary>");
				xml.AppendLine("</member>");
			}
			xml.AppendLine("</members>");
			xml.Append(XML_END);
			File.WriteAllText(toFile.FullName, xml.ToString());
		}

	}
}
