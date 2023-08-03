using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender {
	public class DecompExample {

		private readonly WeakReference<object> test;

		public DecompExample() {
			test = new WeakReference<object>(null);
			
		}

	}
}
