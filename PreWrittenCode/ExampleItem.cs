using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.PreWrittenCode {
	public class ExampleItem {

		public string name;

		public static event orig_DoThing OnDoingThing;

		public delegate int orig_DoThing(orig_DoThing originalMethod, ExampleItem @this);

	}
}
