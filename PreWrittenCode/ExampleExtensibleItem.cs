using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.PreWrittenCode {

	public class ExampleExtensibleItem {

		public ExampleItem? Original => _original.TryGetTarget(out ExampleItem? value) ? value : null;
		private WeakReference<ExampleItem> _original = new WeakReference<ExampleItem>(null!);

		public string name {
			get => Original!.name;
			set => Original!.name = value;
		}

		public void Die() {
			// blah
		}

		public ExampleExtensibleItem() { }

		public static class Binder<T> where T : ExampleExtensibleItem, new() {

			private static ConditionalWeakTable<ExampleItem, T> _instances = new ConditionalWeakTable<ExampleItem, T>();
			private static bool _didFirstInit = false;

			static Binder() {
				ExampleItem.OnDoingThing += ExampleItem_OnDoingThing;
			}

			private static int ExampleItem_OnDoingThing(ExampleItem.orig_DoThing originalMethod, ExampleItem @this) {
				throw new NotImplementedException();
			}

			public static WeakReference<T> Bind(ExampleItem original) {
				T instance = new T();
				instance._original.SetTarget(original);
				_instances.Add(original, instance);
				return new WeakReference<T>(instance);
			}

			private static void Die(object orig, ExampleItem self) {
				if (_instances.TryGetValue(self, out T? target)) {
					target.Die();
				}
			}

		}

	}
}
