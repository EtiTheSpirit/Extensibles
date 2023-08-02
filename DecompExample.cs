using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender {
	public class DecompExample {

		private static readonly ConditionalWeakTable<object, string> test;

		static DecompExample() {
			test = new ConditionalWeakTable<object, string>();
		}

	}
}
