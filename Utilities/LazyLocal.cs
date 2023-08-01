using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HookGenExtender.Utilities {
	internal class LazyLocal : IVariable {
		public TypeSig Type => throw new NotSupportedException();
		public int Index { get; }
		public string Name {
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public LazyLocal(ushort index) {
			Index = index;
		}
	}
}
