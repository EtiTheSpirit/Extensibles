using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.PreWrittenCode {

	internal class ExtensibleBinderBase {

		internal static readonly Dictionary<Type, ExtensibleBinderBase> _binders = new Dictionary<Type, ExtensibleBinderBase>();

	}

	internal class ExtensibleBinder<TOriginal, TYourExtension> : ExtensibleBinderBase where TOriginal : class where TYourExtension : class, new() {

		private readonly ConditionalWeakTable<TOriginal, TYourExtension> _extensionLookup = new ConditionalWeakTable<TOriginal, TYourExtension>();

		private ExtensibleBinder() { } 

		public void Bind(TOriginal original) {
			_extensionLookup.GetValue(original, org => new TYourExtension());
		}
		
		void CommonBindProcedure() {
			Type onHookType = typeof(void); // This will be replaced during code generation with typeof(On.Whatever).
			Type extType = typeof(TYourExtension);
			MethodInfo[] methods = extType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
			for (int j = 0; j < methods.Length; j++) {
				MethodInfo method = methods[j];
				Attribute? attr = method.GetCustomAttribute<Attribute>();
				if (attr != null) {
					object something = attr.TypeId;
					EventInfo? evt = onHookType.GetEvent(something.ToString()!, BindingFlags.Public | BindingFlags.Static);
					if (evt != null) {
						break;
					}
				}
			}
		}

		private void FireBoundExtension() {
			
		}

		/// <summary>
		/// Use this so IOE can be raised for duplicates.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		public static ExtensibleBinder<TOriginal, TYourExtension> CreateBinder() {
			Type extension = typeof(TYourExtension);
			if (!_binders.TryGetValue(extension, out ExtensibleBinderBase? binder)) {
				ExtensibleBinder<TOriginal, TYourExtension> binderInstance = new ExtensibleBinder<TOriginal, TYourExtension>();
				_binders[extension] = binderInstance;
				return binderInstance;
			}
			throw new InvalidOperationException($"Do not create more than one ExtensibleBinder for your type {extension.FullName}! You should only create this once (in your mod's Awake/OnEnable).");
		}

	}
}
